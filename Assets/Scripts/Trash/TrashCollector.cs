using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 쓰레기 수거 · NPC 거래 · 인벤토리 관리 담당 클래스
/// 플레이어의 "배낭 + 지갑" 역할입니다.
/// </summary>
public class TrashCollector : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  데이터
    // ─────────────────────────────────────────

    /// <summary>인벤토리: 아이템 이름 → 수량 (예: "Can" → 3)</summary>
    public Dictionary<string, int> inventory = new Dictionary<string, int>();

    /// <summary>현재 보유 골드 (서버 응답으로만 갱신)</summary>
    public int gold = 0;

    // ─────────────────────────────────────────
    //  아이템 데이터
    // ─────────────────────────────────────────

    [System.Serializable]
    public struct ItemData
    {
        public string itemName;   // 아이템 이름 (예: "Can", "Banana")
        public Sprite itemSprite; // 아이템 아이콘 이미지
    }

    [Header("아이템 데이터 설정")]
    public List<ItemData> itemDatabase;

    // ─────────────────────────────────────────
    //  UI 연결
    // ─────────────────────────────────────────

    [Header("UI 연결 (직접 드래그)")]
    public TextMeshProUGUI goldText;         // 화면 우상단 골드 텍스트
    public TextMeshProUGUI[] hotbarTexts;    // 핫바 슬롯 수량 텍스트 배열
    public Image[] hotbarIcons;              // 핫바 슬롯 아이콘 이미지 배열
    public TextMeshProUGUI[] inventoryTexts; // 인벤토리 수량 텍스트 배열
    public Image[] inventoryIcons;           // 인벤토리 아이콘 이미지 배열

    // ─────────────────────────────────────────
    //  가격 설정
    // ─────────────────────────────────────────

    [Header("가격 설정 (서버와 일치해야 함)")]
    public int pricePerTrash = 10; // 쓰레기 1개 판매 가격
    public int seedPrice = 30;     // 씨앗 구매 가격
    public int batteryPrice = 50;  // 배터리 구매 가격

    //[Header("상태 메시지 UI (선택)")] 스프링 수정이후 다시 활성화
    //public TextMeshProUGUI statusMessageText;
    [Header("서버 연동 설정")]
    [SerializeField] private bool useSpringServer = false;  // Inspector에서 on/off

    [Header("상태 메시지 UI (선택)")]
    public TextMeshProUGUI statusMessageText;
    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────

    private void Start()
    {
        // ── 골드 텍스트 자동 연결 ──
        if (goldText == null)
            goldText = GameObject.Find("GoldText")?.GetComponent<TextMeshProUGUI>();

        if (UIManager.Instance != null)
        {
            // ── 핫바 자동 연결 ──
            // Slot 구조: Hotbar → Slot → ItemIcon (아이콘), CountText (수량)
            // slot.Find("ItemIcon")으로 이름 직접 찾는 이유:
            // GetComponentInChildren은 Slot 자체 Image까지 잡아서
            // SetActive(false) 시 Slot 전체가 꺼지는 버그 발생
            if (UIManager.Instance.hotbar != null &&
                (hotbarTexts == null || hotbarTexts.Length == 0 || hotbarTexts[0] == null))
            {
                var texts = new List<TextMeshProUGUI>();
                var icons = new List<Image>();

                foreach (Transform slot in UIManager.Instance.hotbar.transform)
                {
                    // CountText — 수량 텍스트 (이름으로 직접 찾기)
                    var countText = slot.Find("CountText");
                    if (countText != null)
                    {
                        var t = countText.GetComponent<TextMeshProUGUI>();
                        if (t != null) texts.Add(t);
                    }

                    // ItemIcon — 아이콘 Image (이름으로 직접 찾기)
                    var itemIcon = slot.Find("ItemIcon");
                    if (itemIcon != null)
                    {
                        var i = itemIcon.GetComponent<Image>();
                        if (i != null) icons.Add(i);
                    }
                }

                hotbarTexts = texts.ToArray();
                hotbarIcons = icons.ToArray();
                Debug.Log($"[핫바] 텍스트: {hotbarTexts.Length}개, 아이콘: {hotbarIcons.Length}개");
            }

            // ── 인벤토리 자동 연결 ──
            // 인벤토리 구조: InventoryPanel → Grid → BagSlot → Invenicon (아이콘), InvenCountText (수량)
            // 핫바와 다르게 Grid가 중간에 있어서 Grid 안의 BagSlot을 찾아야 함
            if (UIManager.Instance.inventoryPanel != null &&
                (inventoryTexts == null || inventoryTexts.Length == 0 || inventoryTexts[0] == null))
            {
                var texts = new List<TextMeshProUGUI>();
                var icons = new List<Image>();

                // Grid 컴포넌트를 가진 오브젝트 찾기
                var grid = UIManager.Instance.inventoryPanel.GetComponentInChildren<GridLayoutGroup>();
                if (grid != null)
                {
                    foreach (Transform slot in grid.transform)
                    {
                        // InvenCountText — 수량 텍스트 (이름으로 직접 찾기)
                        var countText = slot.Find("InvenCountText");
                        if (countText != null)
                        {
                            var t = countText.GetComponent<TextMeshProUGUI>();
                            if (t != null) texts.Add(t);
                        }

                        // Invenicon — 아이콘 Image (이름으로 직접 찾기)
                        var itemIcon = slot.Find("Invenicon");
                        if (itemIcon != null)
                        {
                            var i = itemIcon.GetComponent<Image>();
                            if (i != null) icons.Add(i);
                        }
                    }
                }

                inventoryTexts = texts.ToArray();
                inventoryIcons = icons.ToArray();
                Debug.Log($"[인벤토리] 텍스트: {inventoryTexts.Length}개, 아이콘: {inventoryIcons.Length}개");
            }
        }

        //시작 시 UI 초기화(ItemIcon만 비활성화, Slot 배경은 유지)
        ClearAllUI();
        UpdateGoldUI();
        LoadPlayerDataFromServer();
        //    if (useSpringServer)
        //    {
        //        LoadPlayerDataFromServer();
        //    }
        //    else
        //    {
        //        Debug.Log("[TrashCollector] Spring 우회 - 로컬 모드");
        //        gold = 0;
        //        UpdateGoldUI();
        //  }
    }

        // ─────────────────────────────────────────
        //  서버 데이터 로드
        // ─────────────────────────────────────────

  void LoadPlayerDataFromServer()
        {
            if (ApiManager.Instance == null)
            {
                Debug.LogWarning("[초기화] ApiManager 없음 — 골드 0으로 시작");
                gold = 0;
                UpdateGoldUI();
                return;
            }

            if (string.IsNullOrEmpty(ApiManager.Instance.playerId))
            {
                Debug.LogWarning("[초기화] playerId 없음 — 골드 0으로 시작");
                gold = 0;
                UpdateGoldUI();
                return;
            }

            Debug.Log($"[초기화] 서버에서 플레이어 데이터 로딩 중... (playerId: '{ApiManager.Instance.playerId}')");

            ApiManager.Instance.GetPlayerInfo(
                (response) =>
                {
                    gold = response.gold;
                    UpdateGoldUI();
                    Debug.Log($"[초기화] 골드 로드 완료: {gold}G");
                },
                (error) =>
                {
                    Debug.LogWarning($"[초기화] 서버 연결 실패: {error}");
                    gold = 0;
                    UpdateGoldUI();
                }
            );
        }

    // ─────────────────────────────────────────
    //  쓰레기 판매
    // ─────────────────────────────────────────
    public void SellAllTrash()
    {
        if (inventory.Count == 0)
        {
            ShowStatus("팔 아이템이 없습니다.");
            return;
        }

        List<string> names = new List<string>();
        List<int> counts = new List<int>();

        foreach (var item in inventory)
        {
            // Seed, Battery 제외 — 쓰레기만 판매
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

        TrashSellRequest request = new TrashSellRequest
        {
            playerId = ApiManager.Instance.playerId,
            itemNames = names.ToArray(),
            itemCounts = counts.ToArray()
        };

        ApiManager.Instance.SellTrash(request,
            // [1] 성공 콜백
            (response) =>
            {
                gold = response.gold;
                int totalSold = 0; // 한 번에 RPC를 보내기 위해 수량 합산

                foreach (string trashName in names)
                {
                    int count = inventory[trashName];
                    totalSold += count;
                    inventory.Remove(trashName);
                }

                // 변경된 부분: 루프를 돌며 직접 호출하는 대신, 총량을 Host로 전달 (RPC)
                if (totalSold > 0)
                {
                    // GameManager에 새로 만들 RPC 메서드 호출
                    GameManager.Instance?.RPC_AddCollectedTrash(totalSold);
                }

                RefreshUI();
                UpdateGoldUI();
                ShowStatus($"판매 완료! 골드: {gold:N0}");
            }, // <--- [수정됨] 이 닫는 괄호와 쉼표가 빠져서 에러가 났습니다!

            // [2] 실패 콜백
            (error) => // <--- [수정됨] 이 부분도 지워져 있었습니다!
            {
                Debug.LogWarning($"[판매] 서버 실패, 로컬 처리: {error}");
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
    //  아이템 구매
    // ─────────────────────────────────────────

    public void BuyItemFromButton(string itemName)
    {
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

        // ✅✅✅ 여기에 이 코드 추가! ✅✅✅
        if (ApiManager.Instance == null || string.IsNullOrEmpty(ApiManager.Instance.playerId))
        {
            Debug.LogWarning("[구매] playerId 없음 - 로컬 처리");
            if (gold < price)
            {
                ShowStatus($"골드 부족! (필요: {price}G, 보유: {gold}G)");
                return;
            }
            gold -= price;
            if (inventory.ContainsKey(itemName))
                inventory[itemName]++;
            else
                inventory.Add(itemName, 1);
            RefreshUI();
            UpdateGoldUI();
            ShowStatus($"(오프라인) {itemName} 구매 완료!");
            return;
        }
        // ✅✅✅ 여기까지 추가! ✅✅✅

        ShowStatus("서버에 구매 요청 중...");

        BuyRequest request = new BuyRequest
        {
            playerId = ApiManager.Instance.playerId,
            itemName = itemName,
            quantity = 1
        };

        ApiManager.Instance.BuyItem(request,
            // 성공: 서버 승인 시만 아이템 지급
            (response) =>
            {
                if (response.success)
                {
                    gold = response.gold;
                    if (inventory.ContainsKey(itemName))
                        inventory[itemName]++;
                    else
                        inventory.Add(itemName, 1);
                    RefreshUI();
                    UpdateGoldUI();
                    ShowStatus($"{itemName} 구매 완료! 골드: {gold:N0}");
                }
                else
                {
                    ShowStatus(response.message);
                }
            },
            // 실패: 로컬 골드로 폴백
            (error) =>
            {
                Debug.LogWarning($"[구매] 서버 실패, 로컬 처리: {error}");
                if (gold < price)
                {
                    ShowStatus($"골드 부족! (필요: {price}G, 보유: {gold}G)");
                    return;
                }
                gold -= price;
                if (inventory.ContainsKey(itemName))
                    inventory[itemName]++;
                else
                    inventory.Add(itemName, 1);
                RefreshUI();
                UpdateGoldUI();
                ShowStatus($"(오프라인) {itemName} 구매 완료!");
            }
        );
    }

    // ─────────────────────────────────────────
    //  UI 갱신 함수들
    // ─────────────────────────────────────────

    /// <summary>골드 텍스트 갱신</summary>
    void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold:N0}"; // N0: 천 단위 콤마 포함
    }

    /// <summary>
    /// 인벤토리 전체 UI 갱신
    /// 각 아이템을 핫바/인벤토리 슬롯에 순서대로 표시
    /// </summary>
    void UpdateAllUI()
    {
        if (hotbarTexts == null || hotbarIcons == null) return;
        if (inventoryTexts == null || inventoryIcons == null) return;

        int index = 0;
        foreach (var item in inventory)
        {
            // 아이템 이름으로 아이콘 스프라이트 찾기
            Sprite targetSprite = GetSpriteByName(item.Key);

            // 핫바 슬롯 갱신
            if (index < hotbarTexts.Length)
            {
                if (hotbarTexts[index] != null)
                    hotbarTexts[index].text = item.Value.ToString();

                if (targetSprite != null && index < hotbarIcons.Length)
                {
                    if (hotbarIcons[index] != null)
                    {
                        hotbarIcons[index].sprite = targetSprite;
                        hotbarIcons[index].gameObject.SetActive(true);
                    }
                }
            }

            // 인벤토리 슬롯 갱신
            if (index < inventoryTexts.Length)
            {
                if (inventoryTexts[index] != null)
                    inventoryTexts[index].text = item.Value.ToString();

                if (targetSprite != null && index < inventoryIcons.Length)
                {
                    if (inventoryIcons[index] != null)
                    {
                        inventoryIcons[index].sprite = targetSprite;
                        inventoryIcons[index].gameObject.SetActive(true);
                    }
                }
            }

            index++;
        }
    }

    /// <summary>
    /// 전체 UI 초기화
    /// 텍스트는 빈 문자열, ItemIcon만 비활성화 (Slot 배경은 유지)
    /// </summary>
    void ClearAllUI()
    {
        if (hotbarTexts != null)
            foreach (var text in hotbarTexts) if (text) text.text = "";
        if (hotbarIcons != null)
            foreach (var icon in hotbarIcons) if (icon) icon.gameObject.SetActive(false);
        if (inventoryTexts != null)
            foreach (var text in inventoryTexts) if (text) text.text = "";
        if (inventoryIcons != null)
            foreach (var icon in inventoryIcons) if (icon) icon.gameObject.SetActive(false);
    }

    /// <summary>아이템 이름으로 아이콘 Sprite 검색</summary>
    Sprite GetSpriteByName(string name)
    {
        foreach (var data in itemDatabase)
        {
            if (data.itemName == name) return data.itemSprite;
        }
        return null;
    }

    /// <summary>상태 메시지 표시</summary>
    void ShowStatus(string message)
    {
        Debug.Log($"[상태] {message}");
        if (statusMessageText != null)
            statusMessageText.text = message;
    }



    /// <summary>외부에서 UI 갱신 요청 시 호출</summary>
    public void RefreshUI()
    {
        ClearAllUI();
        UpdateAllUI();
    }
}