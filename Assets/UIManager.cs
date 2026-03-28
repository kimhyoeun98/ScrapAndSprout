using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // 스크립트에서 제어할 UI 오브젝트들
    public GameObject inventoryPanel; // 전체 가방
    public GameObject hotbar;         // 화면 하단 리스트

    private bool isInventoryOpen = false; // 현재 가방이 열려 있는지 확인

    void Update()
    {
        // 'I'키 입력 확인
        if (Input.GetKeyDown(KeyCode.I))
        {
            ToggleInventory();
        }
    }

    // 인벤토리 열기/닫기
    void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen; // 상태 반전

        // 인벤토리가 열리면 핫바를 끄고, 가방을 켬
        if (isInventoryOpen)
        {
            inventoryPanel.SetActive(true);
            hotbar.SetActive(false);
        }
        else // 닫히면 반대로
        {
            inventoryPanel.SetActive(false);
            hotbar.SetActive(true);
        }
    }

    // (미구현 PoC) 여기에 나중에 데이터를 UI 칸에 채워 넣는 코드를 작성합니다.
    // public void UpdateUI(Dictionary<string, int> inventoryData) { ... }
}