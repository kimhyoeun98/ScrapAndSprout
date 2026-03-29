using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject interactionUI; // "[E] 상점 열기" 안내 메시지
    public GameObject shopPanel;    // 상점 팝업창 패널

    private bool isPlayerNearby = false;
    private TrashCollector playerCollector;

    void Update()
    {
        // 1. 근처에 있고 E키를 누르면 상점 창을 토글(열기/닫기)
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            ToggleShop();
        }
        // 플레이어가 근처에 있고(isPlayerNearby) F키를 눌렀는가?
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.F))
        {
            // 여기서 직접 판매 함수를 호출합니다! (On Click이 필요 없는 이유)
            playerCollector.SellAllTrash();

            Debug.Log("NPC와 상호작용하여 판매를 완료했습니다.");
        }
    }

    public void ToggleShop()
    {
        if (shopPanel == null) return;

        // 현재 꺼져있으면 켜고, 켜져있으면 끕니다.
        bool isActive = !shopPanel.activeSelf;
        shopPanel.SetActive(isActive);

        if (isActive)
        {
            // 상점이 열릴 때 안내 UI는 잠시 숨깁니다.
            interactionUI.SetActive(false);

            // [현업 꿀팁] 상점이 열리면 마우스 커서를 보이게 설정합니다.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            interactionUI.SetActive(true);
            // 상점이 닫히면 다시 마우스를 숨기거나 고정할 수 있습니다.
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            playerCollector = other.GetComponent<TrashCollector>();
            if (interactionUI != null && !shopPanel.activeSelf)
                interactionUI.SetActive(true);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (shopPanel != null) shopPanel.SetActive(false);
            if (interactionUI != null) interactionUI.SetActive(false);
        }
    }
}