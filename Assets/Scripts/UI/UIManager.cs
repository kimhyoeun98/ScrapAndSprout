using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    //  싱글톤 (어디서든 UIManager.Instance로 접근 가능)
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
    public GameObject pickText; // ← Hierarchy의 'pick text' 드래그 연결!

    [Header("배터리 UI")]
    public RectTransform batteryFillMask;  // 배터리 마스크 (크기 조절용) ⭐ 추가!
    public Image batteryFillImage;         // 배터리 Fill Image
    public TextMeshProUGUI batteryText;    // 배터리 텍스트 (선택사항)

    private float _batteryMaxWidth = 200f;  // 배터리 바 최대 너비 ⭐ 추가!

    // 배터리 색상 (상태별)
    private Color _batteryColorHigh = new Color(0f, 1f, 0f);      // 초록 (70% 이상)
    private Color _batteryColorMid = new Color(1f, 1f, 0f);       // 노랑 (30~70%)
    private Color _batteryColorLow = new Color(1f, 0f, 0f);       // 빨강 (30% 이하)

    // ─────────────────────────────────────────
    //  내부 상태
    // ─────────────────────────────────────────
    private bool _isInventoryOpen = false;

    // 동시에 여러 쓰레기가 범위 안에 있어도 안전하게 관리
    private int _nearbyTrashCount = 0;

    // ─────────────────────────────────────────
    //  Unity 생명주기
    // ─────────────────────────────────────────
    void Start()
    {
        // 시작 시 pick text 숨기기
        if (pickText != null)
            pickText.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            ToggleInventory();
    }

    // ─────────────────────────────────────────
    //  인벤토리
    // ─────────────────────────────────────────
    public void ToggleInventory()
    {
        _isInventoryOpen = !_isInventoryOpen;
        if (inventoryPanel != null) inventoryPanel.SetActive(_isInventoryOpen);
        if (hotbar != null) hotbar.SetActive(!_isInventoryOpen);
    }

    public void CloseInventory()
    {
        _isInventoryOpen = false;
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (hotbar != null) hotbar.SetActive(true);
    }


    // ─────────────────────────────────────────
    //  배터리 UI 업데이트
    // ─────────────────────────────────────────

    /// <summary>
    /// 배터리 게이지를 업데이트합니다 (Simple + Width 조절 방식)
    /// </summary>
    /// <param name="currentBattery">현재 배터리 (0~100)</param>
    /// <param name="maxBattery">최대 배터리 (기본 100)</param>
    public void UpdateBatteryUI(float currentBattery, float maxBattery = 100f)
    {
        // 1. 배터리 비율 계산 (0.0 ~ 1.0)
        float fillRatio = Mathf.Clamp01(currentBattery / maxBattery);

        // 2. BatteryFillMask의 너비 조절
        if (batteryFillMask != null)
        {
            // 첫 실행 시 최대 너비 자동 저장
            if (_batteryMaxWidth <= 0f)
            {
                _batteryMaxWidth = batteryFillMask.sizeDelta.x;
            }

            // 너비 조절 (왼쪽에서 오른쪽으로 줄어듦)
            float newWidth = _batteryMaxWidth * fillRatio;
            batteryFillMask.sizeDelta = new Vector2(newWidth, batteryFillMask.sizeDelta.y);
        }

        // 3. 텍스트 업데이트
        int percentage = Mathf.RoundToInt(fillRatio * 100f);
        if (batteryText != null)
        {
            batteryText.text = $"{percentage}%";
        }

        // 4. 색상 변경 (배터리 잔량에 따라)
        if (batteryFillImage != null)
        {
            if (percentage >= 70)
            {
                batteryFillImage.color = _batteryColorHigh; // 초록
            }
            else if (percentage >= 30)
            {
                batteryFillImage.color = _batteryColorMid;  // 노랑
            }
            else
            {
                batteryFillImage.color = _batteryColorLow;  // 빨강
            }
        }
    }

    // ─────────────────────────────────────────
    //  [E] 줍기 텍스트 표시 관리
    // ─────────────────────────────────────────

    /// <summary>
    /// 쓰레기 범위에 들어올 때 호출.
    /// 카운터를 올려서 pick text를 표시합니다.
    /// </summary>
    public void OnTrashEnter()
    {
        _nearbyTrashCount++;
        if (pickText != null)
            pickText.SetActive(true);

        Debug.Log($"[UIManager] OnTrashEnter — 범위 내 쓰레기: {_nearbyTrashCount}개");
    }

    /// <summary>
    /// 쓰레기 범위에서 벗어날 때 호출.
    /// 모든 쓰레기에서 벗어났을 때만 pick text를 숨깁니다.
    /// </summary>
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

    /// <summary>
    /// 수거 완료 시 카운터 초기화 및 텍스트 숨김.
    /// </summary>
    public void OnTrashCollected()
    {
        _nearbyTrashCount = 0;
        if (pickText != null)
            pickText.SetActive(false);
    }

    // ─────────────────────────────────────────
    //  기존 호환용 (다른 스크립트에서 호출 중인 경우)
    // ─────────────────────────────────────────
    public void ShowStatusMessage(string message, float duration)
    {
        if (string.IsNullOrEmpty(message))
        {
            OnTrashExit();  // ← 카운터 감소
            return;
        }
        // ⭐ pick text랑 완전히 분리 — 아무것도 안 함
        // (TrashItem이 직접 OnTrashEnter/Exit 호출하므로 여기선 건드리지 않음)
        Debug.Log($"[UIManager] ShowStatusMessage: {message}");
    }
}