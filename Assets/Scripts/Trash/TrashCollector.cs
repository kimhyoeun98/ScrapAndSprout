using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 쓰레기 수거 · NPC 거래 · 인벤토리 관리 담당 클래스
/// 
/// [이 클래스의 역할 - 비유]
/// 플레이어의 "배낭 + 지갑" 역할입니다.
/// 쓰레기를 줍고, NPC에게 팔고, 씨앗/배터리를 구매하는
/// 모든 '재화 흐름'을 이 클래스가 관리합니다.
///
/// [중요 원칙 - Server-Authoritative]
/// 골드 계산은 반드시 서버가 합니다.
/// 클라이언트(이 스크립트)는 서버의 결과를 화면에 보여줄 뿐입니다.
/// 이유: 클라이언트 코드는 해킹이 가능하지만, 서버는 그렇지 않습니다.
/// </summary>
public class TrashCollector : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  데이터 (Data)
    // ─────────────────────────────────────────

    /// <summary>인벤토리: 아이템 이름 → 수량 (예: "Can" → 3)</summary>
    public Dictionary<string, int> inventory = new Dictionary<string, int>();

    /// <summary>현재 보유 골드 (서버 응답으로만 갱신됩니다)</summary>
    public int gold = 0;

    // ─────────────────────────────────────────
    //  아이템 데이터 (인스펙터에서 설정)
    // ─────────────────────────────────────────

    [System.Serializable]
    public struct ItemData
    {
        public string itemName;
        public Sprite itemSprite;
    }

    [Header("아이템 데이터 설정")]
    public List<ItemData> itemDatabase; // 아이템 이름과 아이콘 연결 목록

    // ─────────────────────────────────────────
    //  UI 연결 (인스펙터에서 드래그)
    // ─────────────────────────────────────────

    [Header("UI 연결 (직접 드래그)")]
    public TextMeshProUGUI goldText;          // 화면 골드 표시
    public TextMeshProUGUI[] hotbarTexts;     // 하단 핫바 수량 텍스트
    public Image[] hotbarIcons;              // 하단 핫바 아이콘

    public TextMeshProUGUI[] inventoryTexts; // 인벤토리 창 수량 텍스트
    public Image[] inventoryIcons;           // 인벤토리 창 아이콘

    // ─────────────────────────────────────────
    //  상점 설정 (인스펙터에서 설정)
    // ─────────────────────────────────────────

    [Header("가격 설정 (서버와 일치해야 함)")]
    public int pricePerTrash = 10;  // 쓰레기 1개 판매 가격 (로컬 폴백용)
    public int seedPrice = 30;      // 씨앗 구매 가격
    public int batteryPrice = 50;   // 배터리 구매 가격

    // ─────────────────────────────────────────
    //  상태 메시지 UI (인스펙터에서 드래그)
    // ─────────────────────────────────────────

    [Header("상태 메시지 UI (선택)")]
    [Tooltip("'골드 부족', '거래 완료' 등 상태 메시지를 표시할 텍스트입니다.")]
    public TextMeshProUGUI statusMessageText; // 없어도 동작함

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    private void Start()
    {
        // 시작 시 모든 UI를 빈 상태로 초기화
        ClearAllUI();
        UpdateGoldUI();
        LoadPlayerDataFromServer();
    }
    /// <summary>
    /// 서버에서 플레이어 데이터를 불러와 화면에 반영합니다.
    /// 비유: 게임을 켜면 은행 앱이 실제 잔액을 보여주는 것과 같습니다.
    /// </summary>
    void LoadPlayerDataFromServer()
    {
        if (ApiManager.Instance == null)
        {
            Debug.LogWarning("[초기화] ApiManager 없음 — 골드 0으로 시작");
            UpdateGoldUI();
            return;
        }

        Debug.Log("[초기화] 서버에서 플레이어 데이터 로딩 중...");

        ApiManager.Instance.GetPlayerInfo(
            // ── 성공: 서버 데이터로 골드 초기화 ──
            (response) =>
            {
                gold = response.gold;
                UpdateGoldUI();
                Debug.Log($"[초기화] 골드 로드 완료: {gold}G | 나무: {response.treeCount}그루");
            },
            // ── 실패: 서버 연결 불가 시 0으로 시작 ──
            (error) =>
            {
                Debug.LogWarning($"[초기화] 서버 연결 실패, 골드 0으로 시작: {error}");
                gold = 0;
                UpdateGoldUI();
            }
        );
    }

    // ─────────────────────────────────────────
    //  쓰레기 판매 (서버 연동 ✅)
    // ─────────────────────────────────────────

    /// <summary>
    /// NPC에게 모든 쓰레기를 판매합니다.
    /// 
    /// [흐름]
    /// 1. 인벤토리에서 쓰레기 목록 수집
    /// 2. ApiManager를 통해 서버에 판매 요청
    /// 3. 서버가 계산한 최종 골드로 UI 갱신
    /// (서버 연결 실패 시 → 로컬 계산으로 폴백)
    /// </summary>
    public void SellAllTrash()
    {
        if (inventory.Count == 0)
        {
            ShowStatus("팔 아이템이 없습니다.");
            Debug.Log("[판매] 인벤토리가 비어있습니다.");
            return;
        }

        // 1. 판매 대상 쓰레기 목록 수집
        List<string> names = new List<string>();
        List<int> counts = new List<int>();

        foreach (var item in inventory)
        {
            // "Seed", "Battery"가 아닌 것은 모두 쓰레기로 간주
            if (item.Key != "Seed" && item.Key != "Battery")
            {
                names.Add(item.Key);
                counts.Add(item.Value);
            }
        }

        if (names.Count == 0)
        {
            ShowStatus("판매할 쓰레기가 없습니다.");
            return;
        }

        ShowStatus("서버에 판매 요청 중...");

        // 2. 서버에 판매 요청 (Server-Authoritative)
        TrashSellRequest request = new TrashSellRequest
        {
            playerId = ApiManager.Instance.playerId,
            itemNames = names.ToArray(),
            itemCounts = counts.ToArray()
        };

        ApiManager.Instance.SellTrash(request,
        // ── 성공 콜백: 서버가 계산한 결과 반영 ──
        (response) =>
        {
            // 서버가 알려준 골드로 덮어씁니다 (클라이언트 임의 계산 금지!)
            gold = response.gold;

            // 판매한 쓰레기 수만큼 카운팅
            foreach (string trashName in names)
            {
                // 판매한 수량만큼 GameManager에 알림
                int count = inventory[trashName];
                for (int i = 0; i < count; i++)
                {
                    GameManager.Instance?.OnTrashCollected();
                }

                // ✅ 업적 체크 추가!
                if (AchievementManager.Instance != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        AchievementManager.Instance.OnTrashCollected();
                    }
                }

                inventory.Remove(trashName);
            }

            RefreshUI();
            UpdateGoldUI();
            ShowStatus($"판매 완료! 현재 골드: {gold:N0}");
            Debug.Log($"[판매 성공] 서버 응답: {response.message} | 골드: {response.gold}");
        },
            // ── 실패 콜백: 서버 연결 불가 시 로컬로 처리 ──
            (error) =>
            {
                Debug.LogWarning($"[판매] 서버 연결 실패, 로컬 처리로 전환: {error}");

                int totalEarned = 0;
                foreach (string name in names)
                {
                    totalEarned += inventory[name] * pricePerTrash;
                    inventory.Remove(name);
                }
                gold += totalEarned;

                RefreshUI();
                UpdateGoldUI();

                ShowStatus($"(오프라인) 판매 완료 +{totalEarned}G");
            }
        );
    }

    // ─────────────────────────────────────────
    //  아이템 구매 (서버 연동 ✅ — 버그 수정)
    // ─────────────────────────────────────────

    /// <summary>
    /// NPC 상점 버튼과 연결되는 구매 함수입니다.
    /// Unity Inspector의 Button.OnClick()에 이 함수를 등록하세요.
    /// 
    /// [버그 수정 포인트]
    /// 기존 코드는 로컬에서만 골드를 차감했습니다.
    /// 수정 후: 서버에 먼저 요청 → 서버 승인 시 아이템 지급
    /// 이유: 서버만이 골드 잔액을 신뢰할 수 있기 때문입니다.
    /// </summary>
    /// <param name="itemName">구매할 아이템 이름 ("Seed" 또는 "Battery")</param>
    public void BuyItemFromButton(string itemName)
    {
        // 아이템 이름에 따라 가격 결정
        int price = 0;
        if (itemName == "Seed") price = seedPrice;
        else if (itemName == "Battery") price = batteryPrice;
        else
        {
            Debug.LogWarning($"[구매] 알 수 없는 아이템: {itemName}");
            return;
        }

        // 인벤토리 공간 확인 (최대 20칸)
        if (inventory.Count >= 20 && !inventory.ContainsKey(itemName))
        {
            ShowStatus("가방이 가득 찼습니다!");
            return;
        }

        ShowStatus("서버에 구매 요청 중...");

        // ── 서버에 구매 요청 (Server-Authoritative) ──
        BuyRequest request = new BuyRequest
        {
            playerId = ApiManager.Instance.playerId,
            itemName = itemName,
            quantity = 1
        };

        ApiManager.Instance.BuyItem(request,
            // ── 성공 콜백: 서버가 승인한 경우에만 아이템 지급 ──
            (response) =>
            {
                if (response.success)
                {
                    // 서버가 계산한 최종 골드로 갱신
                    gold = response.gold;

                    // 인벤토리에 아이템 추가
                    if (inventory.ContainsKey(itemName))
                        inventory[itemName]++;
                    else
                        inventory.Add(itemName, 1);

                    RefreshUI();
                    UpdateGoldUI();

                    ShowStatus($"{itemName} 구매 완료! 남은 골드: {gold:N0}");
                    Debug.Log($"[구매 성공] {itemName} x1 | 서버 골드: {response.gold}");
                }
                else
                {
                    // 서버가 거부한 경우 (골드 부족 등)
                    ShowStatus(response.message); // 서버 메시지 그대로 표시
                    Debug.Log($"[구매 거부] 서버 메시지: {response.message}");
                }
            },
            // ── 실패 콜백: 서버 연결 불가 시 로컬 처리 ──
            (error) =>
            {
                Debug.LogWarning($"[구매] 서버 연결 실패, 로컬 처리로 전환: {error}");

                // 로컬 잔액 확인 후 처리
                if (gold < price)
                {
                    ShowStatus($"골드가 부족합니다! (필요: {price}G, 보유: {gold}G)");
                    return;
                }

                gold -= price;
                if (inventory.ContainsKey(itemName))
                    inventory[itemName]++;
                else
                    inventory.Add(itemName, 1);

                RefreshUI();
                UpdateGoldUI();
                ShowStatus($"(오프라인) {itemName} 구매 완료! 남은 골드: {gold:N0}");
            }
        );
    }

    // ─────────────────────────────────────────
    //  UI 갱신 함수들
    // ─────────────────────────────────────────

    /// <summary>
    /// 골드 UI만 갱신합니다. (효율적인 부분 갱신)
    /// </summary>
    void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold:N0}"; // N0: 천 단위 콤마 포함
    }

    /// <summary>
    /// 인벤토리 전체 UI를 갱신합니다.
    /// [비유] 창고 재고 현황판을 전부 지우고 다시 씁니다.
    /// </summary>
    void UpdateAllUI()
    {
        int index = 0;
        foreach (var item in inventory)
        {
            Sprite targetSprite = GetSpriteByName(item.Key);

            // 핫바 갱신
            if (index < hotbarTexts.Length)
            {
                hotbarTexts[index].text = item.Value.ToString();
                if (targetSprite != null && index < hotbarIcons.Length)
                {
                    hotbarIcons[index].sprite = targetSprite;
                    hotbarIcons[index].gameObject.SetActive(true);
                }
            }

            // 인벤토리 창 갱신
            if (index < inventoryTexts.Length)
            {
                inventoryTexts[index].text = item.Value.ToString();
                if (targetSprite != null && index < inventoryIcons.Length)
                {
                    inventoryIcons[index].sprite = targetSprite;
                    inventoryIcons[index].gameObject.SetActive(true);
                }
            }
            index++;
        }
    }

    /// <summary>
    /// 전체 UI를 비웁니다.
    /// </summary>
    void ClearAllUI()
    {
        foreach (var text in hotbarTexts) if (text) text.text = "";
        foreach (var icon in hotbarIcons) if (icon) icon.gameObject.SetActive(false);
        foreach (var text in inventoryTexts) if (text) text.text = "";
        foreach (var icon in inventoryIcons) if (icon) icon.gameObject.SetActive(false);
    }

    /// <summary>
    /// 아이템 이름으로 아이콘 Sprite를 찾습니다.
    /// </summary>
    Sprite GetSpriteByName(string name)
    {
        foreach (var data in itemDatabase)
        {
            if (data.itemName == name) return data.itemSprite;
        }
        return null;
    }

    /// <summary>
    /// 상태 메시지를 화면에 표시합니다.
    /// statusMessageText가 없어도 게임은 정상 동작합니다.
    /// </summary>
    void ShowStatus(string message)
    {
        Debug.Log($"[상태] {message}");
        if (statusMessageText != null)
            statusMessageText.text = message;
    }

    // ─────────────────────────────────────────
    //  외부 호출용 공개 함수
    // ─────────────────────────────────────────

    /// <summary>
    /// 다른 스크립트(SeedPlanter 등)에서 UI 갱신 요청 시 호출합니다.
    /// </summary>
    public void RefreshUI()
    {
        ClearAllUI();
        UpdateAllUI();
    }
}