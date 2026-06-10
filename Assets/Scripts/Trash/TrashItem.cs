using UnityEngine;
using Fusion;

/// <summary>
/// 쓰레기 아이템 개별 스크립트 (Photon Fusion 버전)
///
/// [버그 수정 내역]
/// 1. Picked 텍스트 떠다님 → Update()의 OverlapCircle 감지 제거.
///    OnTriggerEnter/Exit2D 단일 경로로 통일.
/// 2. 콜라이더 범위 이상 → extents.magnitude * 2f 제거 (Trigger가 직접 처리).
/// 3. AudioManager 미연결 → TryCollect()에 PlayTrashCollect() 추가.
/// </summary>
public class TrashItem : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────

    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;
    private bool _isCollected = false;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    void Start()
    {
        // 스폰 직후 이미 트리거 안에 플레이어가 있을 경우를 위한 체크
        // (단, 범위는 Collider2D 자체 크기만 사용)
        StartCoroutine(CheckPlayerAfterSpawn());
    }

    void Update()
    {
        // ✅ [버그 수정 1,2]
        // 이전: Update()에서 매 프레임 OverlapCircle로 플레이어 감지
        // → OnTriggerEnter/Exit2D와 이중 감지 충돌 → "Picked" 텍스트 오류
        // → extents.magnitude * 2f 계산으로 감지 범위 과도하게 커짐
        //
        // 수정: Update()에서 거리 감지 완전 제거.
        //       플레이어 감지는 OnTriggerEnter/Exit2D 단일 경로로만 처리.
        //       (Collider2D를 Is Trigger = true 로 설정해야 함)

        // E키 수거만 처리 (감지는 Trigger가 담당)
        if (_isPlayerNearby && _playerCollector != null && Input.GetKeyDown(KeyCode.E))
        {
            var pm = _playerCollector.GetComponent<PlayerMovement>();
            if (pm != null && pm.HasInputAuthority)
                TryCollect();
        }
    }

    // ─────────────────────────────────────────
    //  Trigger 이벤트 (단일 감지 경로)
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어가 쓰레기 Trigger 안으로 들어왔을 때
    ///
    /// [중요] TrashItem의 Collider2D는 반드시 Is Trigger = true 이어야 함.
    ///        콜라이더 크기가 "Picked" 표시 범위를 결정하므로
    ///        Inspector에서 적절한 크기(약 0.5~0.8 radius)로 설정하세요.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return; // 내 캐릭터만 처리

        _isPlayerNearby = true;
        _playerCollector = other.GetComponent<TrashCollector>();
        UIManager.Instance?.OnTrashEnter();

        Debug.Log($"[TrashItem] 플레이어 진입: {gameObject.name}");
    }

    /// <summary>
    /// 플레이어가 쓰레기 Trigger 밖으로 나갔을 때
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        _isPlayerNearby = false;
        _playerCollector = null;
        UIManager.Instance?.OnTrashExit();

        Debug.Log($"[TrashItem] 플레이어 이탈: {gameObject.name}");
    }

    // ─────────────────────────────────────────
    //  스폰 직후 체크 (이미 Trigger 안에 있을 때)
    // ─────────────────────────────────────────

    /// <summary>
    /// 쓰레기가 플레이어 위에 스폰되는 경우 대비.
    /// Trigger Enter 이벤트는 스폰 시점에 발생하지 않을 수 있어서 1프레임 후 수동 체크.
    ///
    /// [버그 수정 2] 이전: extents.magnitude를 반경으로 사용 → 실제보다 넓음
    ///              수정: Collider2D의 bounds.extents.x (가로 절반)만 사용
    /// </summary>
    private System.Collections.IEnumerator CheckPlayerAfterSpawn()
    {
        yield return null; // 1프레임 대기 (Collider 초기화 완료 후)

        Collider2D col = GetComponent<Collider2D>();
        if (col == null) yield break;

        // ✅ 수정: x축 반경만 사용 (원형 콜라이더 기준으로 정확한 범위)
        // magnitude는 대각선 길이라 실제 원보다 항상 더 크게 나옴
        float checkRadius = col.bounds.extents.x;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, checkRadius);

        foreach (Collider2D hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            var pm = hit.GetComponent<PlayerMovement>();
            if (pm == null || !pm.HasInputAuthority) continue;

            _isPlayerNearby = true;
            _playerCollector = hit.GetComponent<TrashCollector>();
            UIManager.Instance?.OnTrashEnter();
            Debug.Log("[TrashItem] 스폰 시 플레이어 감지됨");
            yield break;
        }
    }

    // ─────────────────────────────────────────
    //  수거 처리
    // ─────────────────────────────────────────

    /// <summary>
    /// 실제 수거 로직.
    /// 중복 수거 방지(_isCollected) → 인벤토리 추가 → 배터리 소모 → UI + 오디오 피드백
    /// </summary>
    void TryCollect()
    {
        if (_isCollected) return;
        if (_playerCollector == null) return;

        var pm = _playerCollector.GetComponent<PlayerMovement>();

        // 배터리 방전 상태면 수거 불가
        if (pm != null && !pm.CanAct)
        {
            UIManager.Instance?.ShowStatusMessage("배터리 방전! 수거 불가", 2f);
            return;
        }

        _isCollected = true;

        string itemName = gameObject.name.Replace("(Clone)", "").Trim();

        // 인벤토리에 추가 (RPC → Host → [Networked] 동기화)
        _playerCollector.RPC_AddItem(itemName);

        // 배터리 소모 (RPC → Host → [Networked] 동기화)
        pm?.RPC_DrainBattery(5f);

        // UI 피드백
        UIManager.Instance?.OnTrashCollected();
        UIManager.Instance?.ShowStatusMessage($"{itemName} 수거!", 1.5f);

        // ✅ [버그 수정 3] AudioManager 연결
        // 이전: AudioManager 호출 없음 → 수거 SFX 재생 안 됨
        // 수정: AudioManager.Instance?.PlayTrashCollect() 추가
        AudioManager.Instance?.PlayTrashCollect();

        Debug.Log($"[수거] {itemName} 수거 완료!");

        // Host에게 카운트 증가 + Despawn 요청
        RPC_NotifyTrashCollected();
    }

    // ─────────────────────────────────────────
    //  RPC
    // ─────────────────────────────────────────

    /// <summary>
    /// Host(StateAuthority)가 받아서 처리:
    /// 1. GameManager 수거 카운트 증가
    /// 2. Runner.Despawn → 모든 클라이언트에서 동시 삭제
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_NotifyTrashCollected()
    {
        GameManager.Instance?.OnTrashCollected();
        Runner.Despawn(Object);
    }
}