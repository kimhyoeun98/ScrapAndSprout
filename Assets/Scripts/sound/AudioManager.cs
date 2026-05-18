using UnityEngine;

/// <summary>
/// 게임 전체 오디오 관리 (BGM + SFX)
/// 싱글톤 패턴으로 씬 전환 시에도 유지됨
///
/// [AudioSource 구조]
/// _bgmSource : 루프 BGM 전용
/// _sfxSource : 일반 SFX 전용 (PlayOneShot — 중단 불필요한 짧은 효과음)
/// _npcSource : NPC 대사 전용 (Play/Stop — 패널 닫을 때 중단 가능)
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _npcSource; // ✅ NPC 대사 전용 (Stop 가능)

    [Header("BGM Clips")]
    [SerializeField] private AudioClip _lobbyBGM;
    [SerializeField] private AudioClip _gameBGM;
    [SerializeField] private AudioClip _resultBGM;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip _trashCollectSFX;
    [SerializeField] private AudioClip _seedPlantSFX;
    [SerializeField] private AudioClip _npcTalkSFX;
    [SerializeField] private AudioClip _batteryChargeSFX;
    [SerializeField] private AudioClip _batteryLowSFX;
    [SerializeField] private AudioClip _buttonClickSFX;
    [SerializeField] private AudioClip _achievementSFX;
    [SerializeField] private AudioClip _weatherChangeSFX;
    [SerializeField] private AudioClip _gameOverSFX;
    [SerializeField] private AudioClip _gameClearSFX;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _bgmVolume = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 1f;

    private void Awake()
    {
        // 싱글톤 패턴
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // BGM AudioSource 초기화
        if (_bgmSource == null)
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
        }

        // 일반 SFX AudioSource 초기화
        if (_sfxSource == null)
        {
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
        }

        // ✅ NPC 전용 AudioSource 초기화
        if (_npcSource == null)
        {
            _npcSource = gameObject.AddComponent<AudioSource>();
            _npcSource.loop = false;
            _npcSource.playOnAwake = false;
        }

        // 볼륨 적용
        _bgmSource.volume = _bgmVolume;
        _sfxSource.volume = _sfxVolume;
        _npcSource.volume = _sfxVolume;
    }

    #region BGM Methods

    /// <summary>
    /// BGM 재생
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // 이미 같은 BGM이 재생 중이면 무시
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    /// <summary>
    /// 씬 이름으로 BGM 재생
    /// </summary>
    public void PlayBGMForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "LobbyScene":
            case "WaitingRoomScene":
                PlayBGM(_lobbyBGM);
                break;
            case "TrashZoneScene":
            case "TutorialScene":
                PlayBGM(_gameBGM);
                break;
            case "ResultScene":
                PlayBGM(_resultBGM);
                break;
        }
    }

    /// <summary>
    /// BGM 중지
    /// </summary>
    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    /// <summary>
    /// BGM 볼륨 조절
    /// </summary>
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        _bgmSource.volume = _bgmVolume;
    }

    #endregion

    #region SFX Methods

    /// <summary>
    /// 일반 SFX 재생 (PlayOneShot — 짧은 효과음용, 중단 불필요)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
            _sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// SFX 볼륨 조절
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        _sfxSource.volume = _sfxVolume;
        _npcSource.volume = _sfxVolume;
    }

    // ── 일반 SFX 편의 메서드 (PlayOneShot 방식) ──
    public void PlayTrashCollect() => PlaySFX(_trashCollectSFX);
    public void PlaySeedPlant() => PlaySFX(_seedPlantSFX);
    public void PlayBatteryCharge() => PlaySFX(_batteryChargeSFX);
    public void PlayBatteryLow() => PlaySFX(_batteryLowSFX);
    public void PlayButtonClick() => PlaySFX(_buttonClickSFX);
    public void PlayAchievement() => PlaySFX(_achievementSFX);
    public void PlayWeatherChange() => PlaySFX(_weatherChangeSFX);
    public void PlayGameOver() => PlaySFX(_gameOverSFX);
    public void PlayGameClear() => PlaySFX(_gameClearSFX);

    // ── NPC 대사 전용 (Play/Stop 방식 — 패널 닫을 때 중단 가능) ──

    /// <summary>
    /// NPC 대사 재생.
    /// PlayOneShot 대신 Play() 사용 → StopNPCTalk()으로 중단 가능
    /// </summary>
    public void PlayNPCTalk()
    {
        if (_npcTalkSFX == null) return;

        _npcSource.clip = _npcTalkSFX;
        _npcSource.Play();
    }

    /// <summary>
    /// NPC 대사 중지. 패널 닫을 때 호출하세요.
    /// </summary>
    public void StopNPCTalk()
    {
        _npcSource.Stop();
    }

    #endregion
}