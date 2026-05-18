using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 클릭 SFX 자동 연결 컴포넌트
///
/// [사용법]
/// SFX가 필요한 Button 오브젝트에 Add Component → ButtonSFX
/// AudioManager.Instance가 DontDestroyOnLoad로 유지되므로 씬 상관없이 동작
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour
{
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(
            () => AudioManager.Instance?.PlayButtonClick()
        );
    }
}