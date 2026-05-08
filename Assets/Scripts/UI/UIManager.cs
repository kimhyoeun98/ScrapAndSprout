using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤
    // ─────────────────────────────────────────
    public static UIManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ─────────────────────────────────────────
    //  인스펙터 연결
    // ─────────────────────────────────────────
    [Header("인벤토리")]
    public GameObject inventoryPanel;
    public GameObject hotbar;

    [Header("[E] 줍기 안내 텍스트")]
    public GameObject pickText;

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────
    private bool _isInventoryOpen = false;
    private int _nearbyTrashCount = 0;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        // pick text 숨기기
        if (pickText != null)
            pickText.SetActive(false);

        // 핫바 활성화, 인벤토리 비활성화
        if (hotbar != null)
            hotbar.SetActive(true);
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);

    
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    // ─────────────────────────────────────────
    //  인벤토리 토글
    // ─────────────────────────────────────────
    void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(_isInventoryOpen);
        if (hotbar != null) hotbar.SetActive(!_isInventoryOpen);
    }

    // ─────────────────────────────────────────
    //  쓰레기 줍기 텍스트 관리
    // ─────────────────────────────────────────
    public void OnTrashEnter()
    {
        _nearbyTrashCount++;
        if (pickText != null)
            pickText.SetActive(true);
        Debug.Log($"[UIManager] OnTrashEnter — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashExit()
    {
        _nearbyTrashCount--;
        if (_nearbyTrashCount <= 0)
        {
            _nearbyTrashCount = 0;
            if (pickText != null)
                pickText.SetActive(false);
        }
        Debug.Log($"[UIManager] OnTrashExit — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    public void OnTrashCollected()
    {
        _nearbyTrashCount = 0;
        if (pickText != null)
            pickText.SetActive(false);
    }

    public void ShowStatusMessage(string message, float duration)
    {
        if (string.IsNullOrEmpty(message))
        {
            OnTrashExit();
            return;
        }
        Debug.Log($"[UIManager] ShowStatusMessage: {message}");
    }
}