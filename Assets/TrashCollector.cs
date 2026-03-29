using UnityEngine;
using TMPro;
using UnityEngine.UI; // Image 사용을 위해 필수
using System.Collections.Generic;

public class TrashCollector : MonoBehaviour
{
    // [시스템] 아이템 데이터를 저장할 Dictionary
    public Dictionary<string, int> inventory = new Dictionary<string, int>();
    public int gold = 0;



    // [시스템] 아이템 이름과 이미지를 매칭하기 위한 구조체
    [System.Serializable]
    public struct ItemData
    {
        public string itemName;
        public Sprite itemSprite;
    }

    [Header("아이템 데이터 설정")]
    public List<ItemData> itemDatabase; // 인스펙터에서 등록할 리스트

    [Header("UI 연결 (직접 드래그)")]
    public TextMeshProUGUI goldText; // 골드 표시 텍스트
    public TextMeshProUGUI[] hotbarTexts;    // 하단 핫바 텍스트들
    public Image[] hotbarIcons;            // 하단 핫바 아이콘(Image)들

    public TextMeshProUGUI[] inventoryTexts; // 가방 20칸 텍스트들
    public Image[] inventoryIcons;        // 가방 20칸 아이콘(Image)들

    [Header("아이템 가격 설정")]
    public int pricePerTrash = 10; // 쓰레기 한 개당 기본 가격 / 추후 상인 npc 기분에 따라 아이템별 가격 설정도 가능

    [Header("상점 설정")]
    public int seedPrice = 30; // 씨앗 가격
    public int batteryPrice = 50; // 배터리 가격

    private void Start()
    {
        // 시작 시 모든 아이콘 초기화 (비활성화)
        ClearAllUI();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Trash"))
        {
            string itemName = other.gameObject.name.Replace("(Clone)", "").Trim();

            if (inventory.ContainsKey(itemName)) inventory[itemName]++;
            else inventory.Add(itemName, 1);

            UpdateAllUI();
            Destroy(other.gameObject);
        }
    }

    void UpdateAllUI()
    {
        int index = 0;
        foreach (var item in inventory)
        {
            Sprite targetSprite = GetSpriteByName(item.Key);

            // 1. 하단 핫바 업데이트
            if (index < hotbarTexts.Length)
            {
                hotbarTexts[index].text = item.Value.ToString();
                if (targetSprite != null)
                {
                    hotbarIcons[index].sprite = targetSprite;
                    hotbarIcons[index].gameObject.SetActive(true); // 아이콘 켜기
                }
            }

            // 2. 전체 가방 인벤토리 업데이트
            if (index < inventoryTexts.Length)
            {
                inventoryTexts[index].text = item.Value.ToString();
                if (targetSprite != null)
                {
                    inventoryIcons[index].sprite = targetSprite;
                    inventoryIcons[index].gameObject.SetActive(true); // 아이콘 켜기
                }
            }
            index++;
        }
    }

    // 이름으로 Sprite를 찾아주는 함수
    Sprite GetSpriteByName(string name)
    {
        foreach (var data in itemDatabase)
        {
            if (data.itemName == name) return data.itemSprite;
        }
        return null;
    }

    void ClearAllUI()
    {
        foreach (var text in hotbarTexts) text.text = "";
        foreach (var icon in hotbarIcons) icon.gameObject.SetActive(false);
        foreach (var text in inventoryTexts) text.text = "";
        foreach (var icon in inventoryIcons) icon.gameObject.SetActive(false);
    }

    public void SellAllTrash()
    {
        // 1. 방어 코드: 팔 아이템이 없으면 실행 안 함
        if (inventory.Count == 0)
        {
            Debug.Log("판매할 쓰레기가 없습니다!");
            return;
        }

        // 2. 수익 계산
        int totalEarned = 0;
        foreach (var item in inventory)
        {
            totalEarned += item.Value * pricePerTrash;
        }

        // 3. 데이터 갱신 (Backend로 치면 DB Update 직전 단계)
        gold += totalEarned;
        inventory.Clear(); // 데이터 상으로 인벤토리 비우기

        // 4. UI 동기화 (가장 중요!)
        UpdateGoldUI();    // 골드 텍스트 갱신
        UpdateAllUI();     // 인벤토리 & 핫바 슬롯 초기화
        ClearAllUI();      // 남은 잔상 제거 (아이콘 비활성화 등)

        Debug.Log($"{totalEarned} 골드 획득! 현재 총 골드: {gold}");
    }

    // 골드 UI만 따로 갱신하는 함수 (효율성)
    void UpdateGoldUI()
    {
        if (goldText != null)
        {
            // 현업 스타일: 숫자에 콤마(,)를 넣어 가독성을 높입니다 (예: 1,000 Gold)
            goldText.text = $"Gold: {gold:N0}";
        }
    }

    public void BuyItemFromButton(string itemName)
    {
        int price = 0;

        // 아이템 이름에 따라 미리 설정된 가격을 할당합니다.
        if (itemName == "Seed") price = seedPrice;
        else if (itemName == "Battery") price = batteryPrice;

        // 실제 구매 로직 호출 (기존 BuyItem 로직을 재활용)
        ExecutePurchase(itemName, price);
    }

    // 내부적으로 실행되는 구매 로직 (기능 분리 - 유지보수에 좋음)
    private void ExecutePurchase(string itemName, int price)
    {
        // 1. 잔액 확인
        if (gold < price)
        {
            Debug.Log($"골드가 부족합니다! 필요 금액: {price}, 현재 잔액: {gold}");
            return;
        }

        // 2. 인벤토리 공간 확인 (최대 20칸)
        if (inventory.Count >= 20 && !inventory.ContainsKey(itemName))
        {
            Debug.Log("가방이 가득 찼습니다!");
            return;
        }

        // 3. 결제 및 지급
        gold -= price;
        if (inventory.ContainsKey(itemName)) inventory[itemName]++;
        else inventory.Add(itemName, 1);

        // 4. UI 갱신 (반드시 데이터 변경 후 호출)
        UpdateGoldUI();
        UpdateAllUI();

        Debug.Log($"{itemName} 구매 완료! 소모 골드: {price}, 남은 골드: {gold}");
    }
}