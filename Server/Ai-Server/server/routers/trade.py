# ================================================================
#  trade.py — 거래 라우터
#  담당 기능:
#    1. 아이템 판매 → 골드 획득 + 수거 카운트 + 오염도 감소
#    2. 아이템 구매 → 골드 차감 + 인벤토리 추가
#    3. 씨앗 식재  → 나무 카운트 + 오염도 감소
#    4. 플레이어 정보 조회
#
#  호출 예시:
#    POST http://localhost:8000/api/trade/sell
#    POST http://localhost:8000/api/trade/buy
#    POST http://localhost:8000/api/plant
#    GET  http://localhost:8000/api/player/{playerId}
# ================================================================

from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session
from sqlalchemy import text
from database import get_db
from typing import List


router = APIRouter()

# ----------------------------------------------------------------
#  아이템 가격표
#  Unity TrashCollector.cs의 pricePerTrash 와 맞춰서 관리
# ----------------------------------------------------------------
ITEM_PRICES = {
    "can_0"    : 10,
    "banana_0" : 20,
    "Seed"     : 30,
    "plastic_0": 15,
    "Battery"  : 50,   # ✅ 추가 — TrashCollector.cs에서 구매 가능
}

# ----------------------------------------------------------------
#  요청 데이터 구조 — Unity에서 보내는 JSON 형태
# ----------------------------------------------------------------

class SellRequest(BaseModel):
    playerId  : str        # 플레이어 고유 ID (예: "player-001")
    sessionId : str        # 현재 게임 세션 ID
    itemNames : List[str]  # 판매할 아이템 이름 목록
    itemCounts: List[int]  # 아이템별 판매 수량

class BuyRequest(BaseModel):
    playerId : str   # 플레이어 고유 ID
    sessionId: str   # 현재 게임 세션 ID
    itemName : str   # 구매할 아이템 이름
    quantity : int   # 구매 수량

class PlantRequest(BaseModel):
    playerId : str   # 플레이어 고유 ID
    sessionId: str   # 현재 게임 세션 ID
    posX     : float # 식재 위치 x좌표
    posY     : float # 식재 위치 y좌표


# ----------------------------------------------------------------
#  POST /api/trade/sell — 아이템 판매
#  Unity에서 NPC에게 쓰레기 판매 시 호출
#
#  처리 순서:
#    1. 아이템별 가격 계산 → 총 골드 산출
#    2. SESSION_PLAYERS gold 업데이트
#    3. ✅ MAPS picked_trash_count 업데이트 (수거 카운트 반영)
#    4. ✅ 오염도 재계산 (수거량 + 나무 수 기반)
#
#  반환값:
#    success : 성공 여부
#    gold    : 판매 후 현재 보유 골드
#    message : 획득 골드 안내
# ----------------------------------------------------------------
@router.post("/trade/sell")
def sell_items(req: SellRequest, db: Session = Depends(get_db)):

    # 1. 아이템별 가격 계산 → 총 골드 + 총 판매 수량 산출
    total_gold  = 0
    total_count = 0  # 오염도 감소 계산에 사용

    for name, count in zip(req.itemNames, req.itemCounts):
        price = ITEM_PRICES.get(name, 10)  # 가격표에 없으면 기본값 10
        total_gold  += price * count
        total_count += count

    # 2. SESSION_PLAYERS 테이블 gold 업데이트
    db.execute(
        text("""
            UPDATE SESSION_PLAYERS
            SET gold = gold + :gold
            WHERE player_id = :pid AND session_id = :sid
        """),
        {"gold": total_gold, "pid": req.playerId, "sid": req.sessionId}
    )

    # 3. ✅ MAPS 테이블 수거 카운트 업데이트
    #    개요서 F08 — 누적 수거량이 악천후 확률 감소에 연동됨
    db.execute(
        text("""
            UPDATE MAPS
            SET picked_trash_count = picked_trash_count + :count
            WHERE session_id = :sid
        """),
        {"count": total_count, "sid": req.sessionId}
    )

    # 4. ✅ 오염도 재계산
    #    공식: 오염도 = 100 - (수거량 * 0.5) - (나무 수 * 1.0)
    map_data = db.execute(
        text("SELECT picked_trash_count, tree_count FROM MAPS WHERE session_id = :sid"),
        {"sid": req.sessionId}
    ).fetchone()

    if map_data:
        new_pollution = max(0, 100 - (map_data[0] * 0.5) - (map_data[1] * 1.0))
        db.execute(
            text("UPDATE MAPS SET pollution_level = :pollution WHERE session_id = :sid"),
            {"pollution": new_pollution, "sid": req.sessionId}
        )

    db.commit()  # 위 모든 변경사항 DB에 확정 저장

    # 판매 후 현재 골드 조회 → Unity에 반환
    result = db.execute(
        text("SELECT gold FROM SESSION_PLAYERS WHERE player_id = :pid AND session_id = :sid"),
        {"pid": req.playerId, "sid": req.sessionId}
    ).fetchone()

    return {
        "success": True,
        "gold"   : result[0] if result else total_gold,
        "message": f"{total_gold}골드 획득"
    }


# ----------------------------------------------------------------
#  POST /api/trade/buy — 아이템 구매
#  Unity에서 NPC에게 아이템 구매 시 호출
#
#  처리 순서:
#    1. 구매 가격 계산
#    2. 보유 골드 확인 → 부족 시 실패 반환
#    3. SESSION_PLAYERS gold 차감
#    4. SESSION_PLAYERS의 session_player_id 조회
#    5. INVENTORY 에 아이템 추가 (있으면 수량 증가)
#
#  반환값:
#    success : 성공 여부
#    gold    : 구매 후 현재 보유 골드
#    message : 구매 결과 안내
# ----------------------------------------------------------------
@router.post("/trade/buy")
def buy_item(req: BuyRequest, db: Session = Depends(get_db)):

    # 1. 구매 총 가격 계산
    price = ITEM_PRICES.get(req.itemName, 30) * req.quantity

    # 2. 현재 보유 골드 확인
    result = db.execute(
        text("SELECT gold FROM SESSION_PLAYERS WHERE player_id = :pid AND session_id = :sid"),
        {"pid": req.playerId, "sid": req.sessionId}
    ).fetchone()

    # 골드 부족 시 구매 실패 반환
    if not result or result[0] < price:
        return {
            "success": False,
            "gold"   : result[0] if result else 0,
            "message": "Gold 부족"
        }

    # 3. gold 차감
    db.execute(
        text("""
            UPDATE SESSION_PLAYERS
            SET gold = gold - :price
            WHERE player_id = :pid AND session_id = :sid
        """),
        {"price": price, "pid": req.playerId, "sid": req.sessionId}
    )

    # 4. session_player_id 조회 — INVENTORY FK로 필요
    sp = db.execute(
        text("SELECT session_player_id FROM SESSION_PLAYERS WHERE player_id = :pid AND session_id = :sid"),
        {"pid": req.playerId, "sid": req.sessionId}
    ).fetchone()

    # 5. INVENTORY 업데이트 — 아이템 있으면 수량 증가, 없으면 새로 추가
    if sp:
        db.execute(
            text("""
                INSERT INTO INVENTORY (inventory_id, session_player_id, item_name, quantity)
                VALUES (:inv_id, :sp_id, :item, :qty)
                ON DUPLICATE KEY UPDATE quantity = quantity + :qty
            """),
            {
                "inv_id": f"inv-{req.playerId}-{req.itemName}",
                "sp_id" : sp[0],
                "item"  : req.itemName,
                "qty"   : req.quantity
            }
        )

    db.commit()  # 변경사항 DB에 확정 저장

    # 구매 후 현재 골드 조회 → Unity에 반환
    result = db.execute(
        text("SELECT gold FROM SESSION_PLAYERS WHERE player_id = :pid AND session_id = :sid"),
        {"pid": req.playerId, "sid": req.sessionId}
    ).fetchone()

    return {
        "success": True,
        "gold"   : result[0],
        "message": f"{req.itemName} 구매 완료"
    }


# ----------------------------------------------------------------
#  POST /api/plant — 씨앗 식재
#  Unity에서 F키로 씨앗 심을 때 호출
#
#  처리 순서:
#    1. MAPS 나무 카운트 + 1
#    2. 오염도 재계산 (수거량 + 나무 수 기반)
#    3. SESSION_PLAYERS 식재 수 + 1
#
#  반환값:
#    success   : 성공 여부
#    treeCount : 현재 총 나무 수
#    message   : 식재 완료 안내
# ----------------------------------------------------------------
@router.post("/plant")
def plant_seed(req: PlantRequest, db: Session = Depends(get_db)):

    # 1. MAPS 테이블 나무 카운트 + 1
    db.execute(
        text("UPDATE MAPS SET tree_count = tree_count + 1 WHERE session_id = :sid"),
        {"sid": req.sessionId}
    )

    # 2. 오염도 재계산
    #    공식: 오염도 = 100 - (수거량 * 0.5) - (나무 수 * 1.0)
    map_data = db.execute(
        text("SELECT picked_trash_count, tree_count FROM MAPS WHERE session_id = :sid"),
        {"sid": req.sessionId}
    ).fetchone()

    if map_data:
        new_pollution = max(0, 100 - (map_data[0] * 0.5) - (map_data[1] * 1.0))
        db.execute(
            text("UPDATE MAPS SET pollution_level = :pollution WHERE session_id = :sid"),
            {"pollution": new_pollution, "sid": req.sessionId}
        )

    # 3. SESSION_PLAYERS 식재 수 + 1 (결과 화면 MVP 계산에 사용)
    db.execute(
        text("""
            UPDATE SESSION_PLAYERS
            SET total_trees_planted = total_trees_planted + 1
            WHERE player_id = :pid AND session_id = :sid
        """),
        {"pid": req.playerId, "sid": req.sessionId}
    )

    db.commit()  # 변경사항 DB에 확정 저장

    # 현재 나무 수 조회 → Unity에 반환
    result = db.execute(
        text("SELECT tree_count FROM MAPS WHERE session_id = :sid"),
        {"sid": req.sessionId}
    ).fetchone()

    return {
        "success"  : True,
        "treeCount": result[0] if result else 0,
        "message"  : "식재 완료"
    }


# ----------------------------------------------------------------
#  GET /api/player/{playerId} — 플레이어 정보 조회
#  Unity에서 HUD 갱신 시 호출
#
#  처리 순서:
#    1. SESSION_PLAYERS에서 gold + session_id 조회
#    2. ✅ 해당 session_id 기반으로 MAPS tree_count 조회
#
#  반환값:
#    playerId  : 플레이어 ID
#    gold      : 현재 보유 골드
#    treeCount : 현재 세션의 나무 수
# ----------------------------------------------------------------
@router.get("/player/{playerId}")
def get_player(playerId: str, db: Session = Depends(get_db)):

    # 1. 플레이어 골드 + 세션 ID 조회
    player = db.execute(
        text("SELECT gold, session_id FROM SESSION_PLAYERS WHERE player_id = :pid"),
        {"pid": playerId}
    ).fetchone()

    # 2. ✅ session_id 기반으로 MAPS 조회 (기존 LIMIT 1 버그 수정)
    maps = db.execute(
        text("SELECT tree_count FROM MAPS WHERE session_id = :sid"),
        {"sid": player[1] if player else ""}
    ).fetchone()

    return {
        "playerId" : playerId,
        "gold"     : player[0] if player else 0,
        "treeCount": maps[0]   if maps   else 0
    }