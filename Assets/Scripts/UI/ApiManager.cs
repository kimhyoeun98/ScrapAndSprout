using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;

/// <summary>
/// 서버 통신 매니저 (I01: 클라이언트-서버 데이터 송수신)
/// Spring REST API와의 모든 HTTP 통신을 담당하는 싱글톤입니다.
/// 
/// [비유] 이 클래스는 '우체국'입니다.
/// 다른 스크립트들이 편지(요청)를 우체국에 맡기면,
/// 우체국이 서버로 보내고 답장(응답)을 가져다줍니다.
/// </summary>
public class ApiManager : MonoBehaviour
{
    // ── 싱글톤 패턴 ──
    // 어디서든 ApiManager.Instance로 접근 가능
    public static ApiManager Instance { get; private set; }

    [Header("서버 설정")]
    public string serverBaseUrl = "http://172.31.51.36:8080";
    public string fastApiBaseUrl = "http://172.31.51.36:8000";
    // JWT 토큰 (로그인 시 서버로부터 발급받음)
    private string _jwtToken = "";

    // 현재 로그인한 플레이어 정보
    private string _playerId = "";
    public string playerId
    {
        get => _playerId;
        private set
        {
            if (_playerId != value)
                Debug.Log($"[ApiManager] playerId 변경: '{_playerId}' → '{value}'");
            _playerId = value;
        }
    }
    public string userName { get; private set; } = "";

    // 로그인 상태 확인
    //public bool IsLoggedIn => !string.IsNullOrEmpty(_jwtToken) && playerId > 0;
    public bool IsLoggedIn => !string.IsNullOrEmpty(_jwtToken);

    void Awake()
    {
        // 싱글톤: 게임 내에서 단 하나만 존재하도록 보장
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지

            // 저장된 JWT 토큰이 있으면 자동 로드
            LoadTokenFromPlayerPrefs();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// PlayerPrefs에서 JWT 토큰을 불러옵니다.
    /// [학습 포인트] PlayerPrefs는 Unity의 간단한 저장소입니다.
    /// 비유: 게임을 껐다 켜도 남아있는 "설정 파일" 같은 것
    /// </summary>
    void LoadTokenFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey("jwt_token"))
        {
            _jwtToken = PlayerPrefs.GetString("jwt_token");
            //playerId = PlayerPrefs.GetInt("player_id", 0);
            string loadedId = PlayerPrefs.GetString("player_id", "");
            Debug.Log($"[Auth] PlayerPrefs 로드 - playerId: '{loadedId}'");
            playerId = loadedId;
            userName = PlayerPrefs.GetString("user_name", "");

            Debug.Log($"[Auth] 저장된 토큰 로드 완료 → Player: {userName} (ID: {playerId})");
        }
    }

    /// <summary>
    /// JWT 토큰을 PlayerPrefs에 저장합니다.
    /// </summary>
    //void SaveTokenToPlayerPrefs(string token, int id, string name)
    void SaveTokenToPlayerPrefs(string token, string id, string name)
    {
        _jwtToken = token;
        playerId = id;
        userName = name;

        PlayerPrefs.SetString("jwt_token", token);
        //PlayerPrefs.SetInt("player_id", id);
        PlayerPrefs.SetString("player_id", id);
        PlayerPrefs.SetString("user_name", name);
        PlayerPrefs.Save(); // 즉시 디스크에 저장

        Debug.Log($"[Auth] 토큰 저장 완료 → {userName} (ID: {playerId})");
    }

    /// <summary>
    /// 로그아웃: 저장된 토큰 삭제 및 LoginScene으로 이동
    /// </summary>
    public void Logout()
    {
        // 1. 저장된 토큰 삭제
        PlayerPrefs.DeleteKey("jwt_token");
        PlayerPrefs.Save();

        // 2. 현재 토큰도 초기화
        _jwtToken = "";
        playerId = "";

        Debug.Log("로그아웃 완료!");

        // 3. LoginScene으로 이동
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene");
    }

    // ═══════════════════════════════════════════
    //  1. 로그인 (POST /api/auth/login) — 신규 추가
    // ═══════════════════════════════════════════

    /// <summary>
    /// 서버에 로그인을 요청하고 JWT 토큰을 발급받습니다.
    /// 
    /// [비유] 이 함수는 '출입증 발급소'입니다.
    /// ID/PW를 보여주면 출입증(JWT)을 만들어줍니다.
    /// 이후 모든 요청에 이 출입증을 보여줘야 합니다.
    /// </summary>
    /// <param name="request">로그인 요청 (user_name, password)</param>
    /// <param name="onSuccess">성공 시 콜백 (토큰, ID, 이름 받음)</param>
    /// <param name="onFail">실패 시 콜백</param>
    public void Login(LoginRequest request, Action<LoginResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(PostRequest(
            "/api/auth/login",
            JsonUtility.ToJson(request),
            (LoginResponse response) =>
            {
                // ✅ 토큰이 없으면 실패 처리
                if (string.IsNullOrEmpty(response.token))
                {
                    Debug.LogWarning("[Auth] 토큰 없음 — 로그인 실패");
                    onFail?.Invoke("아이디 또는 비밀번호가 틀렸습니다.");
                    return;
                }
                // 토큰 있으면 저장 후 성공
                SaveTokenToPlayerPrefs(response.token, response.user_id, response.user_name);
                onSuccess?.Invoke(response);
            },
            onFail
        ));
    }



    // ═══════════════════════════════════════════
    //  2. 쓰레기 판매 (POST /api/trade/sell)
    // ═══════════════════════════════════════════

    /// <summary>
    /// 서버에 쓰레기 판매를 요청합니다.
    /// 서버가 금액을 계산하고 DB에 반영한 뒤, 결과 골드를 돌려줍니다.
    /// </summary>
    /// <param name="trashItems">판매할 아이템 이름과 수량</param>
    /// <param name="onSuccess">성공 시 콜백 (갱신된 골드 값을 받음)</param>
    /// <param name="onFail">실패 시 콜백</param>
    public void SellTrash(TrashSellRequest request, Action<TradeResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(PostRequest(
            "/api/trade/sell",
            JsonUtility.ToJson(request),
            onSuccess,
            onFail
        ));
    }

    // ═══════════════════════════════════════════
    //  2. 아이템 구매 (POST /api/trade/buy)
    // ═══════════════════════════════════════════

    /// <summary>
    /// 서버에 아이템 구매를 요청합니다.
    /// 서버가 잔액을 확인하고, 충분하면 차감 후 아이템을 지급합니다.
    /// </summary>
    public void BuyItem(BuyRequest request, Action<TradeResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(PostRequest(
            "/api/trade/buy",
            JsonUtility.ToJson(request),
            onSuccess,
            onFail
        ));
    }

    // ═══════════════════════════════════════════
    //  3. 꾸미기 아이템 배치 기록 (POST /api/deco/place)
    // ═══════════════════════════════════════════

    public void PlaceDecoration(DecoPlaceRequest request, Action<DecoPlaceResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(PostRequest(
            "/api/deco/place",
            JsonUtility.ToJson(request),
            onSuccess,
            onFail
        ));
    }

    // 구버전 호환용 (SeedPlanter 비활성화 후 참조만 남은 경우를 위해 유지)
    public void PlantSeed(PlantRequest request, Action<PlantResponse> onSuccess, Action<string> onFail)
    {
        Debug.LogWarning("[ApiManager] PlantSeed는 더 이상 사용되지 않습니다. PlaceDecoration을 사용하세요.");
        onFail?.Invoke("PlantSeed deprecated");
    }

    // ═══════════════════════════════════════════
    //  4. 플레이어 정보 조회 (GET /api/player/{id})
    // ═══════════════════════════════════════════

    //public void GetPlayerInfo(Action<PlayerInfoResponse> onSuccess, Action<string> onFail)
    //{
    //    StartCoroutine(GetRequest<PlayerInfoResponse>(
    //        $"/api/player/{playerId}",
    //        onSuccess,
    //        onFail
    //    ));
    //}
    public void GetPlayerInfo(Action<PlayerInfoResponse> onSuccess, Action<string> onFail)
    {
        /*
           // ✅ 누가 호출했는지 추적!
           Debug.LogError($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
           Debug.LogError($"[API] GetPlayerInfo 호출됨!");
           Debug.LogError($"[API] playerId: '{playerId}'");
           Debug.LogError($"[API] 호출 위치:\n{System.Environment.StackTrace}");
           Debug.LogError($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        */
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogError("[API] playerId가 비어있음!");
            onFail?.Invoke("로그인이 필요합니다.");
            return;
        }

        string endpoint = $"/api/player/{playerId}";
        Debug.Log($"[API] 요청 URL: {serverBaseUrl}{endpoint}");

        StartCoroutine(GetRequest<PlayerInfoResponse>(
            endpoint,
            onSuccess,
            onFail
        ));
    }
    // ═══════════════════════════════════════════
    //  5. FastAPI - PCG/AI 요청
    // ═══════════════════════════════════════════

    /// <summary>
    /// FastAPI 서버에 요청을 보냅니다 (PCG, AI 추천 등)
    /// </summary>
    public void CallFastAPI(string endpoint, string jsonBody, Action<string> onSuccess, Action<string> onFail)
    {
        StartCoroutine(FastAPIRequest(endpoint, jsonBody, onSuccess, onFail));
    }

    IEnumerator FastAPIRequest(string endpoint, string jsonBody, Action<string> onSuccess, Action<string> onFail)
    {
        string url = fastApiBaseUrl + endpoint;

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log($"[FastAPI] POST {url} → {jsonBody}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            Debug.Log($"[FastAPI] 응답 성공: {responseText}");
            onSuccess?.Invoke(responseText);
        }
        else
        {
            Debug.LogWarning($"[FastAPI] 오류: {request.error}");
            onFail?.Invoke(request.error);
        }

        request.Dispose();
    }
    // ═══════════════════════════════════════════
    //  내부 HTTP 헬퍼 함수들
    // ═══════════════════════════════════════════

    /// <summary>
    /// POST 요청을 보내는 범용 코루틴입니다.
    /// 
    /// [코루틴이란?]
    /// Unity에서 비동기 작업을 처리하는 방법입니다.
    /// 서버 응답을 기다리는 동안 게임이 멈추지 않도록 해줍니다.
    /// 비유: 배달 주문 후 문 앞에서 기다리지 않고, 올 때까지 다른 일 하는 것
    /// 
    /// [JWT 자동 추가]
    /// 로그인되어 있으면 모든 요청에 자동으로 "Authorization: Bearer 토큰" 헤더 추가
    /// </summary>
    IEnumerator PostRequest<T>(string endpoint, string jsonBody, Action<T> onSuccess, Action<string> onFail)
    {
        string url = serverBaseUrl + endpoint;

        // 1. 요청 생성
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // ★ JWT 토큰이 있으면 자동으로 헤더에 추가!
        if (!string.IsNullOrEmpty(_jwtToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);
            Debug.Log($"[API] JWT 토큰 헤더 추가됨");
        }

        Debug.Log($"[API] POST {url} → {jsonBody}");

        // 2. 요청 전송 후 응답 대기
        yield return request.SendWebRequest();

        // 3. 결과 처리
        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            Debug.Log($"[API] 응답 성공: {responseText}");

            // JSON 문자열 → C# 객체로 변환
            T response = JsonUtility.FromJson<T>(responseText);
            onSuccess?.Invoke(response);
        }
        else
        {
            string errorMsg = $"[API] 오류: {request.error}";

            // 401 Unauthorized → 토큰 만료 or 잘못된 토큰
            if (request.responseCode == 401)
            {
                Debug.LogWarning("[Auth] 토큰 만료! 다시 로그인 필요");
                Logout(); // 토큰 삭제
            }

            Debug.LogWarning(errorMsg);
            onFail?.Invoke(errorMsg);
        }

        request.Dispose();
    }

    /// <summary>
    /// GET 요청을 보내는 범용 코루틴입니다.
    /// JWT 토큰이 있으면 자동으로 헤더에 추가됩니다.
    /// </summary>
    IEnumerator GetRequest<T>(string endpoint, Action<T> onSuccess, Action<string> onFail)
    {
        string url = serverBaseUrl + endpoint;

        UnityWebRequest request = UnityWebRequest.Get(url);

        // ★ JWT 토큰이 있으면 자동으로 헤더에 추가!
        if (!string.IsNullOrEmpty(_jwtToken))
        {
            request.SetRequestHeader("Authorization", "Bearer " + _jwtToken);
        }

        Debug.Log($"[API] GET {url}");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string responseText = request.downloadHandler.text;
            Debug.Log($"[API] 응답 성공: {responseText}");

            T response = JsonUtility.FromJson<T>(responseText);
            onSuccess?.Invoke(response);
        }
        else
        {
            // 401 Unauthorized → 토큰 만료
            if (request.responseCode == 401)
            {
                Debug.LogWarning("[Auth] 토큰 만료! 다시 로그인 필요");
                Logout();
            }

            Debug.LogWarning($"[API] 오류: {request.error}");
            onFail?.Invoke(request.error);
        }

        request.Dispose();
    }
}

// ═══════════════════════════════════════════
//  JSON 데이터 모델 (요청/응답 구조체)
//  Spring 서버와 주고받는 JSON의 C# 버전입니다.
// ═══════════════════════════════════════════

// ─────────────────────────────────────────
//  로그인 관련
// ─────────────────────────────────────────

[Serializable]
public class LoginRequest
{
    public string user_name;    // 사용자 이름
    public string password;     // 비밀번호
}

[Serializable]
public class LoginResponse
{
    public string token;        // JWT 토큰 ("eyJhbGc...")

    public string user_id;
    public string user_name;    // 플레이어 이름
}

// ─────────────────────────────────────────
//  거래 관련
// ─────────────────────────────────────────

[Serializable]
public class TrashSellRequest
{
    public string playerId;
    public string[] itemNames;   // 판매할 아이템 이름 배열
    public int[] itemCounts;     // 각 아이템 수량
    public string characterType; // 캐릭터 특성 (델타: 판매가 +5%, 서버에서 계산)
}

[Serializable]
public class BuyRequest
{
    public string playerId;
    public string itemName;     // 구매할 아이템 이름
    public int quantity;        // 구매 수량
    public string characterType; // 캐릭터 특성 (델타: 구매가 할인/적용 서버에서 계산)
}

[Serializable]
public class PlantRequest
{
    public string playerId;
    public float posX;
    public float posY;
}

[Serializable]
public class DecoPlaceRequest
{
    public string playerId;
    public string itemType;     // "나무", "상자" 등
    public int score;           // 배치 점수
}

[Serializable]
public class DecoPlaceResponse
{
    public bool success;
    public int totalDecoScore;
    public string message;
}

[Serializable]
public class TradeResponse
{
    public bool success;        // 거래 성공 여부
    public int gold;            // 거래 후 잔여 골드
    public string message;      // 서버 메시지
}

[Serializable]
public class PlantResponse
{
    public bool success;
    public int treeCount;       // 총 식재 수
    public string message;
}

[Serializable]
public class PlayerInfoResponse
{

    public string playerId;
    public int GOLD;
    public int treeCount;
}