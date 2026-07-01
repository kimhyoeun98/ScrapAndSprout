using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementToast : MonoBehaviour
{
	[Header("UI 연결")]
	[Tooltip("토스트 패널 (애니메이션 대상)")]
	public GameObject toastPanel;

	[Tooltip("업적 이름 텍스트")]
	public TextMeshProUGUI achievementNameText;

	[Tooltip("업적 설명 텍스트")]
	public TextMeshProUGUI descriptionText;

	[Tooltip("업적 아이콘 (선택사항)")]
	public Image iconImage;

	[Header("애니메이션")]
	[Tooltip("표시 시간 (초)")]
	public float displayDuration = 3f;

	[Tooltip("페이드 인 시간 (초)")]
	public float fadeInDuration = 0.3f;

	[Tooltip("페이드 아웃 시간 (초)")]
	public float fadeOutDuration = 0.3f;

	[Tooltip("시작 위치 Y 오프셋")]
	public float startYOffset = 100f;

	private CanvasGroup _canvasGroup;

	private RectTransform _rectTransform;

	private Vector2 _originalPosition;

	private Coroutine _currentToastCoroutine;

	public static AchievementToast Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Start()
	{
		if (toastPanel != null)
		{
			_canvasGroup = toastPanel.GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
			{
				_canvasGroup = toastPanel.AddComponent<CanvasGroup>();
			}
			_rectTransform = toastPanel.GetComponent<RectTransform>();
			_originalPosition = _rectTransform.anchoredPosition;
		}
		if (toastPanel != null)
		{
			toastPanel.SetActive(value: false);
		}
		if (AchievementManager.Instance != null)
		{
			AchievementManager.Instance.OnAchievementUnlocked += ShowAchievement;
		}
		Debug.Log("[AchievementToast] 토스트 알림 시스템 준비 완료");
	}

	private void OnDestroy()
	{
		if (AchievementManager.Instance != null)
		{
			AchievementManager.Instance.OnAchievementUnlocked -= ShowAchievement;
		}
	}

	public void ShowAchievement(AchievementData achievement)
	{
		if (_currentToastCoroutine != null)
		{
			StopCoroutine(_currentToastCoroutine);
		}
		_currentToastCoroutine = StartCoroutine(ShowToastCoroutine(achievement));
	}

	private IEnumerator ShowToastCoroutine(AchievementData achievement)
	{
		if (achievementNameText != null)
		{
			achievementNameText.text = achievement.achievementName;
		}
		if (descriptionText != null)
		{
			descriptionText.text = achievement.detail;
		}
		if (iconImage != null)
		{
			iconImage.gameObject.SetActive(value: false);
		}
		if (toastPanel != null)
		{
			toastPanel.SetActive(value: true);
		}
		_canvasGroup.alpha = 0f;
		_rectTransform.anchoredPosition = _originalPosition + new Vector2(0f, startYOffset);
		float elapsedTime = 0f;
		while (elapsedTime < fadeInDuration)
		{
			elapsedTime += Time.deltaTime;
			float num = elapsedTime / fadeInDuration;
			_canvasGroup.alpha = num;
			float t = 1f - (1f - num) * (1f - num);
			_rectTransform.anchoredPosition = Vector2.Lerp(_originalPosition + new Vector2(0f, startYOffset), _originalPosition, t);
			yield return null;
		}
		_canvasGroup.alpha = 1f;
		_rectTransform.anchoredPosition = _originalPosition;
		yield return new WaitForSeconds(displayDuration);
		elapsedTime = 0f;
		while (elapsedTime < fadeOutDuration)
		{
			elapsedTime += Time.deltaTime;
			float num2 = elapsedTime / fadeOutDuration;
			_canvasGroup.alpha = 1f - num2;
			yield return null;
		}
		_canvasGroup.alpha = 0f;
		if (toastPanel != null)
		{
			toastPanel.SetActive(value: false);
		}
		_currentToastCoroutine = null;
	}
}
