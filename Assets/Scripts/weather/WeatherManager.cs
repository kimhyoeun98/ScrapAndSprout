using UnityEngine;
using System.Collections;

/// <summary>
/// 게임 내 날씨 시스템 관리
/// 오염도 기반으로 날씨를 변경하고, 배터리 소모 배율을 제공합니다.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    // ==================== 싱글톤 ====================
    public static WeatherManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ==================== 날씨 종류 ====================
    public enum WeatherType
    {
        Clear,        // 맑음
        AcidRain,     // 산성비
        YellowDust    // 황사
    }

    // ==================== 현재 날씨 ====================
    [Header("현재 날씨")]
    public WeatherType currentWeather = WeatherType.Clear;
    
    // ==================== 날씨 변경 설정 ====================
    [Header("날씨 변경 설정")]
    [Tooltip("날씨가 바뀌는 주기 (초)")]
    public float weatherChangeInterval = 60f;  // 60초마다 날씨 체크
    
    [Tooltip("현재 오염도 (0~100)")]
    [Range(0f, 100f)]
    public float currentPollutionLevel = 50f;
    
    // ==================== 배터리 소모 배율 ====================
    [Header("배터리 소모 배율")]
    [Tooltip("맑은 날 배터리 배율")]
    public float clearBatteryMultiplier = 1.0f;
    
    [Tooltip("산성비 날 배터리 배율")]
    public float acidRainBatteryMultiplier = 1.5f;
    
    [Tooltip("황사 날 배터리 배율")]
    public float yellowDustBatteryMultiplier = 1.2f;
    
    // ==================== 파티클 시스템 ====================
    [Header("비주얼 효과")]
    [Tooltip("비 파티클 시스템")]
    public ParticleSystem rainParticles;
    
    [Tooltip("황사 파티클 시스템")]
    public ParticleSystem dustParticles;
    
    // ==================== Fog 효과 ====================
    [Tooltip("황사 시 Fog 사용 여부")]
    public bool useFog = true;
    
    [Tooltip("황사 시 Fog 색상")]
    public Color dustFogColor = new Color(0.8f, 0.7f, 0.5f);  // 황토색
    
    [Tooltip("황사 시 Fog 농도")]
    public float dustFogDensity = 0.02f;
    
    // ==================== 내부 변수 ====================
    private float _nextWeatherChangeTime;
    private Color _originalFogColor;
    private float _originalFogDensity;
    
    // ==================== Unity 생명주기 ====================
    void Start()
    {
        // 원본 Fog 설정 저장
        _originalFogColor = RenderSettings.fogColor;
        _originalFogDensity = RenderSettings.fogDensity;
        
        // 첫 날씨 변경 시간 설정
        _nextWeatherChangeTime = Time.time + weatherChangeInterval;
        
        // 시작 시 날씨 적용
        ApplyWeather(currentWeather);
        
        Debug.Log($"[WeatherManager] 시작 - 현재 날씨: {currentWeather}");
    }
    
    void Update()
    {
        // 일정 시간마다 날씨 변경
        if (Time.time >= _nextWeatherChangeTime)
        {
            ChangeWeatherByPollution();
            _nextWeatherChangeTime = Time.time + weatherChangeInterval;
        }
    }
    
    // ==================== 날씨 변경 로직 ====================
    
    /// <summary>
    /// 오염도에 따라 날씨를 확률적으로 변경합니다
    /// </summary>
    void ChangeWeatherByPollution()
    {
        // 오염도 기반 확률 계산
        // 오염도 0% → 맑음 100%
        // 오염도 50% → 맑음 50%, 산성비 30%, 황사 20%
        // 오염도 100% → 맑음 10%, 산성비 60%, 황사 30%
        
        float randomValue = Random.Range(0f, 100f);
        WeatherType newWeather;
        
        if (currentPollutionLevel <= 30f)
        {
            // 저오염 (0~30%): 대부분 맑음
            if (randomValue < 80f)
                newWeather = WeatherType.Clear;
            else if (randomValue < 95f)
                newWeather = WeatherType.AcidRain;
            else
                newWeather = WeatherType.YellowDust;
        }
        else if (currentPollutionLevel <= 70f)
        {
            // 중오염 (30~70%): 골고루 분포
            if (randomValue < 50f)
                newWeather = WeatherType.Clear;
            else if (randomValue < 80f)
                newWeather = WeatherType.AcidRain;
            else
                newWeather = WeatherType.YellowDust;
        }
        else
        {
            // 고오염 (70~100%): 악천후 많음
            if (randomValue < 20f)
                newWeather = WeatherType.Clear;
            else if (randomValue < 70f)
                newWeather = WeatherType.AcidRain;
            else
                newWeather = WeatherType.YellowDust;
        }
        
        SetWeather(newWeather);
    }
    
    /// <summary>
    /// 날씨를 강제로 설정합니다
    /// </summary>
    /// <param name="weather">설정할 날씨</param>
    public void SetWeather(WeatherType weather)
    {
        if (currentWeather == weather)
        {
            Debug.Log($"[WeatherManager] 날씨 변화 없음: {weather}");
            return;
        }
        
        currentWeather = weather;
        ApplyWeather(weather);
        
        Debug.Log($"[WeatherManager] 날씨 변경: {weather} (오염도: {currentPollutionLevel}%)");
    }
    
    /// <summary>
    /// 날씨 효과를 실제로 적용합니다 (파티클, Fog 등)
    /// </summary>
    void ApplyWeather(WeatherType weather)
    {
        // 모든 효과 초기화
        StopAllWeatherEffects();
        
        switch (weather)
        {
            case WeatherType.Clear:
                // 맑음: 모든 효과 끔
                break;
                
            case WeatherType.AcidRain:
                // 산성비: 비 파티클 켜기
                if (rainParticles != null)
                    rainParticles.Play();
                break;
                
            case WeatherType.YellowDust:
                // 황사: 황사 파티클 + Fog
                if (dustParticles != null)
                    dustParticles.Play();
                
                if (useFog)
                {
                    RenderSettings.fog = true;
                    RenderSettings.fogColor = dustFogColor;
                    RenderSettings.fogDensity = dustFogDensity;
                }
                break;
        }
    }
    
    /// <summary>
    /// 모든 날씨 효과를 멈춥니다
    /// </summary>
    void StopAllWeatherEffects()
    {
        // 파티클 정지
        if (rainParticles != null)
            rainParticles.Stop();
        
        if (dustParticles != null)
            dustParticles.Stop();
        
        // Fog 원래대로
        if (useFog)
        {
            RenderSettings.fog = false;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
        }
    }
    
    // ==================== 배터리 배율 ====================
    
    /// <summary>
    /// 현재 날씨에 따른 배터리 소모 배율을 반환합니다
    /// </summary>
    /// <returns>배터리 배율 (1.0 = 기본, 1.5 = 1.5배 소모)</returns>
    public float GetBatteryMultiplier()
    {
        switch (currentWeather)
        {
            case WeatherType.Clear:
                return clearBatteryMultiplier;
            
            case WeatherType.AcidRain:
                return acidRainBatteryMultiplier;
            
            case WeatherType.YellowDust:
                return yellowDustBatteryMultiplier;
            
            default:
                return 1.0f;
        }
    }
    
    // ==================== 오염도 업데이트 ====================
    
    /// <summary>
    /// 오염도를 업데이트합니다 (나무 심기/쓰레기 수거 시 호출)
    /// </summary>
    /// <param name="pollutionLevel">새로운 오염도 (0~100)</param>
    public void UpdatePollutionLevel(float pollutionLevel)
    {
        currentPollutionLevel = Mathf.Clamp(pollutionLevel, 0f, 100f);
        Debug.Log($"[WeatherManager] 오염도 업데이트: {currentPollutionLevel}%");
    }
    
    // ==================== 외부 참조용 ====================
    
    /// <summary>
    /// 현재 날씨가 맑은지 확인
    /// </summary>
    public bool IsClear()
    {
        return currentWeather == WeatherType.Clear;
    }
    
    /// <summary>
    /// 현재 날씨가 산성비인지 확인
    /// </summary>
    public bool IsAcidRain()
    {
        return currentWeather == WeatherType.AcidRain;
    }
    
    /// <summary>
    /// 현재 날씨가 황사인지 확인
    /// </summary>
    public bool IsYellowDust()
    {
        return currentWeather == WeatherType.YellowDust;
    }
    
    /// <summary>
    /// 현재 날씨를 한글로 반환
    /// </summary>
    public string GetWeatherName()
    {
        switch (currentWeather)
        {
            case WeatherType.Clear:
                return "맑음";
            case WeatherType.AcidRain:
                return "산성비";
            case WeatherType.YellowDust:
                return "황사";
            default:
                return "알 수 없음";
        }
    }
}
