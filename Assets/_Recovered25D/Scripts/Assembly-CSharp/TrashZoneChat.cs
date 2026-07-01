using System.Collections;
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;

public class TrashZoneChat : NetworkBehaviour
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
	private float messageFontSize = 9f;

	[SerializeField]
	private string defaultNickname = "Player";

	private readonly List<GameObject> _messageObjects = new List<GameObject>();

	public static bool IsTyping { get; private set; }

	private void Start()
	{
		sendButton?.onClick.AddListener(OnSendClicked);
		chatInputField?.onSubmit.AddListener(OnInputSubmit);
		SetupContentRect();
	}

	private void SetupContentRect()
	{
		if (!(contentRect == null))
		{
			contentRect.anchorMin = new Vector2(0f, 0f);
			contentRect.anchorMax = new Vector2(1f, 0f);
			contentRect.pivot = new Vector2(0.5f, 0f);
			contentRect.anchoredPosition = Vector2.zero;
			contentRect.sizeDelta = Vector2.zero;
			ContentSizeFitter contentSizeFitter = contentRect.GetComponent<ContentSizeFitter>();
			if (contentSizeFitter == null)
			{
				contentSizeFitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
			}
			contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			VerticalLayoutGroup verticalLayoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
			if (verticalLayoutGroup == null)
			{
				verticalLayoutGroup = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
			}
			verticalLayoutGroup.childControlWidth = true;
			verticalLayoutGroup.childControlHeight = true;
			verticalLayoutGroup.childForceExpandWidth = true;
			verticalLayoutGroup.spacing = 2f;
			verticalLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		}
	}

	private void OnDestroy()
	{
		sendButton?.onClick.RemoveListener(OnSendClicked);
		chatInputField?.onSubmit.RemoveListener(OnInputSubmit);
	}

	private void Update()
	{
		if (base.HasInputAuthority)
		{
			if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !IsTyping)
			{
				OpenInput();
			}
			if (Input.GetKeyDown(KeyCode.Escape) && IsTyping)
			{
				CloseInput();
			}
		}
	}

	private void OpenInput()
	{
		IsTyping = true;
		chatInputField?.ActivateInputField();
	}

	private void CloseInput()
	{
		IsTyping = false;
		if (chatInputField != null)
		{
			chatInputField.text = "";
			chatInputField.DeactivateInputField();
		}
	}

	private void OnSendClicked()
	{
		TrySend();
	}

	private void OnInputSubmit(string text)
	{
		if (!string.IsNullOrEmpty(text.Trim()))
		{
			TrySend();
		}
		else
		{
			CloseInput();
		}
	}

	private void TrySend()
	{
		if (!(chatInputField == null))
		{
			string text = chatInputField.text.Trim();
			if (!string.IsNullOrEmpty(text))
			{
				string nickname = GetNickname();
				chatInputField.text = "";
				RPC_BroadcastMessage(nickname, text);
				StartCoroutine(ReactivateInputField());
			}
		}
	}

	private IEnumerator ReactivateInputField()
	{
		yield return null;
		if (chatInputField != null && IsTyping)
		{
			chatInputField.ActivateInputField();
		}
	}

	[Rpc(RpcSources.All, RpcTargets.All)]
	public void RPC_BroadcastMessage(string nickname, string message)
	{

		GameObject gameObject = Resources.Load<GameObject>("ChatText");
		if (gameObject == null)
		{
			Debug.LogError("[TrashZoneChat] ChatText 프리팹 로드 실패 (Resources/ChatText)");
			return;
		}
		GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
		gameObject2.transform.SetParent(contentRect, worldPositionStays: false);
		TMP_Text component = gameObject2.GetComponent<TMP_Text>();
		if (component != null)
		{
			component.text = nickname + ": " + message;
			component.fontSize = messageFontSize;
			component.enableWordWrapping = true;
		}
		_messageObjects.Add(gameObject2);
		if (_messageObjects.Count > maxMessages)
		{
			UnityEngine.Object.Destroy(_messageObjects[0]);
			_messageObjects.RemoveAt(0);
		}
		StartCoroutine(ScrollToBottom());
	}

	private IEnumerator ScrollToBottom()
	{
		yield return new WaitForEndOfFrame();
		if (chatScrollRect != null)
		{
			chatScrollRect.verticalNormalizedPosition = 0f;
		}
	}

	private string GetNickname()
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
