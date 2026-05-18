using UnityEngine;
using Fusion;

/// <summary>
/// 꾸미기 아이템 배치 시스템 (세이프 스테이지 전용)
///
/// [비유]
/// 인벤토리에서 꾸미기 아이템을 꺼내 세이프 스테이지에 놓는 행동입니다.
/// 배치하면 꾸미기 점수가 오르고, GameManager에 보고됩니다.
///
/// [씬 설정]
/// - 세이프 스테이지 안의 빈 공간에 DecoPlacePoint 오브젝트를 배치하세요.
/// - 이 스크립트가 붙은 오브젝트 주변에서 F키를 누르면 배치 UI가 뜹니다.
///
/// [연결 흐름]
/// 플레이어 F키 → DecorationPlacer → 배치 UI 선택 → GameManager.RPC_AddDecorScore
/// </summary>
public class DecorationPlacer : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  설정
    // ─────────────────────────────────────────

    [Header("── 배치 설정 ──")]
    [Tooltip("배치 UI 패널 (Inspector에서 드래그)")]
    public GameObject placementPanel;

    [Tooltip("배치 가능 위치들 (세이프 스테이지 내 빈 공간)")]
    public Transform[] decoPlacePoints;

    [Tooltip("배치 가능 범위 반경")]
    public float interactionRadius = 2f;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;

    // 현재 선택된 배치 위치 인덱스
    private int _selectedPointIndex = 0;

    // ─────────────────────────────────────────
    //  Update — 플레이어 감지 + F키 입력
    // ─────────────────────────────────────────

    void Update()
    {
        DetectNearbyPlayer();

        // F키: 배치 UI 열기/닫기
        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.F))
            TogglePlacementUI();
    }

    // ─────────────────────────────────────────
    //  플레이어 근접 감지
    // ─────────────────────────────────────────

    void DetectNearbyPlayer()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionRadius);

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            var pm = hit.GetComponent<PlayerMovement>();
            if (pm == null || !pm.HasInputAuthority) continue;

            _isPlayerNearby = true;
            _playerCollector = hit.GetComponent<TrashCollector>();
            found = true;
            break;
        }

        if (!found && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            ClosePlacementUI();
        }
    }

    // ─────────────────────────────────────────
    //  배치 UI 열기/닫기
    // ─────────────────────────────────────────

    void TogglePlacementUI()
    {
        if (placementPanel == null) return;

        bool isOpening = !placementPanel.activeSelf;
        placementPanel.SetActive(isOpening);

        if (isOpening)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("[DecorationPlacer] 배치 UI 열림");
        }
    }

    void ClosePlacementUI()
    {
        if (placementPanel != null)
            placementPanel.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  아이템 배치 — UI 버튼에서 호출
    //
    //  [사용법]
    //  배치 UI의 각 아이템 버튼 OnClick()에
    //  이 함수를 연결하고 itemName을 입력하세요.
    //  예: OnClick → PlaceItem("나무")
    // ─────────────────────────────────────────

    public void PlaceItem(string itemName)
    {
        if (_playerCollector == null)
        {
            Debug.LogWarning("[DecorationPlacer] 플레이어를 찾을 수 없습니다!");
            return;
        }

        // 인벤토리에 해당 아이템이 있는지 확인
        if (!_playerCollector.inventory.ContainsKey(itemName) ||
             _playerCollector.inventory[itemName] <= 0)
        {
            UIManager.Instance?.ShowStatusMessage($"{itemName}이(가) 없습니다!", 2f);
            return;
        }

        // 꾸미기 점수 계산 (감마 캐릭터 보정 포함)
        int score = _playerCollector.GetDecorScore(itemName);

        // 인벤토리에서 차감
        _playerCollector.inventory[itemName]--;
        if (_playerCollector.inventory[itemName] <= 0)
            _playerCollector.inventory.Remove(itemName);
        _playerCollector.RefreshUI();

        // GameManager에 점수 보고 (RPC로 Host에게 전달)
        GameManager.Instance?.RPC_AddDecorScore(score);

        // 나무 배치 시 사운드
        if (itemName == "나무" && AudioManager.Instance != null)
            AudioManager.Instance.PlaySeedPlant(); // 기존 씨앗 심기 사운드 재활용

        UIManager.Instance?.ShowStatusMessage($"{itemName} 배치! +{score}pt", 2f);
        Debug.Log($"[DecorationPlacer] {itemName} 배치 완료, 점수: {score}pt");

        // 배치 후 UI 닫기
        ClosePlacementUI();
    }

    // ─────────────────────────────────────────
    //  기즈모 — 에디터에서 범위 시각화
    // ─────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        if (decoPlacePoints == null) return;
        Gizmos.color = Color.cyan;
        foreach (var point in decoPlacePoints)
        {
            if (point != null)
                Gizmos.DrawWireCube(point.position, Vector3.one * 0.5f);
        }
    }
}