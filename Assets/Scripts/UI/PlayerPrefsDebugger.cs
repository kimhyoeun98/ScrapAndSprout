using UnityEngine;

public class PlayerPrefsDebugger : MonoBehaviour
{
	[Header("디버그 도구")]
	[Tooltip("PlayerPrefs를 모두 삭제합니다")]
	public bool clearAllOnStart;

	private void Start()
	{
		if (clearAllOnStart)
		{
			ClearAllPlayerPrefs();
		}
		ShowCurrentTokenInfo();
	}

	public void ClearAllPlayerPrefs()
	{
		Debug.Log("[PlayerPrefs] 모든 데이터 삭제 중...");
		PlayerPrefs.DeleteAll();
		PlayerPrefs.Save();
		Debug.Log("[PlayerPrefs] 삭제 완료!");
	}

	public void ClearJWTToken()
	{
		Debug.Log("[PlayerPrefs] JWT 토큰 삭제 중...");
		PlayerPrefs.DeleteKey("jwt_token");
		PlayerPrefs.DeleteKey("player_id");
		PlayerPrefs.DeleteKey("user_name");
		PlayerPrefs.Save();
		Debug.Log("[PlayerPrefs] JWT 토큰 삭제 완료!");
	}

	public void ShowCurrentTokenInfo()
	{
		if (PlayerPrefs.HasKey("jwt_token"))
		{
			string text = PlayerPrefs.GetString("jwt_token");
			string text2 = PlayerPrefs.GetString("player_id", "test");
			string text3 = PlayerPrefs.GetString("user_name", "");
			Debug.Log("[PlayerPrefs] 저장된 토큰 정보:");
			Debug.Log("  - Token: " + text.Substring(0, Mathf.Min(20, text.Length)) + "...");
			Debug.Log("  - Player ID: " + text2);
			Debug.Log("  - User Name: " + text3);
		}
		else
		{
			Debug.Log("[PlayerPrefs] 저장된 토큰 없음");
		}
	}
}
