using UnityEngine;

public class ClearLogin : MonoBehaviour
{
	[ContextMenu("로그인 정보 초기화")]
	private void ClearLoginData()
	{
		PlayerPrefs.DeleteKey("jwt_token");
		PlayerPrefs.DeleteKey("saved_username");
		PlayerPrefs.DeleteKey("auto_login");
		PlayerPrefs.Save();
		Debug.Log("✅ 로그인 정보가 초기화되었습니다!");
		Debug.Log("게임을 재시작하면 로그인 화면으로 돌아갑니다.");
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F12))
		{
			ClearLoginData();
		}
	}
}
