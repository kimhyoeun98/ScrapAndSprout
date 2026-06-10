using UnityEngine;
using System.Collections;
using Fusion;

/// <summary>
/// 게임 내 날씨 시스템 관리 (Photon Fusion 2 네트워크 버전)
/// Host가 날씨를 결정하고 [Networked]로 모든 클라이언트에 자동 동기화
/// </summary>
public class WeatherManager : NetworkBehaviour
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

    // ==================== [Networked] 현재 날씨 ====================
    /// <summary>
    /// Host가 값을 바꾸면 Fusion이 모든 클라이언트에 자동 동기화
    /// Render()에서 _prevWeather와 비교해 변경 감지 후 시각 효과 적용
    /// </summary>
    [Networked]
    public WeatherType CurrentWeather { get; set; } = WeatherType.Clear;

    // 이전 날씨 저장 — Render()에서 변경 감지용
    private WeatherType _prevWeather = WeatherType.Clear;

    // ==================== 날씨 변경 설정 ====================
    [Header("날씨 변경 설정")]
    [Tooltip("날씨가 바뀌는 주기 (초)")]
    public float weatherChangeInterval = 60f;

    [Tooltip("기상 이벤트 기본 발생 확률 (0~100%)")]
    [Range(0f, 100f)]
    public float badWeatherBaseChance = 30f;

    private int _placedTreeCount = 0;

    public float BadWeatherChance =>
        Mathf.Max(0f, badWeatherBaseChance - _placedTreeCount);

    /// <summary>
    /// 나무 배치 시 호출 — Host에서만 호출해야 함
    /// </summary>
    public void AddTree()
    {
        _placedTreeCount++;
        Debug.Log($"[WeatherManager] 나무 {_placedTreeCount}개 — 기상 이벤트 확률: {BadWeatherChance:F0}%");
    }

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
    public Color dustFogColor = new Color(0.8f, 0.7f, 0.5f);

    [Tooltip("황사 시 Fog 농도")]
    public float dustFogDensity = 0.02f;

    // ==================== 내부 변수 ====================
    private float _nextWeatherChangeTime;
    private Color _originalFogColor;
    private float _originalFogDensity;

    // ==================== Fusion 생명주기 ====================

    /// <summary>
    /// 씬에 배치된 NetworkObject가 네트워크에 합류했을 때 호출
    /// Host만 날씨 타이머를 구동하고, 클라이언트는 현재 날씨 시각 효과만 적용
    /// </summary>
    //public override void Spawned()
    //{
    //    _originalFogColor = RenderSettings.fogColor;
    //    _originalFogDensity = RenderSettings.fogDensity;
    //    _nextWeatherChangeTime = Time.time + weatherChangeInterval;

    //    // 접속 시점의 현재 날씨 즉시 적용 (클라이언트 뒤늦게 접속해도 동기화)
    //    _prevWeather = CurrentWeather;
    //    ApplyWeather(CurrentWeather);

    //    Debug.Log($"[WeatherManager] Spawned — 현재 날씨: {CurrentWeather}, IsServer:{Runner.IsServer}");
    //}
    public override void Spawned()
    {
        _originalFogColor = RenderSettings.fogColor;
        _originalFogDensity = RenderSettings.fogDensity;
        _nextWeatherChangeTime = Time.time + weatherChangeInterval;

        _prevWeather = CurrentWeather;
        ApplyWeather(CurrentWeather);

        // UIManager 초기화 대기 후 날씨 UI 갱신
        StartCoroutine(DelayedWeatherUI(CurrentWeather));

        Debug.Log($"[WeatherManager] Spawned — 현재 날씨: {CurrentWeather}, IsServer:{Runner.IsServer}");
    }

    IEnumerator DelayedWeatherUI(WeatherType weather)
    {
        yield return new WaitForSeconds(3f); // MinimapUI와 동일하게 대기
        UIManager.Instance?.UpdateWeather(weather);
    }

    /// <summary>
    /// 날씨 타이머는 Host(StateAuthority)에서만 구동
    /// 클라이언트는 Render()에서 변경 감지 후 자동 반영
    /// </summary>
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Time.time >= _nextWeatherChangeTime)
        {
            ChangeWeatherByPollution();
            _nextWeatherChangeTime = Time.time + weatherChangeInterval;
        }
    }

    /// <summary>
    /// 매 렌더 프레임마다 날씨 변경 감지 — Host / 클라이언트 모두 실행
    /// [Networked] CurrentWeather가 바뀌었을 때 시각 효과 적용
    /// </summary>
    public override void Render()
    {
        if (CurrentWeather != _prevWeather)
        {
            _prevWeather = CurrentWeather;
            ApplyWeather(CurrentWeather);
            AudioManager.Instance?.PlayWeatherChange();
            Debug.Log($"[WeatherManager] 날씨 변경 감지 → {CurrentWeather}");
        }
    }

    // ==================== 날씨 변경 로직 ====================

    /// <summary>
    /// 나무 배치 수 기반으로 날씨를 확률적으로 변경 (Host 전용)
    /// </summary>
    void ChangeWeatherByPollution()
    {
        float chance = BadWeatherChance;
        float roll = Random.Range(0f, 100f);

        WeatherType newWeather;
        if (roll >= chance)
        {
            newWeather = WeatherType.Clear;
        }
        else
        {
            // 악천후 내에서 산성비 70% / 황사 30%
            newWeather = Random.Range(0f, 100f) < 70f
                ? WeatherType.AcidRain
                : WeatherType.YellowDust;
        }

        SetWeather(newWeather);
    }

    /// <summary>
    /// 날씨 강제 설정 — Host에서만 호출해야 함
    /// CurrentWeather 변경 → Fusion이 클라이언트에 자동 동기화 → Render()에서 감지
    /// </summary>
    public void SetWeather(WeatherType weather)
    {
        if (CurrentWeather == weather)
        {
            Debug.Log($"[WeatherManager] 날씨 변화 없음: {weather}");
            return;
        }

        CurrentWeather = weather; // [Networked] 변경 → 자동 동기화
        Debug.Log($"[WeatherManager] SetWeather: {weather} (기상 확률: {BadWeatherChance:F0}%)");
    }

    // ==================== 시각 효과 적용 ====================

    /// <summary>
    /// 파티클 / Fog 효과 로컬 적용
    /// Render() 또는 Spawned()에서 호출 — 네트워크 전송 없이 각자 실행
    /// </summary>
    void ApplyWeather(WeatherType weather)
    {
        StopAllWeatherEffects();

        switch (weather)
        {
            case WeatherType.Clear:
                break;

            case WeatherType.AcidRain:
                if (rainParticles != null)
                    rainParticles.Play();
                break;

            case WeatherType.YellowDust:
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

        UIManager.Instance?.UpdateWeather(weather);
    }

    /// <summary>
    /// 모든 날씨 효과 초기화
    /// </summary>
    void StopAllWeatherEffects()
    {
        if (rainParticles != null) rainParticles.Stop();
        if (dustParticles != null) dustParticles.Stop();

        if (useFog)
        {
            RenderSettings.fog = false;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
        }
    }

    // ==================== 배터리 배율 ====================

    /// <summary>
    /// 현재 날씨에 따른 배터리 소모 배율 반환
    /// Spawned() 이전 접근 시 기본값 1.0f 반환 (InvalidOperationException 방지)
    /// </summary>
    public float GetBatteryMultiplier()
    {
        if (Object == null || !Object.IsValid) return 1.0f;
        switch (CurrentWeather)
        {
            case WeatherType.Clear: return clearBatteryMultiplier;
            case WeatherType.AcidRain: return acidRainBatteryMultiplier;
            case WeatherType.YellowDust: return yellowDustBatteryMultiplier;
            default: return 1.0f;
        }
    }

    // ==================== 외부 참조용 ====================

    /// <summary>
    /// 현재 날씨가 맑은지 확인
    /// Spawned() 이전 접근 시 true(맑음) 반환
    /// </summary>
    public bool IsClear()
    {
        if (Object == null || !Object.IsValid) return true;
        return CurrentWeather == WeatherType.Clear;
    }

    /// <summary>
    /// 현재 날씨가 산성비인지 확인
    /// Spawned() 이전 접근 시 false 반환
    /// </summary>
    public bool IsAcidRain()
    {
        if (Object == null || !Object.IsValid) return false;
        return CurrentWeather == WeatherType.AcidRain;
    }

    /// <summary>
    /// 현재 날씨가 황사인지 확인
    /// Spawned() 이전 접근 시 false 반환
    /// </summary>
    public bool IsYellowDust()
    {
        if (Object == null || !Object.IsValid) return false;
        return CurrentWeather == WeatherType.YellowDust;
    }

    /// <summary>
    /// 현재 날씨를 한글로 반환
    /// Spawned() 이전 접근 시 "맑음" 반환
    /// </summary>
    public string GetWeatherName()
    {
        if (Object == null || !Object.IsValid) return "맑음";
        switch (CurrentWeather)
        {
            case WeatherType.Clear: return "맑음";
            case WeatherType.AcidRain: return "산성비";
            case WeatherType.YellowDust: return "황사";
            default: return "알 수 없음";
        }
    }

    // ==================== 구버전 호환 ====================
    public void UpdatePollutionLevel(float pollutionLevel) { }
}