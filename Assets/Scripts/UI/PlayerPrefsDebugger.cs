using UnityEngine;

/// <summary>
/// PlayerPrefs 초기화 유틸리티
/// 
/// [사용법]
/// 1. 빈 GameObject에 부착
/// 2. Play 모드 실행
/// 3. Inspector에서 "Clear All PlayerPrefs" 버튼 클릭
/// 4. Play 모드 중지
/// </summary>
public class PlayerPrefsDebugger : MonoBehaviour
{
    [Header("디버그 도구")]
    [Tooltip("PlayerPrefs를 모두 삭제합니다")]
    public bool clearAllOnStart = false;

    void Start()
    {
        if (clearAllOnStart)
        {
            ClearAllPlayerPrefs();
        }

        // 저장된 토큰 확인
        ShowCurrentTokenInfo();
    }

    /// <summary>
    /// 모든 PlayerPrefs 삭제
    /// </summary>
    public void ClearAllPlayerPrefs()
    {
        Debug.Log("[PlayerPrefs] 모든 데이터 삭제 중...");

        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("[PlayerPrefs] 삭제 완료!");
    }

    /// <summary>
    /// JWT 토큰만 삭제
    /// </summary>
    public void ClearJWTToken()
    {
        Debug.Log("[PlayerPrefs] JWT 토큰 삭제 중...");

        PlayerPrefs.DeleteKey("jwt_token");
        PlayerPrefs.DeleteKey("player_id");
        PlayerPrefs.DeleteKey("user_name");
        PlayerPrefs.Save();

        Debug.Log("[PlayerPrefs] JWT 토큰 삭제 완료!");
    }

    /// <summary>
    /// 현재 저장된 토큰 정보 표시
    /// </summary>
    public void ShowCurrentTokenInfo()
    {
        if (PlayerPrefs.HasKey("jwt_token"))
        {
            string token = PlayerPrefs.GetString("jwt_token");
            int playerId = PlayerPrefs.GetInt("player_id", 0);
            string userName = PlayerPrefs.GetString("user_name", "");

            Debug.Log($"[PlayerPrefs] 저장된 토큰 정보:");
            Debug.Log($"  - Token: {token.Substring(0, Mathf.Min(20, token.Length))}...");
            Debug.Log($"  - Player ID: {playerId}");
            Debug.Log($"  - User Name: {userName}");
        }
        else
        {
            Debug.Log("[PlayerPrefs] 저장된 토큰 없음");
        }
    }
}