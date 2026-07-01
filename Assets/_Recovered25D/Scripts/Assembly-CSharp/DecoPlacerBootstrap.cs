using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DecoPlacerBootstrap : MonoBehaviour
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Init()
	{
		GameObject obj = new GameObject("DecoPlacerBootstrap");
		obj.AddComponent<DecoPlacerBootstrap>();
		Object.DontDestroyOnLoad(obj);
	}

	private void Awake()
	{
		StartCoroutine(Watch());
	}

	private IEnumerator Watch()
	{
		while (true)
		{
			if (SceneManager.GetActiveScene().name == "TrashZoneScene" && Object.FindFirstObjectByType<DecorationPlacer>() == null)
			{
				CreatePlacer();
			}
			yield return new WaitForSeconds(1f);
		}
	}

	private void CreatePlacer()
	{
		GameObject gameObject = GameObject.Find("SafeZone_Entrance");
		Vector3 vector;
		if (gameObject != null)
		{
			vector = gameObject.transform.position;
		}
		else
		{
			GameObject gameObject2 = GameObject.Find("Tilemap_safe");
			vector = ((gameObject2 != null) ? gameObject2.transform.position : new Vector3(-30f, -7f, 0f));
		}
		vector.z = 0f;
		GameObject obj = new GameObject("DecorationPlacer (Auto)");
		obj.transform.position = vector;
		obj.AddComponent<DecorationPlacer>().interactionRadius = 25f;
		Debug.Log($"[DecoPlacerBootstrap] 안전존에 DecorationPlacer 자동 생성 완료 (pos={vector})");
	}
}
