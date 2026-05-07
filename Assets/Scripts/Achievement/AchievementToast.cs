using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 업적 달성 시 화면에 표시되는 토스트 알림
/// </summary>
public class AchievementToast : MonoBehaviour
{
    // ==================== 싱글톤 ====================
    public static AchievementToast Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ==================== UI 요소 ====================
    [Header("UI 연결")]
    [Tooltip("토스트 패널 (애니메이션 대상)")]
    public GameObject toastPanel;
    
    [Tooltip("업적 이름 텍스트")]
    public TextMeshProUGUI achievementNameText;
    
    [Tooltip("업적 설명 텍스트")]
    public TextMeshProUGUI descriptionText;
    
    [Tooltip("업적 아이콘 (선택사항)")]
    public Image iconImage;
    
    // ==================== 애니메이션 설정 ====================
    [Header("애니메이션")]
    [Tooltip("표시 시간 (초)")]
    public float displayDuration = 3f;
    
    [Tooltip("페이드 인 시간 (초)")]
    public float fadeInDuration = 0.3f;
    
    [Tooltip("페이드 아웃 시간 (초)")]
    public float fadeOutDuration = 0.3f;
    
    [Tooltip("시작 위치 Y 오프셋")]
    public float startYOffset = 100f;
    
    // ==================== 내부 변수 ====================
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private Vector2 _originalPosition;
    private Coroutine _currentToastCoroutine;
    
    // ==================== Unity 생명주기 ====================
    
    void Start()
    {
        // 컴포넌트 가져오기
        if (toastPanel != null)
        {
            _canvasGroup = toastPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = toastPanel.AddComponent<CanvasGroup>();
            
            _rectTransform = toastPanel.GetComponent<RectTransform>();
            _originalPosition = _rectTransform.anchoredPosition;
        }
        
        // 시작 시 숨기기
        if (toastPanel != null)
            toastPanel.SetActive(false);
        
        // AchievementManager 이벤트 구독
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementUnlocked += ShowAchievement;
        }
        
        Debug.Log("[AchievementToast] 토스트 알림 시스템 준비 완료");
    }
    
    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.OnAchievementUnlocked -= ShowAchievement;
        }
    }
    
    // ==================== 토스트 표시 ====================
    
    /// <summary>
    /// 업적 달성 알림 표시
    /// </summary>
    public void ShowAchievement(AchievementData achievement)
    {
        // 이전 토스트가 실행 중이면 중단
        if (_currentToastCoroutine != null)
        {
            StopCoroutine(_currentToastCoroutine);
        }
        
        _currentToastCoroutine = StartCoroutine(ShowToastCoroutine(achievement));
    }
    
    /// <summary>
    /// 토스트 애니메이션 코루틴
    /// </summary>
    IEnumerator ShowToastCoroutine(AchievementData achievement)
    {
        // UI 설정
        if (achievementNameText != null)
            achievementNameText.text = achievement.achievementName;
        
        if (descriptionText != null)
            descriptionText.text = achievement.detail;
        
        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(false);
        }
        
        // 패널 활성화
        if (toastPanel != null)
            toastPanel.SetActive(true);
        
        // 초기 상태 설정
        _canvasGroup.alpha = 0f;
        _rectTransform.anchoredPosition = _originalPosition + new Vector2(0, startYOffset);
        
        // 페이드 인 + 슬라이드 다운
        float elapsedTime = 0f;
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeInDuration;
            
            // 알파 증가
            _canvasGroup.alpha = t;
            
            // 위치 이동 (Ease Out)
            float easedT = 1f - (1f - t) * (1f - t);
            _rectTransform.anchoredPosition = Vector2.Lerp(
                _originalPosition + new Vector2(0, startYOffset),
                _originalPosition,
                easedT
            );
            
            yield return null;
        }
        
        _canvasGroup.alpha = 1f;
        _rectTransform.anchoredPosition = _originalPosition;
        
        // 표시 유지
        yield return new WaitForSeconds(displayDuration);
        
        // 페이드 아웃
        elapsedTime = 0f;
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeOutDuration;
            
            _canvasGroup.alpha = 1f - t;
            
            yield return null;
        }
        
        _canvasGroup.alpha = 0f;
        
        // 패널 비활성화
        if (toastPanel != null)
            toastPanel.SetActive(false);
        
        _currentToastCoroutine = null;
    }
}
