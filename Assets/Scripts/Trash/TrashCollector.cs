using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Fusion;

/// <summary>
/// 쓰레기 수거 · NPC 거래 · 인벤토리 관리 담당 클래스 (Photon Fusion 2 버전)
///
/// [변경 내용 — 기획 개편 반영]
/// 1. CharacterType enum 추가 (알파/베타/감마/델타)
/// 2. BuyItemFromButton: 씨앗/배터리 → 나무/꾸미기아이템으로 교체
/// 3. 기존 인벤토리·골드·RPC·SellAllTrash 구조는 그대로 유지
/// </summary>
public class TrashCollector : NetworkBehaviour
{
    // ─────────────────────────────────────────
    //  캐릭터 특성 (신규)
    // ─────────────────────────────────────────

    public enum CharacterType
    {
        Alpha,  // 희귀 드랍률 +0.5%
        Beta,   // 더블 드랍 확률 +5%
        Gamma,  // 꾸미기 점수 +1%
        Delta   // 골드 획득률 +5%
    }

    [Header("── 캐릭터 특성 ──")]
    [Tooltip("캐릭터 선택 UI에서 선택된 캐릭터 타입")]
    public CharacterType characterType = CharacterType.Alpha;

    // ─────────────────────────────────────────
    //  데이터 (Data)
    // ─────────────────────────────────────────

    /// <summary>
    /// 인벤토리: 아이템 이름 → 수량 (예: "음료캔" → 3)
    /// [Networked] 미지원 타입이라 로컬 관리
    /// </summary>
    public Dictionary<string, int> inventory = new Dictionary<string, int>();

    /// <summary>
    /// 골드: [Networked]로 모든 클라이언트 자동 동기화
    /// </summary>
    [Networked] public int gold { get; set; }

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
    public List<ItemData> itemDatabase;

    // ─────────────────────────────────────────
    //  UI 연결 (인스펙터에서 드래그)
    // ─────────────────────────────────────────

    [Header("UI 연결 (직접 드래그)")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI[] hotbarTexts;
    public Image[] hotbarIcons;
    public TextMeshProUGUI[] inventoryTexts;
    public Image[] inventoryIcons;

    // ─────────────────────────────────────────
    //  상점 설정 — 꾸미기 아이템 가격표 (신규)
    //
    //  [비유]
    //  NPC 상점의 가격 메뉴판입니다.
    //  서버(Spring)와 반드시 동일한 값이어야 합니다.
    // ─────────────────────────────────────────

    [Header("꾸미기 아이템 구매 가격")]
    [Tooltip("나무 구매 가격 (골드)")]
    public int treePrice = 40;

    [Tooltip("상자 구매 가격 (골드)")]
    public int boxPrice = 20;

    [Tooltip("의자 구매 가격 (골드)")]
    public int chairPrice = 30;

    [Tooltip("울타리 구매 가격 (골드)")]
    public int fencePrice = 50;

    [Tooltip("꽃병 구매 가격 (골드)")]
    public int vasePrice = 60;

    [Tooltip("탁자 구매 가격 (골드)")]
    public int tablePrice = 100;

    [Tooltip("꽃밭 구매 가격 (골드)")]
    public int flowerFieldPrice = 200;

    // ─────────────────────────────────────────
    //  꾸미기 점수표 (GameManager에 보고할 때 사용)
    // ─────────────────────────────────────────

    private static readonly Dictionary<string, int> _decorScores = new Dictionary<string, int>
    {
        { "나무",        40 },
        { "나무풍 상자",  20 },
        { "나무풍 의자",  40 },
        { "나무풍 울타리",50 },
        { "나무풍 꽃병",  60 },
        { "나무풍 탁자",  100 },
        { "나무풍 꽃밭", 200 }
    };

    // ─────────────────────────────────────────
    //  상태 메시지 UI
    // ─────────────────────────────────────────

    [Header("상태 메시지 UI (선택)")]
    public TextMeshProUGUI statusMessageText;

    // ─────────────────────────────────────────
    //  Photon 생명주기
    // ─────────────────────────────────────────

    public override void Spawned()
    {
        // 프리팹 이름 기반 characterType 자동 설정 (Inspector 설정의 안전장치)
        string goName = gameObject.name.ToLower();
        if      (goName.Contains("alpha")) characterType = CharacterType.Alpha;
        else if (goName.Contains("beta"))  characterType = CharacterType.Beta;
        else if (goName.Contains("gamma")) characterType = CharacterType.Gamma;
        else if (goName.Contains("delta")) characterType = CharacterType.Delta;
        Debug.Log($"[TrashCollector] 캐릭터 타입: {characterType}");

        if (HasInputAuthority)
            FindAndConnectUI();

        ClearAllUI();
        UpdateGoldUI();

        if (!HasInputAuthority) return;

        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // DecoScene: 인벤토리 복원 + 플레이어 숨기기
        if (currentScene == "DecoScene")
        {
            DecoInventoryBridge.RestoreTo(this);

            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
                sr.enabled = false;

            var pm = GetComponent<PlayerMovement>();
            if (pm != null) pm.IsMovementLocked = true;

            var nameTag = GetComponentInChildren<PlayerNameTag>();
            if (nameTag != null) nameTag.gameObject.SetActive(false);

            Debug.Log("[TrashCollector] DecoScene — 인벤토리 복원, 플레이어 숨김");
            return;
        }

        // TrashZoneScene 복귀: 인벤토리 복원 + 텔포 위치로 이동
        if (currentScene == "TrashZoneScene" && DecoInventoryBridge.IsReturningFromDeco)
        {
            DecoInventoryBridge.RestoreTo(this);
            StartCoroutine(TeleportToReturnPos());
            DecoInventoryBridge.ClearReturnFlag();
            Debug.Log("[TrashCollector] DecoScene 복귀 — 인벤토리·위치 복원");
            return;
        }

        LoadPlayerDataFromServer();
        Debug.Log("[TrashCollector] 초기화 완료");
    }

    public override void Render()
    {
        if (!HasInputAuthority) return;
        UpdateGoldUI();
    }

    // ─────────────────────────────────────────
    //  서버 데이터 로드
    // ─────────────────────────────────────────

    void LoadPlayerDataFromServer()
    {
        if (ApiManager.Instance == null)
        {
            Debug.LogWarning("[초기화] ApiManager 없음 — 골드 0으로 시작");
            return;
        }
        if (!ApiManager.Instance.IsLoggedIn)
        {
            Debug.LogWarning("[초기화] 로그인 상태가 아닙니다!");
            return;
        }

        ApiManager.Instance.GetPlayerInfo(
            (response) =>
            {
                RPC_SetGold(response.GOLD);
                Debug.Log($"[초기화] 골드 로드 완료: {response.GOLD}G");
            },
            (error) =>
            {
                Debug.LogWarning($"[초기화] 서버 연결 실패, 골드 0으로 시작: {error}");
                RPC_SetGold(0);
            }
        );
    }

    // ─────────────────────────────────────────
    //  쓰레기 판매 (기존 그대로 유지)
    //
    //  [변경점]
    //  델타 캐릭터 특성: 판매 가격 +5% 적용
    // ─────────────────────────────────────────

    public void SellAllTrash()
    {
        if (inventory.Count == 0)
        {
            ShowStatus("팔 아이템이 없습니다.");
            return;
        }

        // 판매 대상 수집 (꾸미기 아이템 제외)
        List<string> names = new List<string>();
        List<int> counts = new List<int>();

        foreach (var item in inventory)
        {
            // 꾸미기 아이템은 판매 대상에서 제외
            if (_decorScores.ContainsKey(item.Key)) continue;

            names.Add(item.Key);
            counts.Add(item.Value);
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
            itemCounts = counts.ToArray(),
            // 델타 캐릭터 여부를 서버에 전달 (서버에서 +5% 계산)
            characterType = characterType.ToString()
        };

        ApiManager.Instance.SellTrash(request,
            (response) =>
            {
                int earned = response.gold - gold;
                RPC_SetGold(response.gold);

                foreach (string trashName in names)
                {
                    int count = inventory[trashName];
                    for (int i = 0; i < count; i++)
                        GameManager.Instance?.OnTrashCollected();
                    inventory.Remove(trashName);
                }

                RefreshUI();

                AchievementManager.Instance?.OnGoldAccumulated(response.gold);
                UIManager.Instance?.ShowFloatingGold(earned, transform.position);
                ShowStatus($"판매 완료! 현재 골드: {gold:N0}");
            },
            (error) =>
            {
                Debug.LogWarning($"[판매] 서버 연결 실패, 로컬 처리: {error}");

                int totalEarned = 0;
                foreach (string name in names)
                {
                    float multiplier = (characterType == CharacterType.Delta) ? 1.05f : 1f;
                    int price = GetLocalItemPrice(name);
                    totalEarned += Mathf.RoundToInt(inventory[name] * price * multiplier);
                    inventory.Remove(name);
                }

                RPC_SetGold(gold + totalEarned);
                RefreshUI();
                UIManager.Instance?.ShowFloatingGold(totalEarned, transform.position);
                ShowStatus($"(오프라인) 판매 완료 +{totalEarned}G");
            }
        );
    }

    // 로컬 폴백용 아이템 가격 조회
    int GetLocalItemPrice(string itemName)
    {
        int index = System.Array.IndexOf(
            new string[] { "휴지", "바나나껍질", "음료캔", "디스크", "타이어", "드럼통", "컴퓨터" },
            itemName
        );
        if (index >= 0 && index < TrashPile.ItemPrices.Length)
            return TrashPile.ItemPrices[index];
        return 0;
    }

    // ─────────────────────────────────────────
    //  꾸미기 아이템 구매 (신규)
    //
    //  [기존 BuyItemFromButton 대체]
    //  씨앗/배터리 → 나무/꾸미기 아이템으로 교체
    // ─────────────────────────────────────────

    /// <summary>
    /// NPC 상점 버튼에서 호출.
    /// itemName 예시: "나무", "상자", "의자", "울타리", "꽃병", "탁자", "꽃밭"
    /// </summary>
    public void BuyDecorationItem(string itemName)
    {
        int price = GetDecorationPrice(itemName);
        if (price <= 0)
        {
            Debug.LogWarning($"[구매] 알 수 없는 아이템: {itemName}");
            return;
        }

        ShowStatus("서버에 구매 요청 중...");

        BuyRequest request = new BuyRequest
        {
            playerId = ApiManager.Instance.playerId,
            itemName = itemName,
            quantity = 1,
            characterType = characterType.ToString()
        };

        ApiManager.Instance.BuyItem(request,
            (response) =>
            {
                if (response.success)
                {
                    int spent = response.gold - gold;
                    RPC_SetGold(response.gold);

                    if (inventory.ContainsKey(itemName))
                        inventory[itemName]++;
                    else
                        inventory.Add(itemName, 1);

                    RefreshUI();
                    UIManager.Instance?.ShowFloatingGold(spent, transform.position);
                    ShowStatus($"{itemName} 구매 완료! 남은 골드: {gold:N0}");
                    Debug.Log($"[구매 성공] {itemName} | 서버 골드: {response.gold}");
                }
                else
                {
                    ShowStatus(response.message);
                }
            },
            (error) =>
            {
                Debug.LogWarning($"[구매] 서버 연결 실패, 로컬 처리: {error}");

                if (gold < price)
                {
                    ShowStatus($"골드 부족! (필요: {price}G, 보유: {gold}G)");
                    return;
                }

                RPC_SetGold(gold - price);

                if (inventory.ContainsKey(itemName))
                    inventory[itemName]++;
                else
                    inventory.Add(itemName, 1);

                RefreshUI();
                UIManager.Instance?.ShowFloatingGold(-price, transform.position);
                ShowStatus($"(오프라인) {itemName} 구매 완료!");
            }
        );
    }

    System.Collections.IEnumerator TeleportToReturnPos()
    {
        yield return null; // 스폰 완료 대기
        var pm = GetComponent<PlayerMovement>();
        if (pm != null)
            pm.RPC_Teleport(DecoInventoryBridge.ReturnPosition);
    }

    int GetDecorationPrice(string itemName)
    {
        switch (itemName)
        {
            case "나무":         return treePrice;
            case "나무풍 상자":   return boxPrice;
            case "나무풍 의자":   return chairPrice;
            case "나무풍 울타리": return fencePrice;
            case "나무풍 꽃병":   return vasePrice;
            case "나무풍 탁자":   return tablePrice;
            case "나무풍 꽃밭":   return flowerFieldPrice;
            default:              return 0;
        }
    }

    /// <summary>
    /// 꾸미기 점수 계산 (감마 캐릭터 +1% 보정 포함)
    /// DecorationPlacer.cs에서 호출합니다
    /// </summary>
    public int GetDecorScore(string itemName)
    {
        if (!_decorScores.ContainsKey(itemName)) return 0;

        float baseScore = _decorScores[itemName];

        // 감마 캐릭터: 꾸미기 점수 +1%
        if (characterType == CharacterType.Gamma)
            baseScore *= 1.01f;

        return Mathf.RoundToInt(baseScore);
    }

    // ─────────────────────────────────────────
    //  RPC
    // ─────────────────────────────────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetGold(int newGold)
    {
        gold = newGold;
        Debug.Log($"[골드] 동기화: {gold}");
    }

    /// <summary>
    /// 아이템 추가 요청 — StateAuthority에서 처리 후 모든 클라이언트에 동기화
    /// </summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_AddItem(string itemName)
    {
        if (!HasStateAuthority) return;

        // Host만 아이템 추가
        if (inventory.ContainsKey(itemName))
            inventory[itemName]++;
        else
            inventory.Add(itemName, 1);

        Debug.Log($"[인벤토리] {itemName} 추가! 현재: {inventory[itemName]}개");

        // 모든 클라이언트에게 UI 갱신 알림
        RPC_RefreshUIAll(itemName);
    }

    /// <summary>
    /// 모든 클라이언트에게 UI 갱신 (추가하지 않음, 동기화만 함)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RefreshUIAll(string itemName)
    {
        Debug.Log($"[인벤토리] UI 갱신: {itemName}");
        RefreshUI();
    }

    // ─────────────────────────────────────────
    //  UI 갱신 (기존 그대로)
    // ─────────────────────────────────────────

    public void RefreshUI()
    {
        ClearAllUI();
        UpdateAllUI();
    }

    void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold:N0}";
    }

    void UpdateAllUI()
    {
        int index = 0;
        foreach (var item in inventory)
        {
            Sprite sprite = GetSpriteByName(item.Key);

            if (index < hotbarTexts.Length && hotbarTexts[index] != null)
                hotbarTexts[index].text = item.Value.ToString();

            if (sprite != null && index < hotbarIcons.Length && hotbarIcons[index] != null)
            {
                hotbarIcons[index].sprite = sprite;
                hotbarIcons[index].gameObject.SetActive(true);
            }

            if (index < inventoryTexts.Length && inventoryTexts[index] != null)
                inventoryTexts[index].text = item.Value.ToString();

            if (sprite != null && index < inventoryIcons.Length && inventoryIcons[index] != null)
            {
                inventoryIcons[index].sprite = sprite;
                inventoryIcons[index].gameObject.SetActive(true);
            }

            index++;
        }
    }

    void FindAndConnectUI()
    {
        Debug.Log("[TrashCollector] UI 자동 연결 시작...");

        GameObject goldObj = GameObject.Find("GoldText");
        if (goldObj != null)
            goldText = goldObj.GetComponent<TextMeshProUGUI>();

        hotbarTexts = new TextMeshProUGUI[10];
        hotbarIcons = new Image[10];
        int hotbarCount = 0;

        for (int i = 0; i < 10; i++)
        {
            string slotName = (i == 0) ? "Slot" : $"Slot ({i})";
            GameObject slotObj = GameObject.Find(slotName);
            if (slotObj != null)
            {
                Transform countText = slotObj.transform.Find("CountText");
                Transform itemIcon  = slotObj.transform.Find("ItemIcon");
                if (countText != null) { hotbarTexts[i] = countText.GetComponent<TextMeshProUGUI>(); hotbarCount++; }
                if (itemIcon  != null)   hotbarIcons[i]  = itemIcon.GetComponent<Image>();
            }
        }
        Debug.Log($"[TrashCollector] ✅ Hotbar 연결: {hotbarCount}개");

        inventoryTexts = new TextMeshProUGUI[24];
        inventoryIcons = new Image[24];

        // FindObjectsByType으로 비활성 오브젝트도 탐색 (UIManager가 inactive여도 찾을 수 있음)
        GameObject uiManager = GameObject.Find("UIManager");
        if (uiManager == null)
        {
            var allBehaviours = FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allBehaviours.Length > 0) uiManager = allBehaviours[0].gameObject;
        }
        if (uiManager != null)
        {
            Transform inventoryPanel = uiManager.transform.Find("InventoryPanel");
            if (inventoryPanel != null)
            {
                Transform grid = inventoryPanel.Find("Grid");
                if (grid != null)
                {
                    int invCount = 0;
                    for (int i = 0; i < 24; i++)
                    {
                        string bagSlotName = (i == 0) ? "BagSlot" : $"BagSlot ({i})";
                        Transform bagSlot  = grid.Find(bagSlotName);
                        if (bagSlot != null)
                        {
                            Transform countText = bagSlot.Find("InvenCountText");
                            Transform itemIcon  = bagSlot.Find("InvenIcon");
                            if (countText != null) { inventoryTexts[i] = countText.GetComponent<TextMeshProUGUI>(); invCount++; }
                            if (itemIcon  != null)   inventoryIcons[i]  = itemIcon.GetComponent<Image>();
                        }
                    }
                    Debug.Log($"[TrashCollector] ✅ Inventory 연결: {invCount}개");
                }
            }
        }
    }

    void ClearAllUI()
    {
        foreach (var t in hotbarTexts)  if (t) t.text = "";
        foreach (var i in hotbarIcons)  if (i) i.gameObject.SetActive(false);
        foreach (var t in inventoryTexts) if (t) t.text = "";
        foreach (var i in inventoryIcons) if (i) i.gameObject.SetActive(false);
    }

    Sprite GetSpriteByName(string name)
    {
        foreach (var data in itemDatabase)
            if (data.itemName == name) return data.itemSprite;
        return null;
    }

    void ShowStatus(string message)
    {
        Debug.Log($"[상태] {message}");
        if (statusMessageText != null)
            statusMessageText.text = message;
    }
}
