using UnityEngine;

/// <summary>
/// 게임 전체 파티클 효과 관리
/// 5종: 쓰레기 수거, 씨앗 식재, 배터리 충전, 산성비, 황사
/// </summary>
public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance;

    [Header("Gameplay Particles")]
    [SerializeField] private ParticleSystem _trashCollectParticle;  // 쓰레기 수거
    [SerializeField] private ParticleSystem _seedPlantParticle;     // 씨앗 식재
    [SerializeField] private ParticleSystem _batteryChargeParticle; // 배터리 충전

    [Header("Weather Particles")]
    [SerializeField] private ParticleSystem _acidRainParticle;      // 산성비
    [SerializeField] private ParticleSystem _dustStormParticle;     // 황사

    [Header("Settings")]
    [SerializeField] private float _particleLifetime = 1f;

    private void Awake()
    {
        // 싱글톤
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 쓰레기 수거 파티클 재생
    /// </summary>
    public void PlayTrashCollect(Vector3 position)
    {
        PlayParticle(_trashCollectParticle, position);
    }

    /// <summary>
    /// 씨앗 식재 파티클 재생
    /// </summary>
    public void PlaySeedPlant(Vector3 position)
    {
        PlayParticle(_seedPlantParticle, position);
    }

    /// <summary>
    /// 배터리 충전 파티클 재생
    /// </summary>
    public void PlayBatteryCharge(Vector3 position)
    {
        PlayParticle(_batteryChargeParticle, position);
    }

    /// <summary>
    /// 산성비 활성화/비활성화
    /// </summary>
    public void SetAcidRain(bool active)
    {
        if (_acidRainParticle != null)
        {
            if (active && !_acidRainParticle.isPlaying)
                _acidRainParticle.Play();
            else if (!active && _acidRainParticle.isPlaying)
                _acidRainParticle.Stop();
        }
    }

    /// <summary>
    /// 황사 활성화/비활성화
    /// </summary>
    public void SetDustStorm(bool active)
    {
        if (_dustStormParticle != null)
        {
            if (active && !_dustStormParticle.isPlaying)
                _dustStormParticle.Play();
            else if (!active && _dustStormParticle.isPlaying)
                _dustStormParticle.Stop();
        }
    }

    /// <summary>
    /// 모든 날씨 파티클 끄기
    /// </summary>
    public void ClearWeather()
    {
        SetAcidRain(false);
        SetDustStorm(false);
    }

    /// <summary>
    /// 파티클 재생 (한 번만)
    /// </summary>
    private void PlayParticle(ParticleSystem particlePrefab, Vector3 position)
    {
        if (particlePrefab == null)
        {
            Debug.LogWarning("파티클 프리팹이 할당되지 않았습니다!");
            return;
        }

        // 파티클 인스턴스 생성
        ParticleSystem particle = Instantiate(particlePrefab, position, Quaternion.identity);

        // 일정 시간 후 자동 삭제
        Destroy(particle.gameObject, _particleLifetime);
    }
}