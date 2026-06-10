using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// 꾸미기 점수 리더보드 — ESC 시 화면 중앙 팝업
/// 씬에 직접 배치된 UI 오브젝트를 Inspector에서 연결해서 사용
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    public float refreshInterval = 0.5f;

    [Header("── UI 참조 ──")]
    [SerializeField] private GameObject panel;          // LBOverlay (어두운 배경 오버레이)
    [SerializeField] private TextMeshProUGUI[] rowTexts = new TextMeshProUGUI[4];  // 각 순위 텍스트
    [SerializeField] private Image[] rowBGs = new Image[4];                        // 각 순위 배경
    [SerializeField] private Button continueButton;     // 계속 게임 버튼
    [SerializeField] private Button exitButton;         // 로비로 나가기 버튼

    private float _timer;
    private bool _isOpen;

    // ── 테마 색상 ──
    static readonly Color C_BODY     = new Color(0.87f, 0.84f, 0.77f, 1.00f);
    static readonly Color C_ROW_EVEN = new Color(0.10f, 0.14f, 0.08f, 0.70f);
    static readonly Color C_ROW_ODD  = new Color(0.07f, 0.10f, 0.05f, 0.40f);
    static readonly Color C_ME_BG    = new Color(0.28f, 0.24f, 0.04f, 0.70f);
    static readonly Color C_ME_TEXT  = new Color(1.00f, 0.88f, 0.20f, 1.00f);

    static readonly string[] _charNames = { "", "알파", "베타", "감마", "델타" };

    void Start()
    {
        if (continueButton != null) continueButton.onClick.AddListener(() => Toggle());
        if (exitButton != null)     exitButton.onClick.AddListener(OnExit);
        if (panel != null)          panel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Toggle();
            return;
        }

        if (!_isOpen) return;
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = refreshInterval;
        Refresh();
    }

    public void Toggle()
    {
        if (panel == null)
        {
            Debug.LogWarning("[LeaderboardUI.Toggle] panel이 NULL — Inspector에서 연결 필요");
            return;
        }
        _isOpen = !_isOpen;
        panel.SetActive(_isOpen);
        Cursor.visible   = _isOpen;
        Cursor.lockState = CursorLockMode.None;
        if (_isOpen) Refresh();
    }

    void OnExit()
    {
        if (panel != null) panel.SetActive(false);
        _isOpen = false;
        if (PhotonManager.Instance != null) _ = PhotonManager.Instance.LeaveRoom();
        else SceneManager.LoadScene("LobbyScene");
    }

    // ─────────────────────────────────────────
    //  갱신
    // ─────────────────────────────────────────

    void Refresh()
    {
        if (PhotonManager.Instance == null) return;

        int localCharIdx = -1;
        foreach (var tc in FindObjectsByType<TrashCollector>(FindObjectsSortMode.None))
        {
            if (!tc.HasInputAuthority) continue;
            localCharIdx = (int)tc.characterType + 1;
            break;
        }

        // GameManager 네트워크 유효 여부와 관계없이 점수 안전 조회
        bool gmValid = GameManager.Instance != null &&
                       GameManager.Instance.Object != null &&
                       GameManager.Instance.Object.IsValid;

        var entries = new List<(string name, int score, bool isMe)>();
        for (int slot = 0; slot < 4; slot++)
        {
            if (!PhotonManager.Instance.HasPlayerInSlot(slot)) continue;
            int ci = PhotonManager.Instance.GetPlayerCharacter(slot);
            if (ci == 0) continue;
            string name  = ci < _charNames.Length ? _charNames[ci] : $"P{slot + 1}";
            int score = 0;
            if (gmValid)
            {
                try { score = GameManager.Instance.PlayerDecorScores.Get(slot); }
                catch { score = GameManager.LocalPlayerScores[slot]; }
            }
            else
            {
                score = GameManager.LocalPlayerScores[slot];
            }
            entries.Add((name, score, ci == localCharIdx));
        }
        entries.Sort((a, b) => b.score.CompareTo(a.score));

        string[] rankLabel = { "1위", "2위", "3위", "4위" };
        for (int i = 0; i < rowTexts.Length; i++)
        {
            if (rowTexts[i] == null) continue;
            if (i < entries.Count)
            {
                var (name, score, isMe) = entries[i];
                string label = isMe ? $"[나] {name}" : name;
                rowTexts[i].text  = $"{rankLabel[i]}   {label}   {score}pt";
                rowTexts[i].color = isMe ? C_ME_TEXT : C_BODY;
                if (rowBGs[i] != null)
                    rowBGs[i].color = isMe ? C_ME_BG : (i % 2 == 0 ? C_ROW_EVEN : C_ROW_ODD);
            }
            else
            {
                rowTexts[i].text = "";
                if (rowBGs[i] != null) rowBGs[i].color = Color.clear;
            }
        }
    }
}
