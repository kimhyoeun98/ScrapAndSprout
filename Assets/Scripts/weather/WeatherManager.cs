using System;
using System.Collections;
using Fusion;
using UnityEngine;

public class WeatherManager : NetworkBehaviour
{
	public enum WeatherType
	{
		Clear,
		AcidRain,
		YellowDust
	}

	private WeatherType _prevWeather;

	[Header("날씨 변경 설정")]
	[Tooltip("날씨가 바뀌는 주기 (초)")]
	public float weatherChangeInterval = 60f;

	[Tooltip("기상 이벤트 기본 발생 확률 (0~100%)")]
	[Range(0f, 100f)]
	public float badWeatherBaseChance = 30f;

	private int _placedTreeCount;

	[Header("배터리 소모 배율")]
	[Tooltip("맑은 날 배터리 배율")]
	public float clearBatteryMultiplier = 1f;

	[Tooltip("산성비 날 배터리 배율")]
	public float acidRainBatteryMultiplier = 1.5f;

	[Tooltip("황사 날 배터리 배율")]
	public float yellowDustBatteryMultiplier = 1.2f;

	[Header("비주얼 효과")]
	[Tooltip("비 파티클 프리팹 (런타임에 카메라 자식으로 인스턴스화됨)")]
	public ParticleSystem rainParticles;

	[Tooltip("황사 파티클 프리팹 (런타임에 카메라 자식으로 인스턴스화됨)")]
	public ParticleSystem dustParticles;

	private ParticleSystem _rainInstance;

	private ParticleSystem _dustInstance;

	[Tooltip("황사 시 Fog 사용 여부")]
	public bool useFog = true;

	[Tooltip("황사 시 Fog 색상")]
	public Color dustFogColor = new Color(0.8f, 0.7f, 0.5f);

	[Tooltip("황사 시 Fog 농도")]
	public float dustFogDensity = 0.02f;

	private float _nextWeatherChangeTime;

	private Color _originalFogColor;

	private float _originalFogDensity;

	public static WeatherManager Instance { get; private set; }

	[Networked]
	public WeatherType CurrentWeather { get; set; }

	public float BadWeatherChance => Mathf.Max(0f, badWeatherBaseChance - (float)_placedTreeCount);

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	public void AddTree()
	{
		_placedTreeCount++;
		Debug.Log($"[WeatherManager] 나무 {_placedTreeCount}개 — 기상 이벤트 확률: {BadWeatherChance:F0}%");
	}

	public override void Spawned()
	{
		_originalFogColor = RenderSettings.fogColor;
		_originalFogDensity = RenderSettings.fogDensity;
		_nextWeatherChangeTime = Time.time + weatherChangeInterval;
		_prevWeather = CurrentWeather;
		ApplyWeather(CurrentWeather);
		StartCoroutine(DelayedWeatherUI(CurrentWeather));
		Debug.Log($"[WeatherManager] Spawned — 현재 날씨: {CurrentWeather}, IsServer:{base.Runner.IsServer}");
	}

	private IEnumerator DelayedWeatherUI(WeatherType weather)
	{
		yield return new WaitForSeconds(3f);
		UIManager.Instance?.UpdateWeather(weather);
	}

	public override void FixedUpdateNetwork()
	{
		if (base.HasStateAuthority && Time.time >= _nextWeatherChangeTime)
		{
			ChangeWeatherByPollution();
			_nextWeatherChangeTime = Time.time + weatherChangeInterval;
		}
	}

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

	private void ChangeWeatherByPollution()
	{
		float badWeatherChance = BadWeatherChance;
		WeatherType weather = ((!(UnityEngine.Random.Range(0f, 100f) >= badWeatherChance)) ? ((UnityEngine.Random.Range(0f, 100f) < 70f) ? WeatherType.AcidRain : WeatherType.YellowDust) : WeatherType.Clear);
		SetWeather(weather);
	}

	public void SetWeather(WeatherType weather)
	{
		if (CurrentWeather == weather)
		{
			Debug.Log($"[WeatherManager] 날씨 변화 없음: {weather}");
			return;
		}
		CurrentWeather = weather;
		Debug.Log($"[WeatherManager] SetWeather: {weather} (기상 확률: {BadWeatherChance:F0}%)");
	}

	private void ApplyWeather(WeatherType weather)
	{
		StopAllWeatherEffects();
		switch (weather)
		{
		case WeatherType.AcidRain:
		{
			ParticleSystem orCreateInstance2 = GetOrCreateInstance(rainParticles, ref _rainInstance);
			if (orCreateInstance2 != null)
			{
				orCreateInstance2.Play();
			}
			break;
		}
		case WeatherType.YellowDust:
		{
			ParticleSystem orCreateInstance = GetOrCreateInstance(dustParticles, ref _dustInstance);
			if (orCreateInstance != null)
			{
				orCreateInstance.Play();
			}
			if (useFog)
			{
				RenderSettings.fog = true;
				RenderSettings.fogColor = dustFogColor;
				RenderSettings.fogDensity = dustFogDensity;
			}
			break;
		}
		}
		UIManager.Instance?.UpdateWeather(weather);
	}

	private ParticleSystem GetOrCreateInstance(ParticleSystem prefab, ref ParticleSystem instance)
	{
		if (instance != null)
		{
			return instance;
		}
		if (prefab == null)
		{
			return null;
		}
		instance = UnityEngine.Object.Instantiate(prefab);
		instance.name = prefab.name + "_Active";
		Camera main = Camera.main;
		if (main != null)
		{
			instance.transform.SetParent(main.transform, worldPositionStays: false);
			float y = (main.orthographic ? (main.orthographicSize + 2f) : 10f);
			instance.transform.localPosition = new Vector3(0f, y, 5f);
			instance.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
		}
		ParticleSystemRenderer[] componentsInChildren = instance.GetComponentsInChildren<ParticleSystemRenderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].sortingOrder = 1000;
		}
		instance.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		return instance;
	}

	private void StopAllWeatherEffects()
	{
		if (_rainInstance != null)
		{
			_rainInstance.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
		if (_dustInstance != null)
		{
			_dustInstance.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
		}
		if (useFog)
		{
			RenderSettings.fog = false;
			RenderSettings.fogColor = _originalFogColor;
			RenderSettings.fogDensity = _originalFogDensity;
		}
	}

	private void Update()
	{
		if (base.HasStateAuthority && !(base.Object == null) && base.Object.IsValid && Input.GetKeyDown(KeyCode.P))
		{
			WeatherType weatherType = CurrentWeather switch
			{
				WeatherType.Clear => WeatherType.AcidRain, 
				WeatherType.AcidRain => WeatherType.YellowDust, 
				_ => WeatherType.Clear, 
			};
			SetWeather(weatherType);
			Debug.Log($"[WeatherManager] (디버그) P키 → {weatherType}");
		}
	}

	public float GetBatteryMultiplier()
	{
		if (base.Object == null || !base.Object.IsValid)
		{
			return 1f;
		}
		return CurrentWeather switch
		{
			WeatherType.Clear => clearBatteryMultiplier, 
			WeatherType.AcidRain => acidRainBatteryMultiplier, 
			WeatherType.YellowDust => yellowDustBatteryMultiplier, 
			_ => 1f, 
		};
	}

	public bool IsClear()
	{
		if (base.Object == null || !base.Object.IsValid)
		{
			return true;
		}
		return CurrentWeather == WeatherType.Clear;
	}

	public bool IsAcidRain()
	{
		if (base.Object == null || !base.Object.IsValid)
		{
			return false;
		}
		return CurrentWeather == WeatherType.AcidRain;
	}

	public bool IsYellowDust()
	{
		if (base.Object == null || !base.Object.IsValid)
		{
			return false;
		}
		return CurrentWeather == WeatherType.YellowDust;
	}

	public string GetWeatherName()
	{
		if (base.Object == null || !base.Object.IsValid)
		{
			return "맑음";
		}
		return CurrentWeather switch
		{
			WeatherType.Clear => "맑음", 
			WeatherType.AcidRain => "산성비", 
			WeatherType.YellowDust => "황사", 
			_ => "알 수 없음", 
		};
	}

	public void UpdatePollutionLevel(float pollutionLevel)
	{
	}

}
