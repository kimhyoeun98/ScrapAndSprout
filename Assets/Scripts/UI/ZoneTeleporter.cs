using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ZoneTeleporter : MonoBehaviour
{
    [Header("── 텔레포트 설정 ──")]
    public Transform destination;
    public float cooldown = 3f;
    public float fadeDuration = 0.3f;

    [Header("── 안내 UI ──")]
    public GameObject guideUI;

    private GameObject _playerInRange = null;
    private bool _isTeleporting = false;
    private bool _downKeyBuffered = false;
    private HashSet<GameObject> _cooldownPlayers = new HashSet<GameObject>();

    void Start()
    {
        if (destination == null)
            Debug.LogWarning($"[ZoneTeleporter] destination 미연결!");
        if (guideUI != null)
            guideUI.SetActive(false);
    }

    void Update()
    {
        // ↓키 버퍼
        if (Input.GetKeyDown(KeyCode.DownArrow))
            _downKeyBuffered = true;

        if (_playerInRange == null) return;
        if (_isTeleporting) return;
        if (_cooldownPlayers.Contains(_playerInRange)) return;
        if (destination == null) return;

        if (_downKeyBuffered)
        {
            _downKeyBuffered = false;
            StartCoroutine(TeleportRoutine(_playerInRange));
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 봇 자동 텔레포트
        var bot = other.GetComponent<AIBotController>();
        if (bot != null && destination != null)
        {
            if (!_cooldownPlayers.Contains(other.gameObject))
                StartCoroutine(BotTeleportRoutine(other.gameObject));
            return;
        }

        if (!other.CompareTag("Player")) return;
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        // 쿨다운 중이면 진입 무시
        if (_cooldownPlayers.Contains(other.gameObject)) return;

        _playerInRange = other.gameObject;
        _downKeyBuffered = false; // 진입 시 버퍼 초기화 (이전 입력 무시)
        if (guideUI != null) guideUI.SetActive(true);
        Debug.Log("[텔레포터] 플레이어 진입 — ↓키로 이동");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        var pm = other.GetComponent<PlayerMovement>();
        if (pm == null || !pm.HasInputAuthority) return;

        // 텔레포트 중엔 Exit 무시 (텔레포트로 나간 거라서)
        if (_isTeleporting) return;

        _playerInRange = null;
        _downKeyBuffered = false;
        if (guideUI != null) guideUI.SetActive(false);
    }

    IEnumerator BotTeleportRoutine(GameObject bot)
    {
        if (destination == null) yield break;

        _cooldownPlayers.Add(bot);

        yield return new WaitForSeconds(0.3f);

        var rb = bot.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        Vector3 targetPos = destination.position;
        targetPos.z = -1.2f;
        bot.transform.position = targetPos;

        Debug.Log($"[텔레포터] 봇 텔레포트 → {targetPos}");

        yield return new WaitForSeconds(cooldown);
        _cooldownPlayers.Remove(bot);
    }

    IEnumerator TeleportRoutine(GameObject player)
    {
        _isTeleporting = true;
        _cooldownPlayers.Add(player);
        _downKeyBuffered = false;

        // 1. 페이드 아웃
        if (fadeDuration > 0f)
        {
            UIManager.Instance?.FadeOut(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        // 2. 위치 이동 (RPC → StateAuthority에서 실행)
        var pm = player.GetComponent<PlayerMovement>();
        Vector3 targetPos = destination.position;
        targetPos.z = -1.2f;

        if (pm != null)
        {
            pm.RPC_Teleport(targetPos);
            Debug.Log($"[텔레포트] RPC_Teleport → {targetPos}");
        }

        // Photon 동기화 대기
        yield return new WaitForSeconds(0.3f);

        // 3. 페이드 인
        if (fadeDuration > 0f)
        {
            UIManager.Instance?.FadeIn(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        // 4. 텔레포트 완료 — UI 정리
        _playerInRange = null;
        if (guideUI != null) guideUI.SetActive(false);
        _isTeleporting = false;
        _downKeyBuffered = false;

        // 5. 쿨다운 후 재진입 허용
        yield return new WaitForSeconds(cooldown);
        _cooldownPlayers.Remove(player);
    }

    void OnDrawGizmos()
    {
        if (destination == null) return;
        Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.8f);
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.5f);

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(0.5f, 0.3f, 1f, 0.3f);
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
        }
    }
}