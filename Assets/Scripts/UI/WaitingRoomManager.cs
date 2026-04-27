using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class WaitingRoomManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI roomNameText;
    public Button exitButton;

    [Header("Player Slots")]
    public PlayerSlot[] playerSlots;

    [Header("Character Select Panel")]
    public GameObject characterSelectPanel;
    public Button alphaButton;
    public Button betaButton;
    public Button gammaButton;
    public Button deltaButton;

    [Header("Footer Buttons")]
    public Button readyButton;
    public Button startButton;

    [Header("Character Sprites")]
    public Sprite alphaSprite;
    public Sprite betaSprite;
    public Sprite gammaSprite;
    public Sprite deltaSprite;

    private int currentSelectingSlotIndex = -1;
    private List<string> selectedCharacters = new List<string>();
    private bool isReady = false;

    void Start()
    {
        InitializeUI();
        SetupButtons();

        roomNameText.text = "방이름: 테스트 룸";

        // Player 1 = 나
        playerSlots[0].SetPlayer("Player 1", true);

        // 나머지 = AI 봇
        for (int i = 1; i < playerSlots.Length; i++)
        {
            playerSlots[i].SetAIBot();
        }
    }

    void InitializeUI()
    {
        characterSelectPanel.SetActive(false);

        for (int i = 0; i < playerSlots.Length; i++)
        {
            playerSlots[i].Initialize(i, this);
        }
    }

    void SetupButtons()
    {
        exitButton.onClick.AddListener(OnExitButtonClicked);

        alphaButton.onClick.AddListener(() => OnCharacterSelected("Alpha"));
        betaButton.onClick.AddListener(() => OnCharacterSelected("Beta"));
        gammaButton.onClick.AddListener(() => OnCharacterSelected("Gamma"));
        deltaButton.onClick.AddListener(() => OnCharacterSelected("Delta"));

        readyButton.onClick.AddListener(OnReadyButtonClicked);
        startButton.onClick.AddListener(OnStartButtonClicked);
    }

    public void OpenCharacterSelectPanel(int slotIndex)
    {
        currentSelectingSlotIndex = slotIndex;
        characterSelectPanel.SetActive(true);
        UpdateCharacterButtonStates();
    }

    void UpdateCharacterButtonStates()
    {
        alphaButton.interactable = !selectedCharacters.Contains("Alpha");
        betaButton.interactable = !selectedCharacters.Contains("Beta");
        gammaButton.interactable = !selectedCharacters.Contains("Gamma");
        deltaButton.interactable = !selectedCharacters.Contains("Delta");
    }

    void OnCharacterSelected(string characterName)
    {
        if (currentSelectingSlotIndex == -1) return;

        // 이전 선택 제거
        if (selectedCharacters.Count > 0)
        {
            selectedCharacters.RemoveAt(0);
        }

        // 새 캐릭터 선택
        selectedCharacters.Add(characterName);

        // UI 업데이트
        Sprite characterSprite = GetCharacterSprite(characterName);
        playerSlots[currentSelectingSlotIndex].SetCharacter(characterName, characterSprite);

        // 패널 닫기
        characterSelectPanel.SetActive(false);
        currentSelectingSlotIndex = -1;

        Debug.Log(characterName + " 캐릭터 선택!");
    }

    Sprite GetCharacterSprite(string characterName)
    {
        if (characterName == "Alpha") return alphaSprite;
        if (characterName == "Beta") return betaSprite;
        if (characterName == "Gamma") return gammaSprite;
        if (characterName == "Delta") return deltaSprite;
        return null;
    }

    void OnReadyButtonClicked()
    {
        isReady = !isReady;

        TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        buttonText.text = isReady ? "준비 취소" : "준비 완료";

        playerSlots[0].UpdateReadyStatus(isReady);

        Debug.Log("준비 상태: " + isReady);
    }

    void OnStartButtonClicked()
    {
        if (!isReady)
        {
            Debug.Log("먼저 준비를 완료해주세요!");
            return;
        }

        Debug.Log("게임 시작!");
        SceneManager.LoadScene("MainGame");
    }

    void OnExitButtonClicked()
    {
        Debug.Log("방 나가기!");
        SceneManager.LoadScene("LobbyScene");
    }
}