using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameIntroOverlay : MonoBehaviour
{
	private static bool _hasShownThisSession;

	[Header("UI 연결")]
	[SerializeField]
	private GameObject overlayPanel;

	[SerializeField]
	private Button confirmButton;

	private bool _ready;

	public static void ResetForNewGame()
	{
		_hasShownThisSession = false;
	}

	private void Start()
	{
		if (_hasShownThisSession)
		{
			overlayPanel?.SetActive(value: false);
			return;
		}
		overlayPanel?.SetActive(value: true);
		confirmButton?.onClick.AddListener(Close);
		_hasShownThisSession = true;
		StartCoroutine(EnableInputNextFrame());
	}

	private IEnumerator EnableInputNextFrame()
	{
		yield return null;
		_ready = true;
	}

	private void Update()
	{
		if (_ready && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
		{
			Close();
		}
	}

	private void Close()
	{
		overlayPanel?.SetActive(value: false);
		_ready = false;
	}
}
