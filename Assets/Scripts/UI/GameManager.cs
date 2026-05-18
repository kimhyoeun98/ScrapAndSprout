using UnityEngine;
using TMPro;
using Fusion;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    [Header("── HUD UI 연결 ──")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI decorScoreText;

    [Networked] private float _elapsedTime { get; set; }
    [Networked] public int DecorScore { get; set; }

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        _elapsedTime = 0f;
        DecorScore = 0;
        Debug.Log("[GameManager] 게임 시작!");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        _elapsedTime += Runner.DeltaTime;
    }

    void Update()
    {
        if (Runner == null) return;
        UpdateTimerUI();
        UpdateDecorScoreUI();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddDecorScore(int amount)
    {
        if (!HasStateAuthority) return;
        DecorScore += amount;
        Debug.Log($"[GameManager] 꾸미기 +{amount}pt → 총 {DecorScore}pt");
    }

    // 하위 호환용
    public void OnTrashCollected() { }
    public void OnTreePlanted() { }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddCollectedTrash(int count) { }

    void UpdateTimerUI()
    {
        if (timerText == null) return;
        int min = Mathf.FloorToInt(_elapsedTime / 60f);
        int sec = Mathf.FloorToInt(_elapsedTime % 60f);
        timerText.text = $"{min:00}:{sec:00}";
    }

    void UpdateDecorScoreUI()
    {
        if (decorScoreText != null)
            decorScoreText.text = $"꾸미기: {DecorScore}pt";
        UIManager.Instance?.UpdateDecorScore(DecorScore, 0);
    }
}