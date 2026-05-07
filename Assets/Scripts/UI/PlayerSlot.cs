using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerSlot : MonoBehaviour
{
    [Header("UI Elements")]
    public Image characterIcon;
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI readyStatusText;
    public Button selectCharacterButton;

    private int slotIndex;
    private WaitingRoomManager manager;
    private bool isLocalPlayer = false;
    private bool isAIBot = false;

    public void Initialize(int index, WaitingRoomManager waitingRoomManager)
    {
        slotIndex = index;
        manager = waitingRoomManager;
        selectCharacterButton.onClick.AddListener(OnSelectCharacterClicked);
    }

    public void SetPlayer(string playerName, bool isLocal)
    {
        isLocalPlayer = isLocal;
        isAIBot = false;

        playerNameText.text = playerName;
        readyStatusText.text = "대기 중...";
        readyStatusText.color = Color.gray;

        selectCharacterButton.interactable = isLocal;
    }

    public void SetAIBot()
    {
        isAIBot = true;
        isLocalPlayer = false;

        playerNameText.text = "AI Bot";
        readyStatusText.text = "준비 완료!";
        readyStatusText.color = Color.green;

        selectCharacterButton.interactable = false;
    }

    public void SetCharacter(string characterName, Sprite sprite)
    {
        characterIcon.sprite = sprite;
        characterIcon.color = Color.white;

        Debug.Log("Slot " + slotIndex + ": " + characterName + " 설정됨");
    }

    public void UpdateReadyStatus(bool isReady)
    {
        readyStatusText.text = isReady ? "준비 완료!" : "대기 중...";
        readyStatusText.color = isReady ? Color.green : Color.gray;
    }

    void OnSelectCharacterClicked()
    {
        if (manager != null)
        {
            manager.OpenCharacterSelectPanel(slotIndex);
        }
    }
}