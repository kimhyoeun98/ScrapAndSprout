using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerZoneManager : MonoBehaviour
{
	[Header("── 구역 범위 ──")]
	[Tooltip("첫 번째 구역 시작 X (SafeZone 왼쪽 경계)")]
	public float zoneStartX = 3f;

	[Tooltip("구역 1개당 너비")]
	public float zoneWidth = 12f;

	[Header("── 시각 마커 ──")]
	[Tooltip("바닥 마커 Y 중심")]
	public float groundY = -5f;

	[Tooltip("바닥 마커 높이")]
	public float zoneHeight = 10f;

	private static readonly string[] _names = new string[4] { "알파존", "베타존", "감마존", "델타존" };

	private static readonly Color[] _colors = new Color[4]
	{
		new Color(0.95f, 0.78f, 0.2f, 0.13f),
		new Color(0.2f, 0.55f, 0.95f, 0.13f),
		new Color(0.2f, 0.8f, 0.35f, 0.13f),
		new Color(0.75f, 0.25f, 0.8f, 0.13f)
	};

	public static PlayerZoneManager Instance { get; private set; }

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

	public (float min, float max) GetZoneBounds(int slot)
	{
		float num = zoneStartX + (float)slot * zoneWidth;
		return (min: num, max: num + zoneWidth);
	}

	public string GetZoneName(int slot)
	{
		if (slot < 0 || slot >= _names.Length)
		{
			return $"플레이어{slot + 1}존";
		}
		return _names[slot];
	}

	public int GetLocalSlot()
	{
		TrashCollector[] array = Object.FindObjectsByType<TrashCollector>(FindObjectsSortMode.None);
		foreach (TrashCollector trashCollector in array)
		{
			if (trashCollector.HasInputAuthority)
			{
				return (int)trashCollector.characterType;
			}
		}
		return 0;
	}

	public void TeleportToDecoScene()
	{
		if (PhotonManager.Instance != null)
		{
			PhotonManager.Instance.LoadDecoScene();
			return;
		}
		DecoInventoryBridge.SaveFrom(Object.FindFirstObjectByType<TrashCollector>());
		SceneManager.LoadScene("DecoScene");
	}
}
