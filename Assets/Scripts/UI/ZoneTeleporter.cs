using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class ZoneTeleporter : MonoBehaviour
{
    [Header("── 텔레포트 설정 ──")]
    public Transform destination;
    public float cooldown = 3f;
    public float fadeDuration = 0.3f;

    [Header("── 씬 전환 ──")]
    [Tooltip("체크하면 씬 전환, 미체크하면 위치 이동")]
    public bool teleportToScene = false;
    [Tooltip("이동할 씬 이름")]
    public string sceneName = "DecoScene";

    [Header("── 안내 UI ──")]
    public GameObject guideUI;

    private GameObject _playerInRange = null;
    private bool _isTeleporting = false;
    private bool _downKeyBuffered = false;
    private HashSet<GameObject> _cooldownPlayers = new HashSet<GameObject>();

    void Start()
    {
        // 이름으로 자동 감지: "DecoZone" 포함하면 DecoScene으로 이동
        if (gameObject.name.Contains("DecoZone") || gameObject.name.Contains("Deco"))
        {
            teleportToScene = true;
            sceneName = "DecoScene";
            Debug.Log($"[ZoneTeleporter] {gameObject.name} → DecoScene으로 자동 설정");
        }

        if (destination == null && !teleportToScene)
            Debug.LogWarning($"[ZoneTeleporter] destination 미연결!");
        if (guideUI != null)
            guideUI.SetActive(false);
    }

    void Update()
    {
        if (_playerInRange == null) return;
        if (_isTeleporting) return;
        if (_cooldownPlayers.Contains(_playerInRange)) return;
        if (destination == null && !teleportToScene) return;

        // 다운 키 감지
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            Debug.Log("[텔레포터] 다운 키 감지 → 텔레포트 시작");
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
        if (pm == null) return;

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
        if (pm == null) return;

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

        var botController = bot.GetComponent<AIBotController>();
        if (botController != null)
            botController.ForceIdle();

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

    /// <summary>
    /// 플레이어 텔레포트 루틴
    /// 페이드 아웃 → 위치 이동 → 페이드 인 순서로 실행
    /// HasInputAuthority 체크로 본인 화면에서만 페이드 효과 적용
    /// </summary>
    IEnumerator TeleportRoutine(GameObject player)
    {
        _isTeleporting = true;
        _cooldownPlayers.Add(player);
        _downKeyBuffered = false;

        var pm = player.GetComponent<PlayerMovement>();

        // 1. 페이드 아웃 — RPC로 본인 화면에서만 실행
        if (fadeDuration > 0f)
        {
            if (pm != null)
                pm.RPC_FadeOut(fadeDuration);
            yield return new WaitForSeconds(fadeDuration);
        }

        // 2. 씬 전환 또는 위치 이동
        if (teleportToScene)
        {
            Debug.Log($"[텔레포트] {sceneName}로 이동");

            // DecoScene → Fusion 경로 사용 (인벤토리 저장 + 모든 클라이언트 동시 이동)
            if (sceneName == "DecoScene" && PhotonManager.Instance != null)
            {
                // 복귀 시 스폰 위치 + 타이머 보존
                float elapsed   = GameManager.Instance != null ? GameManager.Instance.ElapsedTime   : 0f;
                int   decoScore = GameManager.Instance != null ? GameManager.Instance.SafeDecorScore : 0;
                DecoInventoryBridge.SaveReturnState(player.transform.position, elapsed, decoScore);

                PhotonManager.Instance.LoadDecoScene();
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
        else
        {
            // 위치 이동
            Vector3 targetPos = destination.position;
            targetPos.z = -1.2f;

            if (pm != null)
            {
                pm.RPC_Teleport(targetPos);
                Debug.Log($"[텔레포트] RPC_Teleport → {targetPos}");
            }

            // 3. Photon 동기화 대기
            yield return new WaitForSeconds(0.3f);

            // 4. 페이드 인 — RPC로 본인 화면에서만 실행
            if (fadeDuration > 0f)
            {
                if (pm != null)
                    pm.RPC_FadeIn(fadeDuration);
                yield return new WaitForSeconds(fadeDuration);
            }

            // 5. 텔레포트 완료 — UI 정리
            _playerInRange = null;
            if (guideUI != null) guideUI.SetActive(false);
            _isTeleporting = false;
            _downKeyBuffered = false;

            // 6. 쿨다운 후 재진입 허용
            yield return new WaitForSeconds(cooldown);
            _cooldownPlayers.Remove(player);
        }
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

    public void AddBotCooldown(GameObject bot)
    {
        StartCoroutine(BotInitialCooldown(bot));
    }

    IEnumerator BotInitialCooldown(GameObject bot)
    {
        _cooldownPlayers.Add(bot);
        yield return new WaitForSeconds(15f);
        _cooldownPlayers.Remove(bot);
        Debug.Log("[텔레포터] 봇 초기 쿨다운 해제");
    }

    public void TryTeleportByNetwork(GameObject player)
    {
        if (_isTeleporting) return;
        if (_cooldownPlayers.Contains(player)) return;
        if (destination == null) return;
        if (_playerInRange != player) return; 

        Debug.Log($"[Portal] TryTeleportByNetwork - player: {player.name}");
        StartCoroutine(TeleportRoutine(player));
    }
}