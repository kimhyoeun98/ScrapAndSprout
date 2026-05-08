using Fusion;
using UnityEngine;
using UnityEngine.Tilemaps;
/// <summary>
/// 씨앗 식재 시스템 (S-04: 씨앗 식재 → 타일 교체 + 서버 기록)
/// 
/// [이 클래스의 역할]
/// 플레이어가 Q키로 식재 모드를 켜고,
/// 마우스 클릭으로 오염 타일을 정화 타일로 바꾸는 시스템입니다.
///
/// [서버와의 관계]
/// 타일 교체는 클라이언트에서 즉시 처리하고 (빠른 반응성),
/// 서버에는 식재 사실을 기록만 합니다 (pollution_level 갱신용).
///
/// [부착 위치] Player 오브젝트에 부착하세요.
/// </summary>
// NetworkBehaviour = Photon RPC, HasInputAuthority 등 네트워크 기능 사용 가능
public class SeedPlanter : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────

    [Header("타일맵 연결")]
    [Tooltip("씬의 Grid > Tilemap 오브젝트를 드래그해서 연결하세요.")]
    public Tilemap targetTilemap;

    [Header("타일 에셋 설정")]
    [Tooltip("씨앗을 심을 수 있는 오염 타일 (기준 타일)")]
    public TileBase pollutedTile;
    [Tooltip("씨앗을 심으면 이 타일로 교체됩니다")]
    public TileBase purifiedTile;

    [Header("나무 프리팹")]
    [Tooltip("씨앗을 심으면 생성될 나무/식물 프리팹")]
    public GameObject treePrefab;

    [Header("식재 설정")]
    [Tooltip("식재 모드 활성화 키 (기본: Q)")]
    public KeyCode plantModeKey = KeyCode.Q;
    [Tooltip("정화 반경 (1=해당 칸만, 2=주변 1칸까지)")]
    public int purifyRadius = 1;

    // ─────────────────────────────────────────
    //  내부 상태 변수
    // ─────────────────────────────────────────

    /// <summary>현재 식재 모드인지 여부</summary>
    private bool _isPlantMode = false;

    /// <summary>같은 오브젝트의 TrashCollector 참조</summary>
    private TrashCollector _trashCollector;

    /// <summary>카메라 참조 (마우스 위치 변환에 필요)</summary>
    private Camera _mainCamera;

    /// <summary>
    /// 총 식재한 나무 수 (읽기 전용 프로퍼티)
    /// 외부에서 _treeCount = 5 같이 임의 변경 불가 → 서버 데이터 신뢰성 보호
    /// </summary>
    private int _treeCount = 0;
    public int TreeCount => _treeCount;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        _trashCollector = GetComponent<TrashCollector>();
        _mainCamera = Camera.main;

        if (_trashCollector == null)
            Debug.LogError("[SeedPlanter] ❌ TrashCollector 컴포넌트를 찾을 수 없습니다!");

        // targetTilemap 자동 연결 추가!
        if (targetTilemap == null)
            targetTilemap = FindFirstObjectByType<UnityEngine.Tilemaps.Tilemap>();

        if (targetTilemap == null)
            Debug.LogError("[SeedPlanter] ❌ targetTilemap이 연결되지 않았습니다!");
    }

    void Update()
    {
        if (!HasInputAuthority) return;
        // ── Q키: 식재 모드 토글 ──
        if (Input.GetKeyDown(plantModeKey))
        {
            TogglePlantMode();
        }

        // ── 식재 모드 + 마우스 클릭: 씨앗 심기 ──
        if (_isPlantMode && Input.GetMouseButtonDown(0))
        {
            TryPlantSeed();
        }
    }

    // ─────────────────────────────────────────
    //  핵심 함수들
    // ─────────────────────────────────────────

    /// <summary>
    /// Q키로 식재 모드를 켜고 끕니다.
    /// [비유] 게임의 '건설 모드' 진입과 동일합니다.
    /// </summary>
    void TogglePlantMode()
    {
        // 식재 모드 켜기 전에 씨앗 보유 확인
        if (!_isPlantMode && !HasSeed())
        {
            Debug.Log("[식재] 씨앗이 없습니다! NPC에서 구매하세요.");
            // TODO: UI 팝업 메시지 표시
            return;
        }

        _isPlantMode = !_isPlantMode;

        // 식재 모드 상태를 화면에 알림 (커서 변경으로 구분)
        if (_isPlantMode)
        {
            Debug.Log($"🌱 [식재 모드 ON] 심을 위치를 클릭하세요. 남은 씨앗: {GetSeedCount()}개");
            // 마우스 커서를 보이게 해서 클릭 위치를 확인할 수 있게 합니다
            Cursor.visible = true;
        }
        else
        {
            Debug.Log("🚫 [식재 모드 OFF]");
        }
    }

    /// <summary>
    /// 마우스 클릭 위치에 씨앗을 심는 핵심 함수입니다.
    ///
    /// [처리 순서]
    /// 1. 씨앗 보유 확인
    /// 2. 마우스 스크린 좌표 → 월드 좌표 변환
    /// 3. 월드 좌표 → 타일맵 셀 좌표 변환
    /// 4. 심을 수 있는 타일인지 확인
    /// 5. 타일 교체 (오염 → 정화)
    /// 6. 나무 프리팹 생성
    /// 7. 인벤토리에서 씨앗 차감
    /// 8. 서버에 식재 기록 전송 (비동기)
    /// </summary>
    void TryPlantSeed()
    {
        var pm = GetComponent<PlayerMovement>();
        if (pm != null && !pm.CanAct)
        {
            Debug.Log("[식재] 배터리 방전 상태에서는 식재할 수 없습니다!");
            return;
        }
        // ── 방어 코드 1: 씨앗 보유 확인 ──
        if (!HasSeed())
        {
            Debug.Log("[식재] 씨앗이 없습니다!");
            _isPlantMode = false;
            return;
        }

        // ── 방어 코드 2: 타일맵 연결 확인 ──
        if (targetTilemap == null)
        {
            Debug.LogError("[식재] targetTilemap이 없습니다!");
            return;
        }

        // ── 1단계: 마우스 스크린 좌표 → 월드 좌표 변환 ──
        // [비유] 지도(화면)에서 클릭한 점을 실제 GPS 좌표로 변환하는 것
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Mathf.Abs(_mainCamera.transform.position.z);
        Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

        // ── 2단계: 월드 좌표 → 타일맵 셀(격자) 좌표 변환 ──
        // [비유] GPS 좌표를 "3행 5열" 같은 격자 번호로 변환
        Vector3Int cellPosition = targetTilemap.WorldToCell(mouseWorldPos);

        // ── 3단계: 해당 위치에 타일이 있는지 확인 ──
        TileBase currentTile = targetTilemap.GetTile(cellPosition);

        if (currentTile == null)
        {
            Debug.Log("[식재] 이 위치에는 타일이 없습니다. 다른 위치를 클릭해주세요.");
            return;
        }

        // ── 4단계: 이미 정화된 타일인지 확인 ──
        if (currentTile == purifiedTile)
        {
            Debug.Log("[식재] 이미 정화된 땅입니다. 오염된 타일에만 심을 수 있습니다.");
            return;
        }

        // ─────────────── 심기 실행 ───────────────

        // ── 5단계: 타일 교체 (오염 → 정화) ──
        // ── 6단계: 나무 오브젝트 생성 ──
        // RPC로 모든 클라이언트에 동시 적용 (직접 호출하면 내 화면에서만 실행됨)
        RPC_PlantSync(cellPosition);

        // ── 7단계: 인벤토리에서 씨앗 1개 차감 ──
        ConsumeSeed();

        GetComponent<PlayerMovement>()?.DrainBattery(10f);

        // ── 8단계: 통계 업데이트 ──
        _treeCount++;
        Debug.Log($"🌳 식재 완료! 총 {_treeCount}그루 | 남은 씨앗: {GetSeedCount()}개");

        // ── 9단계: 서버에 식재 기록 전송 (비동기 — 게임 흐름 안 막음) ──
        ReportPlantingToServer(cellPosition);

        // ── 씨앗이 다 떨어지면 식재 모드 자동 종료 ──
        if (!HasSeed())
        {
            _isPlantMode = false;
            Debug.Log("[식재] 씨앗을 모두 사용했습니다. 식재 모드를 종료합니다.");
        }
    }

    // 모든 클라이언트에서 동시에 실행 — 타일 교체 + 나무 생성 + 할당량 카운트
    // RpcSources.All = 누구든 호출 가능, RpcTargets.All = 모든 클라이언트가 받아서 실행
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_PlantSync(Vector3Int cellPos)
    {
        PurifyTiles(cellPos);
        SpawnTree(cellPos);
        GameManager.Instance?.OnTreePlanted();
    }

    // ─────────────────────────────────────────
    //  타일 조작 함수들
    // ─────────────────────────────────────────

    /// <summary>
    /// 지정한 셀 중심으로 purifyRadius 범위의 타일을 정화 타일로 교체합니다.
    /// 
    /// [반경 예시]
    /// purifyRadius=1 → 클릭한 1칸만 교체
    /// purifyRadius=2 → 클릭한 칸 + 주변 8칸 (3x3) 교체
    /// </summary>
    void PurifyTiles(Vector3Int centerCell)
    {
        for (int x = -(purifyRadius - 1); x <= (purifyRadius - 1); x++)
        {
            for (int y = -(purifyRadius - 1); y <= (purifyRadius - 1); y++)
            {
                Vector3Int targetCell = new Vector3Int(
                    centerCell.x + x,
                    centerCell.y + y,
                    centerCell.z
                );

                TileBase tile = targetTilemap.GetTile(targetCell);

                // 타일이 있고, 아직 오염 상태인 경우에만 교체
                if (tile != null && tile != purifiedTile)
                {
                    targetTilemap.SetTile(targetCell, purifiedTile);
                }
            }
        }
    }

    /// <summary>
    /// 타일 중앙 위치에 나무 프리팹을 생성합니다.
    /// </summary>
    void SpawnTree(Vector3Int cellPosition)
    {
        if (treePrefab == null)
        {
            Debug.LogWarning("[식재] treePrefab이 연결되지 않았습니다. Inspector를 확인하세요.");
            return;
        }

        // 타일 셀의 정중앙 월드 좌표를 계산
        Vector3 spawnPosition = targetTilemap.GetCellCenterWorld(cellPosition);
        Instantiate(treePrefab, spawnPosition, Quaternion.identity);
    }

    // ─────────────────────────────────────────
    //  서버 연동
    // ─────────────────────────────────────────

    /// <summary>
    /// 서버에 씨앗 식재를 기록합니다 (비동기).
    ///
    /// [왜 비동기?]
    /// 서버 응답을 기다리면 게임이 잠깐 멈춥니다.
    /// 타일 교체는 이미 완료됐으므로, 서버 기록은 백그라운드로 보냅니다.
    /// 서버가 실패해도 타일은 정화된 상태로 유지됩니다.
    /// (추후 Photon 멀티 환경에서는 RPC로 대체 예정)
    /// </summary>
    void ReportPlantingToServer(Vector3Int cellPosition)
    {
        if (ApiManager.Instance == null)
        {
            Debug.LogWarning("[식재] ApiManager가 없습니다. 서버 기록을 건너뜁니다.");
            return;
        }

        // 타일 셀 좌표를 월드 좌표로 변환하여 서버에 전송
        Vector3 worldPos = targetTilemap.GetCellCenterWorld(cellPosition);

        PlantRequest request = new PlantRequest
        {
            playerId = ApiManager.Instance.playerId,
            posX = worldPos.x,
            posY = worldPos.y
        };

        ApiManager.Instance.PlantSeed(request,
            // 성공: pollution_level 갱신 정보를 로그로 확인
            (response) =>
            {
                Debug.Log($"[서버 기록] 식재 완료 | 총 나무: {response.treeCount} | {response.message}");
            },
            // 실패: 클라이언트 타일은 이미 바뀌었으므로 경고만 출력
            (error) =>
            {
                Debug.LogWarning($"[서버 기록] 실패 (클라이언트 타일은 유지됨): {error}");
            }
        );
    }

    // ─────────────────────────────────────────
    //  인벤토리 연동 헬퍼
    // ─────────────────────────────────────────

    /// <summary>씨앗이 1개 이상 있는지 확인합니다.</summary>
    bool HasSeed()
    {
        return _trashCollector != null
               && _trashCollector.inventory.ContainsKey("Seed")
               && _trashCollector.inventory["Seed"] > 0;
    }

    /// <summary>현재 보유 씨앗 수량을 반환합니다.</summary>
    int GetSeedCount()
    {
        return HasSeed() ? _trashCollector.inventory["Seed"] : 0;
    }

    /// <summary>
    /// 인벤토리에서 씨앗 1개를 차감하고 UI를 갱신합니다.
    /// 수량이 0이 되면 딕셔너리에서 완전히 제거합니다.
    /// </summary>
    void ConsumeSeed()
    {
        if (!HasSeed()) return;

        _trashCollector.inventory["Seed"]--;

        // 0개가 되면 딕셔너리에서 제거 (빈 슬롯이 UI에 남지 않도록)
        if (_trashCollector.inventory["Seed"] <= 0)
            _trashCollector.inventory.Remove("Seed");

        // TrashCollector의 UI를 갱신하여 인벤토리 슬롯에 반영
        _trashCollector.RefreshUI();
    }
}