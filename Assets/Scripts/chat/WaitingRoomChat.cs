using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// WaitingRoom 전용 채팅 시스템
/// Photon Fusion RPC로 메시지 동기화
/// </summary>
public class WaitingRoomChat : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  UI 연결
    // ─────────────────────────────────────────
    [Header("UI 연결")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private TextMeshProUGUI chatLogText;
    [SerializeField] private ScrollRect chatScrollRect;  // ✅ 추가

    // ─────────────────────────────────────────
    //  설정
    // ─────────────────────────────────────────
    [Header("설정")]
    [SerializeField] private int maxMessages = 50;  // 최대 메시지 수
    [SerializeField] private string defaultNickname = "Player";

    private List<string> messageList = new List<string>();

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        // Send 버튼 클릭 이벤트
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        // InputField Enter 키 이벤트
        if (chatInputField != null)
        {
            chatInputField.onSubmit.AddListener(OnInputFieldSubmit);
        }

        // 초기 채팅 로그 비우기
        if (chatLogText != null)
        {
            chatLogText.text = "";
        }

        Debug.Log("[채팅] WaitingRoomChat 초기화 완료");
    }

    void OnDestroy()
    {
        // 이벤트 정리
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(OnSendButtonClicked);
        }

        if (chatInputField != null)
        {
            chatInputField.onSubmit.RemoveListener(OnInputFieldSubmit);
        }
    }

    // ─────────────────────────────────────────
    //  메시지 전송
    // ─────────────────────────────────────────

    /// <summary>
    /// Send 버튼 클릭 시
    /// </summary>
    void OnSendButtonClicked()
    {
        SendMessage();
    }

    /// <summary>
    /// InputField에서 Enter 누를 시
    /// </summary>
    void OnInputFieldSubmit(string text)
    {
        SendMessage();  // ✅ ActivateInputField 제거
    }

    /// <summary>
    /// 메시지 전송 처리
    /// </summary>
    void SendMessage()
    {
        if (chatInputField == null) return;

        string message = chatInputField.text.Trim();

        // 빈 메시지 체크
        if (string.IsNullOrEmpty(message))
        {
            Debug.Log("[채팅] 빈 메시지는 전송하지 않음");
            return;
        }

        // 닉네임 가져오기
        string nickname = GetPlayerNickname();

        // RPC로 모든 클라이언트에 전송
        RPC_BroadcastMessage(nickname, message);

        // InputField 초기화
        chatInputField.text = "";

        // ✅ 코루틴으로 포커스 재설정
        StartCoroutine(ReactivateInputField());

        Debug.Log($"[채팅] 메시지 전송: {nickname}: {message}");
    }

    // ✅ 새로운 함수 추가
    IEnumerator ReactivateInputField()
    {
        yield return null;  // 1프레임 대기
        if (chatInputField != null)
        {
            chatInputField.ActivateInputField();
        }
    }

    // ─────────────────────────────────────────
    //  RPC - 메시지 동기화
    // ─────────────────────────────────────────

    /// <summary>
    /// 모든 클라이언트에게 메시지 브로드캐스트
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_BroadcastMessage(string nickname, string message)
    {
        // 메시지 포맷: "닉네임: 내용"
        string formattedMessage = $"{nickname}: {message}";

        // 메시지 리스트에 추가
        messageList.Add(formattedMessage);

        // 최대 메시지 수 제한
        if (messageList.Count > maxMessages)
        {
            messageList.RemoveAt(0);
        }

        // UI 갱신
        UpdateChatLog();

        Debug.Log($"[채팅] 메시지 수신: {formattedMessage}");
    }

    // ─────────────────────────────────────────
    //  UI 갱신
    // ─────────────────────────────────────────

    /// <summary>
    /// 채팅 로그 텍스트 갱신
    /// </summary>
    void UpdateChatLog()
    {
        if (chatLogText == null) return;

        // 모든 메시지를 줄바꿈으로 연결
        chatLogText.text = string.Join("\n", messageList);

        // ✅ 최신 메시지로 자동 스크롤
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;  // 0 = 맨 아래
        }
    }

    // ─────────────────────────────────────────
    //  Helper 함수
    // ─────────────────────────────────────────

    /// <summary>
    /// 플레이어 ID 가져오기
    /// </summary>
    string GetPlayerNickname()
    {
        if (Runner != null)
        {
            int playerId = Runner.LocalPlayer.PlayerId;
            return $"Player{playerId + 1}";
        }

        return "Player";
    }
}