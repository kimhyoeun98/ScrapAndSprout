using UnityEngine;
using Fusion;

/// <summary>
/// 쓰레기 더미 오브젝트 (소형 / 대형)
///
/// [비유]
/// 기존 TrashItem이 "이미 떨어진 쓰레기 하나"라면,
/// TrashPile은 "쓰레기가 가득 쌓인 더미"입니다.
/// 플레이어가 E키를 누르면 미니게임이 시작되고,
/// 성공하면 더미에서 쓰레기 아이템이 드랍됩니다.
///
/// [연결 흐름]
/// TrashPile → MiningMinigame → (성공) → 아이템 드랍 → TrashCollector 인벤토리
/// </summary>
public class TrashPile : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  더미 종류 설정
    // ─────────────────────────────────────────

    public enum PileSize
    {
        Small,  // 소형: 기본 입력 4번, 날씨 악화 시 6번
        Large   // 대형: 기본 입력 6번, 날씨 악화 시 8번
    }

    [Header("── 더미 설정 ──")]
    public PileSize pileSize = PileSize.Small;

    [Header("── 감지 범위 ──")]
    [Tooltip("플레이어 감지 범위 (0이면 Collider 크기 자동 사용)")]
    public float interactionRadius = 2.5f;

    // 소형/대형 각각의 기본 입력 횟수 (인스펙터에서 조정 가능)
    [Tooltip("소형 더미 기본 입력 횟수")]
    public int smallBaseInputCount = 4;

    [Tooltip("대형 더미 기본 입력 횟수")]
    public int largeBaseInputCount = 6;

    [Tooltip("날씨 악화 시 추가 입력 횟수")]
    public int badWeatherExtraInputs = 2;

    // ─────────────────────────────────────────
    //  드랍 아이템 설정
    // ─────────────────────────────────────────

    [Header("── 드랍 아이템 프리팹 ──")]
    [Tooltip("드랍될 아이템 프리팹들 (TrashCollector가 인벤토리로 받음)")]
    public NetworkObject[] itemPrefabs;

    // ─────────────────────────────────────────
    //  드랍률 테이블 (기획서 기준)
    //
    //  [비유]
    //  주사위를 굴려서 어떤 아이템이 나올지 결정하는 확률표입니다.
    //  배열 순서: 휴지, 바나나껍질, 음료캔, 디스크, 타이어, 드럼통, 컴퓨터
    // ─────────────────────────────────────────

    // 기본 드랍률 (합계 = 100%)
    // 소형 더미는 타이어/드럼통/컴퓨터 인덱스(4,5,6)를 자동으로 제외합니다
    private static readonly float[] _baseDropRates =
    {
        35f,  // 0: 휴지
        30f,  // 1: 바나나껍질
        20f,  // 2: 음료캔
        10f,  // 3: 디스크
         3f,  // 4: 타이어    (소형 제외)
         1f,  // 5: 드럼통    (소형 제외)
         0.5f // 6: 컴퓨터   (소형 제외)
    };

    // 날씨 악화 시 드랍률 (합계 = 100%)
    private static readonly float[] _badWeatherDropRates =
    {
        44.5f,  // 0: 휴지
        38.25f, // 1: 바나나껍질
        10f,    // 2: 음료캔
         5f,    // 3: 디스크
         1.5f,  // 4: 타이어    (소형 제외)
         0.5f,  // 5: 드럼통    (소형 제외)
         0.25f  // 6: 컴퓨터   (소형 제외)
    };

    // 아이템 이름 (인덱스와 일치)
    private static readonly string[] _itemNames =
    {
        "휴지", "바나나껍질", "음료캔", "디스크", "타이어", "드럼통", "컴퓨터"
    };

    // ─────────────────────────────────────────
    //  판매 가격 (서버와 반드시 일치해야 함)
    // ─────────────────────────────────────────

    public static readonly int[] ItemPrices =
    {
          7,   // 0: 휴지
         10,   // 1: 바나나껍질
         15,   // 2: 음료캔
         30,   // 3: 디스크
        100,   // 4: 타이어
        200,   // 5: 드럼통
        350    // 6: 컴퓨터
    };

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;
    private bool _isBeingMined = false; // 현재 미니게임 진행 중인지

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Update()
    {
        if (_isBeingMined) return;
        DetectNearbyPlayer(); // ✅ 추가
        // E키 입력 감지 → 미니게임 시작 (OnTrigger로 근접 감지)
        if (_isPlayerNearby && _playerCollector != null && Input.GetKeyDown(KeyCode.E))
        {
            var pm = _playerCollector.GetComponent<PlayerMovement>();
            if (pm != null && !pm.CanAct)
            {
                UIManager.Instance?.ShowStatusMessage("배터리 방전! 채굴 불가", 2f);
                return;
            }
            StartMining();
        }
    }

    // ─────────────────────────────────────────
    //  플레이어 근접 감지
    // ─────────────────────────────────────────

    void DetectNearbyPlayer()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        // 더미 주변 원형 범위 안의 모든 콜라이더 탐색
        float radius = interactionRadius > 0f
            ? interactionRadius
            : col.bounds.extents.magnitude * 2f;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            radius
        );

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            var pm = hit.GetComponent<PlayerMovement>();
            // 내 캐릭터(조작권 있는)만 처리
            if (pm == null || !pm.HasInputAuthority) continue;

            if (!_isPlayerNearby)
                UIManager.Instance?.OnTrashEnter(); // "E키를 눌러 채굴" 안내 표시

            _isPlayerNearby = true;
            _playerCollector = hit.GetComponent<TrashCollector>();
            found = true;
            break;
        }

        // 범위에서 벗어났을 때
        if (!found && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            UIManager.Instance?.OnTrashExit(); // 안내 숨기기
        }
    }

    // ─────────────────────────────────────────
    //  채굴 시작 → 미니게임 패널 열기
    // ─────────────────────────────────────────

    void StartMining()
    {
        _isBeingMined = true;

        // 채굴 시도 자체에 배터리 -5% 소모 (날씨 악화 시 배율 적용)
        var pm = _playerCollector?.GetComponent<PlayerMovement>();
        float weatherMult = WeatherManager.Instance != null
            ? WeatherManager.Instance.GetBatteryMultiplier() : 1f;
        pm?.RPC_DrainBattery(5f * weatherMult);

        int requiredInputs = GetRequiredInputCount();

        Debug.Log($"[TrashPile] 채굴 시작! 더미 크기: {pileSize}, 필요 입력: {requiredInputs}회");

        if (MiningMinigame.Instance == null)
        {
            Debug.LogError("[TrashPile] MiningMinigame.Instance null — MiningMinigame 오브젝트가 활성화 상태인지 확인!");
            _isBeingMined = false;
            return;
        }

        MiningMinigame.Instance.StartMinigame(
            requiredInputs,
            OnMinigameResult
        );
    }

    // ─────────────────────────────────────────
    //  미니게임 결과 콜백
    //  성공: 아이템 드랍 / 실패: 배터리 소모
    // ─────────────────────────────────────────
   
    void OnMinigameResult(bool success)
    {
        _isBeingMined = false;

        if (success)
        {
            // 성공: 드랍 아이템 결정 후 인벤토리에 추가
            string droppedItemName = RollDropItem();
            GiveItemToPlayer(droppedItemName);

            // 베타 캐릭터 특성: 더블 드랍 확률 +5%
            if (IsPlayerBetaCharacter() && Random.Range(0f, 100f) < 5f)
            {
                string bonusItem = RollDropItem();
                GiveItemToPlayer(bonusItem);
                UIManager.Instance?.ShowStatusMessage($"더블 드랍! {droppedItemName} + {bonusItem}", 2f);
            }
            else
            {
                UIManager.Instance?.ShowStatusMessage($"채굴 성공! {droppedItemName} 획득", 1.5f);
            }

            // 업적 체크
            if (droppedItemName == "컴퓨터")
                AchievementManager.Instance?.OnRareItemFirstObtained("컴퓨터");
            AchievementManager.Instance?.OnMinigameSuccess();


           
            // Despawn 전 UI 강제 리셋 (Fusion Despawn 시 OnTriggerExit 미발생 대비)
            UIManager.Instance?.OnTrashCollected();

            // 수정 — Host면 바로 Despawn, 클라이언트면 RPC 요청
            if (HasStateAuthority)
            {
                Debug.Log("[TrashPile] Host — 바로 Despawn");
                Runner.Despawn(Object);
            }
            else
            {
                Debug.Log($"[TrashPile] Despawn 요청 — Object.IsValid:{Object?.IsValid}");
                RPC_RequestDespawn();
                Debug.Log("[TrashPile] 채굴 완료 — Despawn 요청");
            }
        }
        else
        {
            // 실패: 배터리 추가 소모 (날씨 악화 시 배율 적용)
            var pm = _playerCollector?.GetComponent<PlayerMovement>();
            float wm = WeatherManager.Instance != null
                ? WeatherManager.Instance.GetBatteryMultiplier() : 1f;
            pm?.RPC_DrainBattery(10f * wm);

            UIManager.Instance?.ShowStatusMessage("채굴 실패! 배터리 -10%", 1.5f);
            Debug.Log("[TrashPile] 채굴 실패 — 배터리 소모");
        }
    }

    // ─────────────────────────────────────────
    //  드랍 아이템 결정 (확률 룰렛)
    //
    //  [비유]
    //  원판 룰렛처럼 0~100 사이 랜덤 값을 뽑아서
    //  각 아이템의 확률 구간에 해당하면 그 아이템을 드랍합니다.
    // ─────────────────────────────────────────

    string RollDropItem()
    {
        // 날씨가 나쁘면 악화 드랍률 사용
        bool isBadWeather = WeatherManager.Instance != null &&
                            !WeatherManager.Instance.IsClear();

        float[] rates = isBadWeather ? _badWeatherDropRates : _baseDropRates;

        // 소형 더미는 타이어(4), 드럼통(5), 컴퓨터(6) 제외
        int maxIndex = (pileSize == PileSize.Small) ? 4 : 7;

        // 알파 캐릭터 특성: 희귀 아이템 드랍률 +0.5%
        bool isAlphaPlayer = IsPlayerAlphaCharacter();

        // 사용할 아이템들의 확률 합산 (재정규화)
        float total = 0f;
        for (int i = 0; i < maxIndex; i++)
        {
            float rate = rates[i];
            // 알파: 디스크(3)~컴퓨터(6)에 0.5% 추가
            if (isAlphaPlayer && i >= 3)
                rate += 0.5f;
            total += rate;
        }

        // 0 ~ total 사이 랜덤 값으로 아이템 결정
        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < maxIndex; i++)
        {
            float rate = rates[i];
            if (isAlphaPlayer && i >= 3)
                rate += 0.5f;

            cumulative += rate;
            if (roll <= cumulative)
                return _itemNames[i];
        }

        // 혹시 모를 예외 처리 (기본값: 휴지)
        return _itemNames[0];
    }

    // ─────────────────────────────────────────
    //  플레이어 인벤토리에 아이템 추가
    // ─────────────────────────────────────────

    void GiveItemToPlayer(string itemName)
    {
        if (_playerCollector == null) return;

        // RPC로 Host에게 인벤토리 추가 요청 (기존 TrashCollector 그대로 활용)
        _playerCollector.RPC_AddItem(itemName);

        Debug.Log($"[TrashPile] 아이템 지급: {itemName}");
    }

    // ─────────────────────────────────────────
    //  날씨 및 캐릭터 특성 헬퍼
    // ─────────────────────────────────────────

   

    bool IsPlayerAlphaCharacter()
    {
        if (_playerCollector == null) return false;
        // TrashCollector에 characterType 필드 추가 후 연결 예정
        return _playerCollector.characterType == TrashCollector.CharacterType.Alpha;
    }

    bool IsPlayerBetaCharacter()
    {
        if (_playerCollector == null) return false;
        return _playerCollector.characterType == TrashCollector.CharacterType.Beta;
    }

    // ─────────────────────────────────────────
    //  Gizmos — Scene 뷰에서 콜라이더 범위 시각화
    // ─────────────────────────────────────────

    void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        // 감지 범위 — 초록색 박스
        Gizmos.color = _isPlayerNearby
            ? new Color(0f, 1f, 0f, 0.5f)   // 플레이어 감지 시 밝은 초록
            : new Color(1f, 1f, 0f, 0.3f);  // 대기 시 노랑

        Gizmos.DrawCube(
            transform.position + (Vector3)col.offset,
            col.size
        );

        // 테두리
        Gizmos.color = _isPlayerNearby ? Color.green : Color.yellow;
        Gizmos.DrawWireCube(
            transform.position + (Vector3)col.offset,
            col.size
        );
    }

    // ─────────────────────────────────────────
    //  트리거 이벤트 (Collider 방식 보조)
    // ─────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();
        UIManager.Instance?.OnTrashEnter();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = false;
        _playerCollector = null;

        // 떠날 때 미니게임 중이었으면 취소
        if (_isBeingMined)
        {
            _isBeingMined = false;
            MiningMinigame.Instance?.CancelMinigame();
        }

        UIManager.Instance?.OnTrashExit();
    }
    // ─────────────────────────────────────────
    //  [Networked] 필요 입력 횟수 — 호스트가 스폰 시 계산해서 저장
    //  클라이언트는 이 값을 그대로 읽어서 사용 → 불일치 방지
    // ─────────────────────────────────────────
    [Networked] private int NetworkedRequiredInputs { get; set; }

    /// <summary>
    /// 네트워크 스폰 시 호스트가 입력 횟수 계산해서 [Networked]에 저장
    /// 클라이언트는 Spawned()에서 이미 동기화된 값을 받음
    /// </summary>
    public override void Spawned()
    {
        Debug.Log($"[TrashPile] Spawned — IsServer:{Runner.IsServer} / Position:{transform.position}");

        // 호스트만 계산해서 저장 — 클라이언트는 자동 동기화로 받음
        if (HasStateAuthority)
        {
            int baseCount = (pileSize == PileSize.Small) ? smallBaseInputCount : largeBaseInputCount;

            bool isBadWeather = WeatherManager.Instance != null &&
                                !WeatherManager.Instance.IsClear();

            NetworkedRequiredInputs = isBadWeather
                ? baseCount + badWeatherExtraInputs
                : baseCount;

            Debug.Log($"[TrashPile] 입력 횟수 확정: {NetworkedRequiredInputs}회 (날씨악화:{isBadWeather})");
        }
    }

    // 교체
    /// <summary>
    /// 입력 횟수 반환 — WeatherManager.CurrentWeather([Networked]) 직접 읽음
    /// 호스트/클라이언트 동일한 날씨값 보장됨
    /// </summary>
    int GetRequiredInputCount()
    {
        int baseCount = (pileSize == PileSize.Small) ? smallBaseInputCount : largeBaseInputCount;

        bool isBadWeather = WeatherManager.Instance != null &&
                            !WeatherManager.Instance.IsClear();

        return isBadWeather ? baseCount + badWeatherExtraInputs : baseCount;
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestDespawn()
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning("[TrashPile] RPC_RequestDespawn — Object 무효");
            return;
        }
        Debug.Log("[TrashPile] RPC_RequestDespawn — Despawn 실행");
        Runner.Despawn(Object);
    }
}