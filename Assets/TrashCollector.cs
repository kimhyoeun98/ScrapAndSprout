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
        if (inventory.Count == 0)
        {
            Debug.Log("판매할 쓰레기가 없습니다!");
            return;
        }

        int totalEarned = 0;
        foreach (var item in inventory)
        {
            totalEarned += item.Value * pricePerTrash; // 개수 * 가격
        }

        gold += totalEarned;
        inventory.Clear(); // 인벤토리 비우기 

        Debug.Log($"{totalEarned} 골드를 획득했습니다!");

        UpdateGoldUI();
        UpdateAllUI(); // 인벤토리 UI도 함께 새로고침됨
    }

    void UpdateGoldUI()
    {
        if (goldText != null)
            goldText.text = $"Gold: {gold}";
    }
}