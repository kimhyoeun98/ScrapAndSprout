using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어별 꾸미기 구역 관리
///
/// SafeZone을 캐릭터 타입(알파/베타/감마/델타)별로 4구역 분할.
/// 각 구역의 경계와 이름을 관리.
///
/// [자동 설치]
/// DecorationPlacer.Start()에서 없으면 자동 생성.
/// </summary>
public class PlayerZoneManager : MonoBehaviour
{
    public static PlayerZoneManager Instance { get; private set; }

    [Header("── 구역 범위 ──")]
    [Tooltip("첫 번째 구역 시작 X (SafeZone 왼쪽 경계)")]
    public float zoneStartX = 3f;
    [Tooltip("구역 1개당 너비")]
    public float zoneWidth  = 12f;

    [Header("── 시각 마커 ──")]
    [Tooltip("바닥 마커 Y 중심")]
    public float groundY    = -5f;
    [Tooltip("바닥 마커 높이")]
    public float zoneHeight = 10f;

    // 구역별 이름 / 색상
    static readonly string[] _names = { "알파존", "베타존", "감마존", "델타존" };
    static readonly Color[]  _colors =
    {
        new Color(0.95f, 0.78f, 0.20f, 0.13f), // 알파: 금
        new Color(0.20f, 0.55f, 0.95f, 0.13f), // 베타: 파랑
        new Color(0.20f, 0.80f, 0.35f, 0.13f), // 감마: 초록
        new Color(0.75f, 0.25f, 0.80f, 0.13f), // 델타: 보라
    };

    // ─────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    // ─────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────

    /// <summary>슬롯 인덱스(=캐릭터타입 인덱스) → 구역 X 범위</summary>
    public (float min, float max) GetZoneBounds(int slot)
    {
        float min = zoneStartX + slot * zoneWidth;
        return (min, min + zoneWidth);
    }

    /// <summary>구역 이름 반환</summary>
    public string GetZoneName(int slot)
        => (slot >= 0 && slot < _names.Length) ? _names[slot] : $"플레이어{slot + 1}존";

    /// <summary>로컬 플레이어의 구역 슬롯 (캐릭터타입 인덱스)</summary>
    public int GetLocalSlot()
    {
        foreach (var tc in FindObjectsByType<TrashCollector>(FindObjectsSortMode.None))
            if (tc.HasInputAuthority) return (int)tc.characterType;
        return 0;
    }

    // ─────────────────────────────────────────
    //  DecoScene 전환
    // ─────────────────────────────────────────

    /// <summary>플레이어를 DecoScene으로 텔레포트 (Host만 가능)</summary>
    public void TeleportToDecoScene()
    {
        if (PhotonManager.Instance != null)
        {
            PhotonManager.Instance.LoadDecoScene();
        }
        else
        {
            // 로컬 테스트용 폴백
            DecoInventoryBridge.SaveFrom(
                FindFirstObjectByType<TrashCollector>());
            SceneManager.LoadScene("DecoScene");
        }
    }
}
