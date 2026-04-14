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

    // 현재 로그인한 플레이어 ID (임시로 1번 고정, 추후 로그인 시스템 연동)
    public int playerId = 1;

    void Awake()
    {
        // 싱글톤: 게임 내에서 단 하나만 존재하도록 보장
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ═══════════════════════════════════════════
    //  1. 쓰레기 판매 (POST /api/trade/sell)
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
    //  3. 씨앗 식재 기록 (POST /api/plant)
    // ═══════════════════════════════════════════

    public void PlantSeed(PlantRequest request, Action<PlantResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(PostRequest(
            "/api/plant",
            JsonUtility.ToJson(request),
            onSuccess,
            onFail
        ));
    }

    // ═══════════════════════════════════════════
    //  4. 플레이어 정보 조회 (GET /api/player/{id})
    // ═══════════════════════════════════════════

    public void GetPlayerInfo(Action<PlayerInfoResponse> onSuccess, Action<string> onFail)
    {
        StartCoroutine(GetRequest<PlayerInfoResponse>(
            $"/api/player/{playerId}",
            onSuccess,
            onFail
        ));
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
            Debug.LogWarning(errorMsg);
            onFail?.Invoke(errorMsg);
        }

        request.Dispose();
    }

    /// <summary>
    /// GET 요청을 보내는 범용 코루틴입니다.
    /// </summary>
    IEnumerator GetRequest<T>(string endpoint, Action<T> onSuccess, Action<string> onFail)
    {
        string url = serverBaseUrl + endpoint;

        UnityWebRequest request = UnityWebRequest.Get(url);

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

[Serializable]
public class TrashSellRequest
{
    public int playerId;
    public string[] itemNames;  // 판매할 아이템 이름 배열
    public int[] itemCounts;    // 각 아이템 수량
}

[Serializable]
public class BuyRequest
{
    public int playerId;
    public string itemName;     // 구매할 아이템 이름
    public int quantity;        // 구매 수량
}

[Serializable]
public class PlantRequest
{
    public int playerId;
    public float posX;          // 식재 위치 X
    public float posY;          // 식재 위치 Y
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
    public int playerId;
    public int gold;
    public int treeCount;
}