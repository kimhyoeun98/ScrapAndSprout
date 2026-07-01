using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class ApiManager : MonoBehaviour
{
	[Header("서버 설정")]
	public string serverBaseUrl = "http://172.31.51.36:8080";

	public string fastApiBaseUrl = "http://172.31.51.36:8000";

	private string _jwtToken = "";

	private string _playerId = "";

	public static ApiManager Instance { get; private set; }

	public string playerId
	{
		get
		{
			return _playerId;
		}
		private set
		{
			if (_playerId != value)
			{
				Debug.Log("[ApiManager] playerId 변경: '" + _playerId + "' → '" + value + "'");
			}
			_playerId = value;
		}
	}

	public string userName { get; private set; } = "";

	public bool IsLoggedIn => !string.IsNullOrEmpty(_jwtToken);

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
			LoadTokenFromPlayerPrefs();
		}
		else
		{
			UnityEngine.Object.Destroy(base.gameObject);
		}
	}

	private void LoadTokenFromPlayerPrefs()
	{
		if (PlayerPrefs.HasKey("jwt_token"))
		{
			_jwtToken = PlayerPrefs.GetString("jwt_token");
			string text = PlayerPrefs.GetString("player_id", "");
			Debug.Log("[Auth] PlayerPrefs 로드 - playerId: '" + text + "'");
			playerId = text;
			userName = PlayerPrefs.GetString("user_name", "");
			Debug.Log("[Auth] 저장된 토큰 로드 완료 → Player: " + userName + " (ID: " + playerId + ")");
		}
	}

	private void SaveTokenToPlayerPrefs(string token, string id, string name)
	{
		_jwtToken = token;
		playerId = id;
		userName = name;
		PlayerPrefs.SetString("jwt_token", token);
		PlayerPrefs.SetString("player_id", id);
		PlayerPrefs.SetString("user_name", name);
		PlayerPrefs.Save();
		Debug.Log("[Auth] 토큰 저장 완료 → " + userName + " (ID: " + playerId + ")");
	}

	public void Logout()
	{
		PlayerPrefs.DeleteKey("jwt_token");
		PlayerPrefs.Save();
		_jwtToken = "";
		playerId = "";
		Debug.Log("로그아웃 완료!");
		SceneManager.LoadScene("LoginScene");
	}

	public void Login(LoginRequest request, Action<LoginResponse> onSuccess, Action<string> onFail)
	{
		StartCoroutine(PostRequest("/api/auth/login", JsonUtility.ToJson(request), delegate(LoginResponse response)
		{
			if (string.IsNullOrEmpty(response.token))
			{
				Debug.LogWarning("[Auth] 토큰 없음 — 로그인 실패");
				onFail?.Invoke("아이디 또는 비밀번호가 틀렸습니다.");
			}
			else
			{
				SaveTokenToPlayerPrefs(response.token, response.user_id, response.user_name);
				onSuccess?.Invoke(response);
			}
		}, onFail));
	}

	public void SignUp(SignUpRequest request, Action<SignUpResponse> onSuccess, Action<string> onFail)
	{
		StartCoroutine(PostRequest("/api/signup", JsonUtility.ToJson(request), onSuccess, onFail));
	}

	public void CheckIdDuplicate(string username, Action<CheckIdResponse> onSuccess, Action<string> onFail)
	{
		string endpoint = "/api/checkid?username=" + UnityWebRequest.EscapeURL(username);
		StartCoroutine(GetRequest(endpoint, onSuccess, onFail));
	}

	public void SellTrash(TrashSellRequest request, Action<TradeResponse> onSuccess, Action<string> onFail)
	{
		StartCoroutine(PostRequest("/api/trade/sell", JsonUtility.ToJson(request), onSuccess, onFail));
	}

	public void BuyItem(BuyRequest request, Action<TradeResponse> onSuccess, Action<string> onFail)
	{
		if (SceneManager.GetActiveScene().name == "TutorialScene")
		{
			Debug.Log($"[ApiManager] TutorialScene — 구매 API 우회, 즉시 성공 처리: {request.itemName} x{request.quantity}");
			onSuccess?.Invoke(new TradeResponse
			{
				success = true,
				gold = 0,
				message = "튜토리얼 구매 (서버 미반영)"
			});
		}
		else
		{
			StartCoroutine(PostRequest("/api/trade/buy", JsonUtility.ToJson(request), onSuccess, onFail));
		}
	}

	public void PlaceDecoration(DecoPlaceRequest request, Action<DecoPlaceResponse> onSuccess, Action<string> onFail)
	{
		StartCoroutine(PostRequest("/api/deco/place", JsonUtility.ToJson(request), onSuccess, onFail));
	}

	public void PlantSeed(PlantRequest request, Action<PlantResponse> onSuccess, Action<string> onFail)
	{
		Debug.LogWarning("[ApiManager] PlantSeed는 더 이상 사용되지 않습니다. PlaceDecoration을 사용하세요.");
		onFail?.Invoke("PlantSeed deprecated");
	}

	public void GetPlayerInfo(Action<PlayerInfoResponse> onSuccess, Action<string> onFail)
	{
		if (string.IsNullOrEmpty(playerId))
		{
			Debug.LogError("[API] playerId가 비어있음!");
			onFail?.Invoke("로그인이 필요합니다.");
		}
		else
		{
			string text = "/api/player/" + playerId;
			Debug.Log("[API] 요청 URL: " + serverBaseUrl + text);
			StartCoroutine(GetRequest(text, onSuccess, onFail));
		}
	}

	public void CallFastAPI(string endpoint, string jsonBody, Action<string> onSuccess, Action<string> onFail)
	{
		StartCoroutine(FastAPIRequest(endpoint, jsonBody, onSuccess, onFail));
	}

	private IEnumerator FastAPIRequest(string endpoint, string jsonBody, Action<string> onSuccess, Action<string> onFail)
	{
		string text = fastApiBaseUrl + endpoint;
		UnityWebRequest request = new UnityWebRequest(text, "POST");
		byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
		request.uploadHandler = new UploadHandlerRaw(bytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		Debug.Log("[FastAPI] POST " + text + " → " + jsonBody);
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			string text2 = request.downloadHandler.text;
			Debug.Log("[FastAPI] 응답 성공: " + text2);
			onSuccess?.Invoke(text2);
		}
		else
		{
			Debug.LogWarning("[FastAPI] 오류: " + request.error);
			onFail?.Invoke(request.error);
		}
		request.Dispose();
	}

	private IEnumerator PostRequest<T>(string endpoint, string jsonBody, Action<T> onSuccess, Action<string> onFail)
	{
		string text = serverBaseUrl + endpoint;
		UnityWebRequest request = new UnityWebRequest(text, "POST");
		byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
		request.uploadHandler = new UploadHandlerRaw(bytes);
		request.downloadHandler = new DownloadHandlerBuffer();
		request.SetRequestHeader("Content-Type", "application/json");
		if (!string.IsNullOrEmpty(_jwtToken))
		{
			request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);
			Debug.Log("[API] JWT 토큰 헤더 추가됨");
		}
		Debug.Log("[API] POST " + text + " → " + jsonBody);
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			string text2 = request.downloadHandler.text;
			Debug.Log("[API] 응답 성공: " + text2);
			T obj = JsonUtility.FromJson<T>(text2);
			onSuccess?.Invoke(obj);
		}
		else
		{
			string text3 = "[API] 오류: " + request.error;
			if (request.responseCode == 401)
			{
				Debug.LogWarning("[Auth] 토큰 만료! 다시 로그인 필요");
				Logout();
			}
			Debug.LogWarning(text3);
			onFail?.Invoke(text3);
		}
		request.Dispose();
	}

	private IEnumerator GetRequest<T>(string endpoint, Action<T> onSuccess, Action<string> onFail)
	{
		string text = serverBaseUrl + endpoint;
		UnityWebRequest request = UnityWebRequest.Get(text);
		if (!string.IsNullOrEmpty(_jwtToken))
		{
			request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);
		}
		Debug.Log("[API] GET " + text);
		yield return request.SendWebRequest();
		if (request.result == UnityWebRequest.Result.Success)
		{
			string text2 = request.downloadHandler.text;
			Debug.Log("[API] 응답 성공: " + text2);
			T obj = JsonUtility.FromJson<T>(text2);
			onSuccess?.Invoke(obj);
		}
		else
		{
			if (request.responseCode == 401)
			{
				Debug.LogWarning("[Auth] 토큰 만료! 다시 로그인 필요");
				Logout();
			}
			Debug.LogWarning("[API] 오류: " + request.error);
			onFail?.Invoke(request.error);
		}
		request.Dispose();
	}
}
