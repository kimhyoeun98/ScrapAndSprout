using UnityEngine;
using TMPro;

/// <summary>
/// 쓰레기 아이템 개별 스크립트 (E키 수거 방식)
/// [부착 위치] can_0, can_1, banana_0 등 쓰레기 프리팹 각각에 부착
/// </summary>
public class TrashItem : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    /// <summary>플레이어가 범위 안에 있는지</summary>
    private bool _isPlayerNearby = false;

    /// <summary>수거 대상 플레이어의 TrashCollector</summary>
    private TrashCollector _playerCollector;

    /// <summary>이미 수거됐는지 (중복 수거 방지)</summary>
    private bool _isCollected = false;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 한 프레임 뒤에 스폰 위치 플레이어 체크
        // (Instantiate 직후엔 Physics2D가 아직 갱신 전이라 바로 체크하면 못 찾음)
        StartCoroutine(CheckPlayerAfterSpawn());
    }

    void Update()
    {
        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
            TryCollect();
    }

    // ─────────────────────────────────────────
    //  스폰 시 플레이어 체크
    // ─────────────────────────────────────────

    /// <summary>
    /// 스폰 직후 한 프레임 기다렸다가 플레이어가 이미
    /// 범위 안에 있는지 확인합니다.
    /// </summary>
    private System.Collections.IEnumerator CheckPlayerAfterSpawn()
    {
        // ⭐ 핵심: 한 프레임 대기
        yield return null;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) yield break;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            col.bounds.extents.magnitude
        );

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            _isPlayerNearby = true;
            _playerCollector = hit.GetComponent<TrashCollector>();

            // UIManager로 pick text 표시
            if (UIManager.Instance != null)
                UIManager.Instance.OnTrashEnter();

            Debug.Log("[쓰레기] 스폰 시 플레이어 감지! E키 활성화");
            yield break;
        }
    }

    // ─────────────────────────────────────────
    //  수거 로직
    // ─────────────────────────────────────────

    void TryCollect()
    {
        if (_isCollected) return;

        if (_playerCollector == null)
        {
            Debug.LogWarning("[쓰레기] TrashCollector를 찾을 수 없습니다!");
            return;
        }

        var pm = _playerCollector.GetComponent<PlayerMovement>();
        if (pm != null && !pm.CanAct)
        {
            Debug.Log("[쓰레기] 배터리 방전 상태!");
            if (UIManager.Instance != null)
                UIManager.Instance.ShowStatusMessage("배터리 방전! 수거 불가", 2f);
            return;
        }

        _isCollected = true;

        string itemName = gameObject.name.Replace("(Clone)", "").Trim();

        if (_playerCollector.inventory.ContainsKey(itemName))
            _playerCollector.inventory[itemName]++;
        else
            _playerCollector.inventory.Add(itemName, 1);

        _playerCollector.RefreshUI();
        pm?.DrainBattery(5f);

        // 수거 완료 → pick text 숨기기
        if (UIManager.Instance != null)
            UIManager.Instance.OnTrashCollected();

        if (UIManager.Instance != null)
            UIManager.Instance.ShowStatusMessage(
                $"{itemName} 수거! (보유: {_playerCollector.inventory[itemName]}개)", 1.5f);

        Debug.Log($"[수거] {itemName} 수거 완료!");

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    //  Trigger 충돌 감지
    // ─────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();

        // UIManager로 pick text 표시
        if (UIManager.Instance != null)
            UIManager.Instance.OnTrashEnter();

        Debug.Log("[쓰레기] 플레이어 접근 — E키로 수거 가능");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _isPlayerNearby = false;
        _playerCollector = null;

        // UIManager로 pick text 숨기기
        if (UIManager.Instance != null)
            UIManager.Instance.OnTrashExit();
    }
}