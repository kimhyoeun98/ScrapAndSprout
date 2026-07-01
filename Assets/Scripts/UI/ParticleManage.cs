using UnityEngine;

public class ParticleManager : MonoBehaviour
{
	public static ParticleManager Instance;

	[Header("Gameplay Particles")]
	[SerializeField]
	private ParticleSystem _trashCollectParticle;

	[SerializeField]
	private ParticleSystem _seedPlantParticle;

	[SerializeField]
	private ParticleSystem _batteryChargeParticle;

	[Header("Weather Particles")]
	[SerializeField]
	private ParticleSystem _acidRainParticle;

	[SerializeField]
	private ParticleSystem _dustStormParticle;

	[Header("Settings")]
	[SerializeField]
	private float _particleLifetime = 1f;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			Object.DontDestroyOnLoad(base.gameObject);
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	public void PlayTrashCollect(Vector3 position)
	{
		PlayParticle(_trashCollectParticle, position);
	}

	public void PlaySeedPlant(Vector3 position)
	{
		PlayParticle(_seedPlantParticle, position);
	}

	public void PlayBatteryCharge(Vector3 position)
	{
		PlayParticle(_batteryChargeParticle, position);
	}

	public void SetAcidRain(bool active)
	{
		if (_acidRainParticle != null)
		{
			if (active && !_acidRainParticle.isPlaying)
			{
				_acidRainParticle.Play();
			}
			else if (!active && _acidRainParticle.isPlaying)
			{
				_acidRainParticle.Stop();
			}
		}
	}

	public void SetDustStorm(bool active)
	{
		if (_dustStormParticle != null)
		{
			if (active && !_dustStormParticle.isPlaying)
			{
				_dustStormParticle.Play();
			}
			else if (!active && _dustStormParticle.isPlaying)
			{
				_dustStormParticle.Stop();
			}
		}
	}

	public void ClearWeather()
	{
		SetAcidRain(active: false);
		SetDustStorm(active: false);
	}

	private void PlayParticle(ParticleSystem particlePrefab, Vector3 position)
	{
		if (particlePrefab == null)
		{
			Debug.LogWarning("파티클 프리팹이 할당되지 않았습니다!");
		}
		else
		{
			Object.Destroy(Object.Instantiate(particlePrefab, position, Quaternion.identity).gameObject, _particleLifetime);
		}
	}
}
