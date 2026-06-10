using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 꾸미기 아이템 배치 컨트롤러
/// InventoryUI에서 아이템 선택 시 StartPlacement() 호출 → 고스트 프리뷰 → 클릭으로 배치
/// R키: 배치 전 좌우반전 / ESC·우클릭: 취소
/// </summary>
public class DecoPlacementController : MonoBehaviour
{
    public static DecoPlacementController Instance { get; private set; }

    private const float GRID = 0.5f;

    // 아이템명 → Resources 경로
    static readonly Dictionary<string, string> PrefabPaths = new()
    {
        { "나무",   "deco/deco1_Tree_0" },
        { "나무풍 상자",   "deco/deco1_WoodenBox_0"      },
        { "나무풍 의자",   "deco/deco1_WoodChair_0"      },
        { "나무풍 울타리", "deco/deco1_WoodFence_0"      },
        { "나무풍 꽃병",   "deco/deco1_WoodVase_0"      },
        { "나무풍 탁자",   "deco/deco1_WoodTable_0"      },
        { "나무풍 꽃밭",   "deco/deco1_WoodFlowerField_0"      },
    };

    static readonly Dictionary<string, int> DecoScores = new()
    {
        { "나무", 20 }, { "나무풍 상자", 10 }, { "나무풍 의자", 20 },
        { "나무풍 울타리", 25 }, { "나무풍 꽃병", 30 }, { "나무풍 탁자", 50 }, { "나무풍 꽃밭", 100 }
    };

    private string _selectedItem;
    private GameObject _ghost;
    private bool _isPlacing;
    private bool _isFlipped;
    private bool _skipNextClick;   // 인벤토리 버튼 클릭과 배치 클릭 분리
    private TrashCollector _collector;

    // 로컬 점수 (DecoScene에서 GameManager RPC 실패 시 폴백)
    private int _localScore;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "DecoScene")
        {
            BuildDecoSceneUI();
            RestoreSavedDecorations();
        }
    }

    // ─────────────────────────────────────────
    //  외부 진입점
    // ─────────────────────────────────────────

    public void StartPlacement(string itemName, TrashCollector collector)
    {
        if (_isPlacing) CancelPlacement();

        if (!PrefabPaths.TryGetValue(itemName, out string path))
        {
            Debug.LogWarning($"[DecoPlacement] 프리팹 경로 없음: {itemName}");
            return;
        }

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[DecoPlacement] 프리팹 로드 실패: {path}");
            return;
        }

        _selectedItem   = itemName;
        _collector      = collector;
        _isPlacing      = true;
        _isFlipped      = false;
        _skipNextClick  = true;    // 인벤토리 클릭과 같은 프레임 배치 방지

        // 배치 모드 진입 시 모든 인벤토리 UI 닫기 (어떤 경로로 진입하든 확실히)
        foreach (var inv in FindObjectsByType<InventoryUI>(FindObjectsSortMode.None))
            inv.CloseInventory();
        UIManager.Instance?.ForceCloseInventory();

        // 고스트 생성
        _ghost = Instantiate(prefab);
        _ghost.name = "DecoGhost";

        // 콜라이더 비활성화 (배치 클릭 방해 방지)
        foreach (var col in _ghost.GetComponentsInChildren<Collider2D>())
            col.enabled = false;

        // 반투명 처리
        foreach (var sr in _ghost.GetComponentsInChildren<SpriteRenderer>())
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0.5f);

        UIManager.Instance?.ShowStatusMessage($"{itemName} 선택됨 — 클릭으로 배치 / R: 반전 / ESC: 취소", 3f);
    }

    // ─────────────────────────────────────────
    //  매 프레임
    // ─────────────────────────────────────────

    void Update()
    {
        if (!_isPlacing || _ghost == null) return;

        UpdateGhostPosition();

        // R키: 좌우 반전
        if (Input.GetKeyDown(KeyCode.R))
            ToggleFlip();

        // 인벤토리 클릭과 같은 프레임은 건너뜀
        if (_skipNextClick)
        {
            _skipNextClick = false;
            return;
        }

        // UI 위에서는 클릭 무시
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 좌클릭: 배치
        if (!overUI && Input.GetMouseButtonDown(0))
        {
            PlaceItem();
            return;
        }

        // 우클릭 / ESC: 취소
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    // ─────────────────────────────────────────
    //  고스트 위치 갱신
    // ─────────────────────────────────────────

    void UpdateGhostPosition()
    {
        if (Camera.main == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        float snappedX = Mathf.Round(mouseWorld.x / GRID) * GRID;
        float snappedY = Mathf.Round(mouseWorld.y / GRID) * GRID;

        // PlayerZoneManager 있으면 구역 내로 클램프
        if (PlayerZoneManager.Instance != null && _collector != null)
        {
            int slot = PlayerZoneManager.Instance.GetLocalSlot();
            var (minX, maxX) = PlayerZoneManager.Instance.GetZoneBounds(slot);
            snappedX = Mathf.Clamp(snappedX, minX + GRID, maxX - GRID);
        }

        _ghost.transform.position = new Vector3(snappedX, snappedY, -1f);
    }

    // ─────────────────────────────────────────
    //  반전 토글
    // ─────────────────────────────────────────

    void ToggleFlip()
    {
        _isFlipped = !_isFlipped;
        Vector3 s = _ghost.transform.localScale;
        s.x *= -1f;
        _ghost.transform.localScale = s;
    }

    // ─────────────────────────────────────────
    //  배치 확정
    // ─────────────────────────────────────────

    void PlaceItem()
    {
        if (!PrefabPaths.TryGetValue(_selectedItem, out string path))
        {
            CancelPlacement();
            return;
        }

        // 인벤토리 확인 및 차감
        if (_collector != null)
        {
            if (!_collector.inventory.TryGetValue(_selectedItem, out int cnt) || cnt <= 0)
            {
                UIManager.Instance?.ShowStatusMessage($"{_selectedItem}이(가) 없습니다!", 2f);
                CancelPlacement();
                return;
            }
            _collector.inventory[_selectedItem]--;
            if (_collector.inventory[_selectedItem] <= 0)
                _collector.inventory.Remove(_selectedItem);
            _collector.RefreshUI();
        }

        Vector3 placePos = _ghost.transform.position;
        bool    flipped  = _isFlipped;

        // 실제 프리팹 배치
        SpawnDecoObject(_selectedItem, path, placePos, flipped);

        // 브릿지에 저장 (DecoScene 재진입 시 복원)
        DecoInventoryBridge.AddDeco(_selectedItem, placePos, flipped);

        // 꾸미기 점수 추가
        int score     = DecoScores.TryGetValue(_selectedItem, out int sc) ? sc : 10;
        _localScore  += score;
        UpdateScoreUI();

        // slotIndex 계산 (gmValid 무관하게 먼저 구함)
        var runner    = FindFirstObjectByType<Fusion.NetworkRunner>();
        int slotIndex = runner != null ? runner.LocalPlayer.PlayerId - 1 : 0;

        // LocalPlayerScores 항상 업데이트 (리더보드 폴백)
        if (slotIndex >= 0 && slotIndex < 4)
            GameManager.LocalPlayerScores[slotIndex] += score;

        bool gmValid = GameManager.Instance != null &&
                       GameManager.Instance.Object != null &&
                       GameManager.Instance.Object.IsValid;

        if (gmValid)
        {
            GameManager.Instance.RPC_AddDecorScore(score);
            GameManager.Instance.RPC_AddPlayerDecorScore(slotIndex, score);
            GameManager.Instance.RPC_PlaceDecoItem(_selectedItem, placePos);
            GameManager.Instance.RPC_TrackPlacedItem(_selectedItem);
        }

        // 날씨 처리 (나무 배치 시)
        if (_selectedItem == "나무")
            WeatherManager.Instance?.AddTree();

        UIManager.Instance?.ShowStatusMessage($"{_selectedItem} 배치 완료! +{score}pt", 2f);

        Destroy(_ghost);
        _ghost     = null;
        _isPlacing = false;
    }

    // ─────────────────────────────────────────
    //  프리팹 스폰 (배치 + 복원 공용)
    // ─────────────────────────────────────────

    void SpawnDecoObject(string itemName, string resourcePath, Vector3 pos, bool flipped)
    {
        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null) return;

        var placed = Instantiate(prefab, pos, Quaternion.identity);
        placed.name = itemName;

        if (flipped)
        {
            Vector3 s = placed.transform.localScale;
            s.x *= -1f;
            placed.transform.localScale = s;
        }

        placed.AddComponent<PlacedDecoItem>();
    }

    // ─────────────────────────────────────────
    //  저장된 꾸미기 아이템 복원
    // ─────────────────────────────────────────

    void RestoreSavedDecorations()
    {
        foreach (var deco in DecoInventoryBridge.SavedDecorations)
        {
            if (!PrefabPaths.TryGetValue(deco.itemName, out string path)) continue;
            SpawnDecoObject(deco.itemName, path, deco.position, deco.flipped);
        }
        Debug.Log($"[DecoPlacement] 저장된 꾸미기 {DecoInventoryBridge.SavedDecorations.Count}개 복원");
    }

    // ─────────────────────────────────────────
    //  취소
    // ─────────────────────────────────────────

    void CancelPlacement()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost     = null;
        _isPlacing = false;
    }

    public bool IsPlacing => _isPlacing;

    // ─────────────────────────────────────────
    //  DecoScene 전용 UI (점수 + 돌아가기 버튼)
    // ─────────────────────────────────────────

    void BuildDecoSceneUI()
    {
        // 현재 씬 Canvas 찾기
        Canvas canvas = null;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.gameObject.scene.name == sceneName) { canvas = c; break; }
        }
        if (canvas == null) return;

        // 돌아가기 버튼 (좌하단)
        var btnObj = new GameObject("ReturnBtn", typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(canvas.transform, false);
        var br = btnObj.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(0f, 0f);
        br.pivot = new Vector2(0f, 0f);
        br.anchoredPosition = new Vector2(10f, 10f);
        br.sizeDelta = new Vector2(180f, 50f);
        btnObj.GetComponent<Image>().color = new Color(0.20f, 0.55f, 0.20f, 0.95f);

        var lblObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblObj.transform.SetParent(btnObj.transform, false);
        var lr = lblObj.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        var lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.text = "쓰레기존으로 돌아가기";
        lblTmp.fontSize = 13f;
        lblTmp.fontStyle = FontStyles.Bold;
        lblTmp.alignment = TextAlignmentOptions.Center;
        lblTmp.color = Color.white;

        btnObj.GetComponent<Button>().onClick.AddListener(ReturnToTrashZone);
    }

    void UpdateScoreUI()
    {
        UIManager.Instance?.UpdateDecorScore(_localScore, 0);
    }

    void ReturnToTrashZone()
    {
        // 로컬 collector 확보 (아이템 배치를 안 했으면 _collector가 null일 수 있음)
        var collector = _collector;
        if (collector == null)
        {
            foreach (var tc in FindObjectsByType<TrashCollector>(FindObjectsSortMode.None))
            {
                if (tc.HasInputAuthority) { collector = tc; break; }
            }
        }

        // 현재 인벤토리(DecoScene에서 사용 후 남은 것)를 브릿지에 최신화 + 복귀 플래그 설정
        if (collector != null)
            DecoInventoryBridge.PrepareReturn(collector);
        else
            DecoInventoryBridge.MarkReturning();  // collector 못 찾아도 복귀 플래그는 설정

        if (PhotonManager.Instance != null)
            PhotonManager.Instance.LoadGameScene();
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("TrashZoneScene");
    }
}
