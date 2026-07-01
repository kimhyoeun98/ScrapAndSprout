using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class WaitingRoomChat : NetworkBehaviour
{
	[Header("UI 연결")]
	[SerializeField]
	private TMP_InputField chatInputField;

	[SerializeField]
	private Button sendButton;

	[SerializeField]
	private ScrollRect chatScrollRect;

	[SerializeField]
	private RectTransform contentRect;

	[Header("설정")]
	[SerializeField]
	private int maxMessages = 50;

	[SerializeField]
	private string defaultNickname = "Player";

	private List<GameObject> messageObjects = new List<GameObject>();

	private void Start()
	{
		if (sendButton != null)
		{
			sendButton.onClick.AddListener(OnSendButtonClicked);
		}
		if (chatInputField != null)
		{
			chatInputField.onSubmit.AddListener(OnInputFieldSubmit);
		}
		Debug.Log("[채팅] WaitingRoomChat 초기화 완료");
	}

	private void OnDestroy()
	{
		if (sendButton != null)
		{
			sendButton.onClick.RemoveListener(OnSendButtonClicked);
		}
		if (chatInputField != null)
		{
			chatInputField.onSubmit.RemoveListener(OnInputFieldSubmit);
		}
	}

	private void OnSendButtonClicked()
	{
		SendChatMessage();
	}

	private void OnInputFieldSubmit(string text)
	{
		if (!string.IsNullOrEmpty(text.Trim()))
		{
			SendChatMessage();
		}
	}

	private void SendChatMessage()
	{
		if (!(chatInputField == null))
		{
			string text = chatInputField.text.Trim();
			if (!string.IsNullOrEmpty(text))
			{
				string playerNickname = GetPlayerNickname();
				chatInputField.text = "";
				RPC_BroadcastMessage(playerNickname, text);
				StartCoroutine(ReactivateInputField());
				Debug.Log("[Chat] Send - sender:" + playerNickname + " message:" + text);
			}
		}
	}

	private IEnumerator ReactivateInputField()
	{
		yield return null;
		if (chatInputField != null)
		{
			chatInputField.ActivateInputField();
		}
	}

	[Rpc(RpcSources.All, RpcTargets.All)]
	public void RPC_BroadcastMessage(string nickname, string message)
	{

		Debug.Log("[Chat] Receive - sender:" + nickname + " message:" + message);
		GameObject gameObject = Resources.Load<GameObject>("ChatText");
		if (gameObject == null)
		{
			Debug.LogError("[Chat] ChatText 프리팹 로드 실패!");
			return;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
		gameObject2.transform.SetParent(contentRect, worldPositionStays: false);
		TMP_Text component = gameObject2.GetComponent<TMP_Text>();
		if (component != null)
		{
			component.text = nickname + ": " + message;
		}
		messageObjects.Add(gameObject2);
		if (messageObjects.Count > maxMessages)
		{
			UnityEngine.Object.Destroy(messageObjects[0]);
			messageObjects.RemoveAt(0);
		}
		StartCoroutine(ScrollToBottom());
		Debug.Log($"[Chat] messageCount:{messageObjects.Count}");
	}

	private IEnumerator ScrollToBottom()
	{
		yield return new WaitForEndOfFrame();
		if (chatScrollRect != null)
		{
			chatScrollRect.verticalNormalizedPosition = 0f;
		}
	}

	private string GetPlayerNickname()
	{
		if (PhotonManager.Instance != null && !string.IsNullOrEmpty(PhotonManager.Instance.LocalPlayerName))
		{
			return PhotonManager.Instance.LocalPlayerName;
		}
		string text = PlayerPrefs.GetString("user_name", "");
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		if (base.Runner != null)
		{
			return $"Player{base.Runner.LocalPlayer.PlayerId}";
		}
		return defaultNickname;
	}

}
