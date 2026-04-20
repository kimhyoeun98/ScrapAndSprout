using UnityEngine;

/// <summary>
/// PlayerPrefs에 저장된 로그인 정보를 초기화하는 유틸리티
/// </summary>
public class ClearLogin : MonoBehaviour
{
    // Unity 에디터 메뉴에서 실행
    [ContextMenu("로그인 정보 초기화")]
    void ClearLoginData()
    {
        // 1. JWT 토큰 삭제
        PlayerPrefs.DeleteKey("jwt_token");

        // 2. 저장된 ID 삭제 (있다면)
        PlayerPrefs.DeleteKey("saved_username");

        // 3. 자동 로그인 설정 삭제 (있다면)
        PlayerPrefs.DeleteKey("auto_login");

        // 4. 변경사항 즉시 저장
        PlayerPrefs.Save();

        Debug.Log("✅ 로그인 정보가 초기화되었습니다!");
        Debug.Log("게임을 재시작하면 로그인 화면으로 돌아갑니다.");
    }

    // 게임 실행 중 키보드로 초기화 (F12 키)
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            ClearLoginData();
        }
    }
}