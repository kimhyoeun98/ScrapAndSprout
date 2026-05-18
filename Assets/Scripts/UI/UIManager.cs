using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────
    //  HUD 연결
    // ─────────────────────────────────────────
    [Header("── HUD ──")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI decorScoreText;
    public TextMeshProUGUI statusMessageText;

    [Header("── 인벤토리 ──")]
    public GameObject inventoryPanel;
    public GameObject hotbar;

    [Header("── 안내 텍스트 ──")]
    public GameObject pickText;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────
    private bool _isInventoryOpen = false;
    private int _nearbyTrashCount = 0;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        if (pickText != null) pickText.SetActive(false);
        if (hotbar != null) hotbar.SetActive(true);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);

        // HUD 자동 연결
        AutoConnectHUD();
    }

    void AutoConnectHUD()
    {
        // 비활성화된 오브젝트도 탐색
        var allTMP = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var tmp in allTMP)
        {
            switch (tmp.gameObject.name)
            {
                case "GoldText": if (goldText == null) goldText = tmp; break;
                case "TimerText": if (timerText == null) timerText = tmp; break;
                case "DecorScoreText": if (decorScoreText == null) decorScoreText = tmp; break;
                case "StatusMessageText": if (statusMessageText == null) statusMessageText = tmp; break;
            }
        }

        // StatusMessageText는 텍스트만 비워서 숨기기 (비활성화 X)
        if (statusMessageText != null)
        {
            statusMessageText.gameObject.SetActive(true);
            statusMessageText.text = "";
        }

        Debug.Log($"[UIManager] HUD 자동 연결 — Gold:{goldText != null} Timer:{timerText != null} Score:{decorScoreText != null} Status:{statusMessageText != null}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    // ─────────────────────────────────────────
    //  인벤토리 토글
    // ─────────────────────────────────────────
    void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(_isInventoryOpen);
        if (hotbar != null) hotbar.SetActive(!_isInventoryOpen);
    }

    // ─────────────────────────────────────────
    //  쓰레기 줍기 텍스트 관리
    // ─────────────────────────────────────────
    public void OnTrashEnter()
    {
        _nearbyTrashCount++;
        if (pickText != null)
            pickText.SetActive(true);
        Debug.Log($"[UIManager] OnTrashEnter — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashExit()
    {
        _nearbyTrashCount--;
        if (_nearbyTrashCount <= 0)
        {
            _nearbyTrashCount = 0;
            if (pickText != null)
                pickText.SetActive(false);
        }
        Debug.Log($"[UIManager] OnTrashExit — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashCollected()
    {
        _nearbyTrashCount = 0;
        if (pickText != null)
            pickText.SetActive(false);
    }


    // ─────────────────────────────────────────
    //  페이드 인/아웃 (텔레포트 연출용)
    //
    //  [씬 설정]
    //  Canvas 하위에 Image 오브젝트 생성
    //  이름: FadePanel, Color: 검정(0,0,0,0), RaycastTarget: false
    //  Anchor: Stretch (전체 화면 덮기)
    //  Inspector에서 fadePanel 슬롯에 연결
    // ─────────────────────────────────────────

    [Header("── 페이드 패널 ──")]
    [Tooltip("전체화면 검정 Image. Canvas 하위에 배치 후 연결")]
    public Image fadePanel;

    private Coroutine _fadeCoroutine;

    public void FadeOut(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, duration));
    }

    public void FadeIn(float duration)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, duration));
    }

    IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration)
    {
        if (fadePanel == null) yield break;

        fadePanel.gameObject.SetActive(true);

        float elapsed = 0f;
        Color c = fadePanel.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(fromAlpha, toAlpha, elapsed / duration);
            fadePanel.color = c;
            yield return null;
        }

        c.a = toAlpha;
        fadePanel.color = c;

        // 완전히 투명해지면 비활성화
        if (toAlpha <= 0f)
            fadePanel.gameObject.SetActive(false);
    }

    public void ShowStatusMessage(string message, float duration = 2f)
    {
        if (string.IsNullOrEmpty(message)) return;
        Debug.Log($"[UIManager] {message}");

        if (statusMessageText != null)
        {
            if (_statusCoroutine != null) StopCoroutine(_statusCoroutine);
            _statusCoroutine = StartCoroutine(ShowStatusRoutine(message, duration));
        }
    }

    private Coroutine _statusCoroutine;

    IEnumerator ShowStatusRoutine(string message, float duration)
    {
        statusMessageText.text = message;
        statusMessageText.gameObject.SetActive(true);
        yield return new WaitForSeconds(duration);
        statusMessageText.gameObject.SetActive(false);
        _statusCoroutine = null;
    }

    // ─────────────────────────────────────────
    //  HUD 갱신 (외부에서 호출)
    // ─────────────────────────────────────────

    public void UpdateGold(int gold)
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold:N0}G";
    }

    public void UpdateTimer(float elapsedSeconds)
    {
        if (timerText == null) return;
        int min = Mathf.FloorToInt(elapsedSeconds / 60f);
        int sec = Mathf.FloorToInt(elapsedSeconds % 60f);
        timerText.text = $"{min:00}:{sec:00}";
        timerText.color = Color.white;
    }

    public void UpdateDecorScore(int current, int goal)
    {
        if (decorScoreText != null)
            decorScoreText.text = $"꾸미기: {current}pt";
    }
}