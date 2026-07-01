using UnityEngine;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance;

	[Header("Audio Sources")]
	[SerializeField]
	private AudioSource _bgmSource;

	[SerializeField]
	private AudioSource _sfxSource;

	[SerializeField]
	private AudioSource _npcSource;

	[Header("BGM Clips")]
	[SerializeField]
	private AudioClip _lobbyBGM;

	[SerializeField]
	private AudioClip _gameBGM;

	[SerializeField]
	private AudioClip _resultBGM;

	[Header("SFX Clips")]
	[SerializeField]
	private AudioClip _trashCollectSFX;

	[SerializeField]
	private AudioClip _seedPlantSFX;

	[SerializeField]
	private AudioClip _npcTalkSFX;

	[SerializeField]
	private AudioClip _batteryChargeSFX;

	[SerializeField]
	private AudioClip _batteryLowSFX;

	[SerializeField]
	private AudioClip _buttonClickSFX;

	[SerializeField]
	private AudioClip _achievementSFX;

	[SerializeField]
	private AudioClip _weatherChangeSFX;

	[SerializeField]
	private AudioClip _gameOverSFX;

	[SerializeField]
	private AudioClip _gameClearSFX;

	[Header("Volume Settings")]
	[Range(0f, 1f)]
	[SerializeField]
	private float _bgmVolume = 0.7f;

	[Range(0f, 1f)]
	[SerializeField]
	private float _sfxVolume = 1f;

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			Object.DontDestroyOnLoad(base.gameObject);
			if (_bgmSource == null)
			{
				_bgmSource = base.gameObject.AddComponent<AudioSource>();
				_bgmSource.loop = true;
				_bgmSource.playOnAwake = false;
			}
			if (_sfxSource == null)
			{
				_sfxSource = base.gameObject.AddComponent<AudioSource>();
				_sfxSource.loop = false;
				_sfxSource.playOnAwake = false;
			}
			if (_npcSource == null)
			{
				_npcSource = base.gameObject.AddComponent<AudioSource>();
				_npcSource.loop = false;
				_npcSource.playOnAwake = false;
			}
			_bgmSource.volume = _bgmVolume;
			_sfxSource.volume = _sfxVolume;
			_npcSource.volume = _sfxVolume;
		}
		else
		{
			Object.Destroy(base.gameObject);
		}
	}

	public void PlayBGM(AudioClip clip)
	{
		if (!(clip == null) && (!(_bgmSource.clip == clip) || !_bgmSource.isPlaying))
		{
			_bgmSource.clip = clip;
			_bgmSource.Play();
		}
	}

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

	public void StopBGM()
	{
		_bgmSource.Stop();
	}

	public void SetBGMVolume(float volume)
	{
		_bgmVolume = Mathf.Clamp01(volume);
		_bgmSource.volume = _bgmVolume;
	}

	public void PlaySFX(AudioClip clip)
	{
		if (clip != null)
		{
			_sfxSource.PlayOneShot(clip);
		}
	}

	public void SetSFXVolume(float volume)
	{
		_sfxVolume = Mathf.Clamp01(volume);
		_sfxSource.volume = _sfxVolume;
		_npcSource.volume = _sfxVolume;
	}

	public void PlayTrashCollect()
	{
		PlaySFX(_trashCollectSFX);
	}

	public void PlaySeedPlant()
	{
		PlaySFX(_seedPlantSFX);
	}

	public void PlayBatteryCharge()
	{
		PlaySFX(_batteryChargeSFX);
	}

	public void PlayBatteryLow()
	{
		PlaySFX(_batteryLowSFX);
	}

	public void PlayButtonClick()
	{
		PlaySFX(_buttonClickSFX);
	}

	public void PlayAchievement()
	{
		PlaySFX(_achievementSFX);
	}

	public void PlayWeatherChange()
	{
		PlaySFX(_weatherChangeSFX);
	}

	public void PlayGameOver()
	{
		PlaySFX(_gameOverSFX);
	}

	public void PlayGameClear()
	{
		PlaySFX(_gameClearSFX);
	}

	public void PlayNPCTalk()
	{
		if (!(_npcTalkSFX == null))
		{
			_npcSource.clip = _npcTalkSFX;
			_npcSource.Play();
		}
	}

	public void StopNPCTalk()
	{
		_npcSource.Stop();
	}
}
