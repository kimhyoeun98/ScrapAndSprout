using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class WaitingRoomChat : NetworkBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private ScrollRect chatScrollRect;
    [SerializeField] private RectTransform contentRect; // ✅ Content 오브젝트 연결

    [Header("설정")]
    [SerializeField] private int maxMessages = 50;
    [SerializeField] private string defaultNickname = "Player";

    // ✅ chatLogText 대신 프리팹 방식으로 변경
    private List<GameObject> messageObjects = new List<GameObject>();

    void Start()
    {
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (chatInputField != null)
            chatInputField.onSubmit.AddListener(OnInputFieldSubmit);

        Debug.Log("[채팅] WaitingRoomChat 초기화 완료");
    }

    void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnSendButtonClicked);

        if (chatInputField != null)
            chatInputField.onSubmit.RemoveListener(OnInputFieldSubmit);
    }

    void OnSendButtonClicked()
    {
        SendChatMessage();
    }

    void OnInputFieldSubmit(string text)
    {
        if (string.IsNullOrEmpty(text.Trim())) return;
        SendChatMessage();
    }

    void SendChatMessage()
    {
        if (chatInputField == null) return;

        string message = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        string nickname = GetPlayerNickname();

        chatInputField.text = "";

        RPC_BroadcastMessage(nickname, message);

        StartCoroutine(ReactivateInputField());

        Debug.Log($"[Chat] Send - sender:{nickname} message:{message}");
    }

    IEnumerator ReactivateInputField()
    {
        yield return null;
        if (chatInputField != null)
            chatInputField.ActivateInputField();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_BroadcastMessage(string nickname, string message)
    {
        Debug.Log($"[Chat] Receive - sender:{nickname} message:{message}");

        // ✅ 프리팹 로드 및 생성
        GameObject textPrefab = Resources.Load<GameObject>("ChatText");
        if (textPrefab == null)
        {
            Debug.LogError("[Chat] ChatText 프리팹 로드 실패!");
            return;
        }

        // ✅ 프리팹 생성 후 Content 하위에 추가
        GameObject newMessage = Instantiate(textPrefab);
        newMessage.transform.SetParent(contentRect, false);

        // ✅ 텍스트 설정
        TMP_Text tmpText = newMessage.GetComponent<TMP_Text>();
        if (tmpText != null)
            tmpText.text = $"{nickname}: {message}";

        // ✅ 메시지 리스트에 추가
        messageObjects.Add(newMessage);

        // ✅ 최대 메시지 수 초과 시 오래된 메시지 삭제
        if (messageObjects.Count > maxMessages)
        {
            Destroy(messageObjects[0]);
            messageObjects.RemoveAt(0);
        }

        // ✅ 자동 스크롤
        StartCoroutine(ScrollToBottom());

        Debug.Log($"[Chat] messageCount:{messageObjects.Count}");
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;
    }

    string GetPlayerNickname()
    {
        // ✅ 수정: PhotonManager 메모리에서 먼저 읽기
        if (PhotonManager.Instance != null && !string.IsNullOrEmpty(PhotonManager.Instance.LocalPlayerName))
            return PhotonManager.Instance.LocalPlayerName;

        // 폴백: PlayerPrefs
        string nickname = PlayerPrefs.GetString("user_name", "");
        if (!string.IsNullOrEmpty(nickname))
            return nickname;

        if (Runner != null)
            return $"Player{Runner.LocalPlayer.PlayerId}";

        return defaultNickname;
    }
}