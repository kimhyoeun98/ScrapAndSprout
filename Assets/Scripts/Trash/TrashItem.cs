using UnityEngine;
using Fusion;

/// <summary>
/// 쓰레기 아이템 개별 스크립트 (Photon Fusion 버전)
/// NetworkBehaviour로 변경 — 수거 시 모든 클라이언트에서 동기화 삭제
/// </summary>
public class TrashItem : NetworkBehaviour
{
    private bool _isPlayerNearby = false;
    private TrashCollector _playerCollector;
    private bool _isCollected = false;

    void Start()
    {
        StartCoroutine(CheckPlayerAfterSpawn());
    }

    void Update()
    {
       
        // 매 프레임 직접 거리 체크로 대체
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            col.bounds.extents.magnitude * 2f
        );

        bool found = false;
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;
            var pm = hit.GetComponent<PlayerMovement>();
            if (pm != null && pm.HasInputAuthority)
            {
                if (!_isPlayerNearby)
                    UIManager.Instance?.OnTrashEnter();

                _isPlayerNearby = true;
                _playerCollector = hit.GetComponent<TrashCollector>();
                found = true;
                break;
            }
        }

        // 범위 벗어났을 때
        if (!found && _isPlayerNearby)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            UIManager.Instance?.OnTrashExit();
        }

        // E키 수거
        if (_isPlayerNearby && _playerCollector != null && Input.GetKeyDown(KeyCode.E))
        {
            var pm = _playerCollector.GetComponent<PlayerMovement>();
            if (pm != null && pm.HasInputAuthority)
                TryCollect();
        }
    }

    private System.Collections.IEnumerator CheckPlayerAfterSpawn()
    {
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
            if (UIManager.Instance != null)
                UIManager.Instance.OnTrashEnter();
            yield break;
        }
    }

    void TryCollect()
    {
        if (_isCollected) return;
        if (_playerCollector == null) return;

        var pm = _playerCollector.GetComponent<PlayerMovement>();
        if (pm != null && !pm.CanAct)
        {
            UIManager.Instance?.ShowStatusMessage("배터리 방전! 수거 불가", 2f);
            return;
        }

        _isCollected = true;

        string itemName = gameObject.name.Replace("(Clone)", "").Trim();

        // 인벤토리에 아이템 추가 (로컬)
        if (_playerCollector.inventory.ContainsKey(itemName))
            _playerCollector.inventory[itemName]++;
        else
            _playerCollector.inventory.Add(itemName, 1);

        _playerCollector.RefreshUI();
        pm?.RPC_DrainBattery(5f);

        UIManager.Instance?.OnTrashCollected();
        UIManager.Instance?.ShowStatusMessage(
            $"{itemName} 수거! (보유: {_playerCollector.inventory[itemName]}개)", 1.5f);

        Debug.Log($"[수거] {itemName} 수거 완료!");

        // Host한테 GameManager 카운트 증가 요청
        // Client는 [Networked] 변수를 직접 못 건드리므로 RPC로 Host에 요청
        RPC_NotifyTrashCollected();
    }

    /// <summary>
    /// 모든 클라이언트가 호출 가능 → Host(StateAuthority)가 받아서 처리
    /// GameManager의 [Networked] 변수는 Host만 수정 가능하므로 RPC 필요
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_NotifyTrashCollected()
    {
        // Host가 GameManager 카운트 증가
        GameManager.Instance?.OnTrashCollected();

        // Host가 Despawn — 모든 클라이언트에서 동시에 삭제!
        Runner.Despawn(Object);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var collector = other.GetComponent<TrashCollector>();
        var pm = other.GetComponent<PlayerMovement>();

        // 내 캐릭터(HasInputAuthority)만 저장
        if (pm != null && pm.HasInputAuthority)
        {
            _isPlayerNearby = true;
            _playerCollector = collector;
            UIManager.Instance?.OnTrashEnter();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        var pm = other.GetComponent<PlayerMovement>();
        if (pm != null && pm.HasInputAuthority)
        {
            _isPlayerNearby = false;
            _playerCollector = null;
            UIManager.Instance?.OnTrashExit();
        }
    }
    }