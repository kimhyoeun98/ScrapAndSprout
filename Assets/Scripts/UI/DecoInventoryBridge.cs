using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TrashZoneScene ↔ DecoScene 전환 시 게임 상태 보존.
/// </summary>
public static class DecoInventoryBridge
{
    public struct PlacedDeco
    {
        public string  itemName;
        public Vector3 position;
        public bool    flipped;
    }

    // 인벤토리 / 골드
    public static Dictionary<string, int> SavedInventory  { get; private set; } = new();
    public static int                     SavedGold        { get; private set; }

    // 배치 아이템 (DecoScene 재진입 복원)
    public static List<PlacedDeco>        SavedDecorations { get; private set; } = new();

    // TrashZoneScene 복귀 상태
    public static bool    IsReturningFromDeco { get; private set; }
    public static Vector3 ReturnPosition      { get; private set; }
    public static float   SavedElapsedTime    { get; private set; }
    public static int     SavedDecorScore     { get; private set; }

    // ─── 저장 ───────────────────────────────

    public static void SaveFrom(TrashCollector collector)
    {
        SavedInventory = new Dictionary<string, int>(collector.inventory);
        SavedGold      = collector.gold;
    }

    public static void AddDeco(string itemName, Vector3 position, bool flipped)
    {
        SavedDecorations.Add(new PlacedDeco { itemName = itemName, position = position, flipped = flipped });
    }

    /// <summary>DecoScene 입장 전 호출 — 텔포 위치 + 타이머 + 점수 보존</summary>
    public static void SaveReturnState(Vector3 playerPos, float elapsedTime, int decorScore)
    {
        ReturnPosition   = playerPos;
        SavedElapsedTime = elapsedTime;
        SavedDecorScore  = decorScore;
    }

    /// <summary>TrashZoneScene 복귀 직전 호출 — 인벤토리 최신화 + 복귀 플래그 설정</summary>
    public static void PrepareReturn(TrashCollector collector)
    {
        SaveFrom(collector);
        IsReturningFromDeco = true;
    }

    /// <summary>collector를 못 찾았을 때: 기존 SavedInventory 유지하고 복귀 플래그만 설정</summary>
    public static void MarkReturning()
    {
        IsReturningFromDeco = true;
    }

    public static void ClearReturnFlag() => IsReturningFromDeco = false;

    // ─── 복원 ───────────────────────────────

    public static void RestoreTo(TrashCollector collector)
    {
        if (SavedInventory.Count > 0)
            collector.inventory = new Dictionary<string, int>(SavedInventory);
        if (SavedGold > 0)
            collector.RPC_SetGold(SavedGold);
    }

    public static void ClearDecorations() => SavedDecorations.Clear();

    public static bool HasData => SavedInventory.Count > 0;
}
