# ================================================================
#  session.py — 게임 세션 라우터
#  담당 기능:
#    1. 게임 시작 시 → 유저 배터리 초기화 + 새 맵 레코드 생성
#    2. 게임 종료 시 → 결과값(쓰레기/나무/배터리) DB에 저장
#
#  호출 예시:
#    POST http://localhost:8000/api/session/start
#    POST http://localhost:8000/api/session/end
# ================================================================

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session
from sqlalchemy import text
from database import get_db  # database.py에 있는 DB 연결 함수

router = APIRouter()

# ----------------------------------------------------------------
#  요청 데이터 구조 정의 (Unity에서 보내는 JSON 형태)
# ----------------------------------------------------------------

# 게임 시작 요청 — Unity에서 보내는 데이터
class GameStartRequest(BaseModel):
    userId: int         # 시작하는 유저 ID

# 게임 종료 요청 — Unity에서 보내는 데이터
class GameEndRequest(BaseModel):
    userId      : int   # 종료하는 유저 ID
    trashCount  : int   # 이번 게임에서 수거한 쓰레기 수
    treeCount   : int   # 이번 게임에서 심은 나무 수
    finalBattery: float # 게임 종료 시점의 배터리 잔량


# ----------------------------------------------------------------
#  POST /api/session/start — 게임 시작
#  Unity에서 게임 씬 로드 완료 후 호출
#
#  처리 순서:
#    1. 유저 존재 여부 확인
#    2. 유저 배터리를 100으로 초기화
#    3. 새 맵 레코드 생성 (게임 1판 = 맵 1개)
#
#  반환값:
#    success  : 성공 여부
#    mapId    : 생성된 맵 ID (이번 게임 세션 식별자)
#    userName : 유저 닉네임
#    battery  : 초기화된 배터리 (항상 100.0)
#    message  : 상태 메시지
# ----------------------------------------------------------------
@router.post("/session/start")
def game_start(req: GameStartRequest, db: Session = Depends(get_db)):

    # 1. 유저 존재 확인
    user = db.execute(
        text("SELECT user_id, user_name FROM users WHERE user_id = :uid"),
        {"uid": req.userId}
    ).fetchone()

    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다")

    # 2. 유저 배터리 100으로 초기화
    db.execute(
        text("UPDATE users SET battery = 100.0 WHERE user_id = :uid"),
        {"uid": req.userId}
    )

    # 3. 새 맵(세션) 레코드 생성 — 게임 1판 시작
    db.execute(
        text("""
            INSERT INTO maps (elapsed_days, picked_trash_count, tree_count)
            VALUES (0, 0, 0)
        """)
    )
    db.commit()  # 위 변경사항 DB에 확정 저장

    # 방금 만든 맵의 ID 가져오기
    new_map = db.execute(
        text("SELECT map_id FROM maps ORDER BY map_id DESC LIMIT 1")
    ).fetchone()

    return {
        "success" : True,
        "mapId"   : new_map[0],
        "userName": user[1],
        "battery" : 100.0,
        "message" : "게임 시작!"
    }


# ----------------------------------------------------------------
#  POST /api/session/end — 게임 종료
#  Unity에서 게임 오버 or 클리어 시 호출
#
#  처리 순서:
#    1. 유저 배터리 최종값 저장
#    2. 가장 최근 맵에 수거/식재 결과 저장
#
#  반환값:
#    success : 성공 여부
#    message : 저장된 결과 요약
# ----------------------------------------------------------------
@router.post("/session/end")
def game_end(req: GameEndRequest, db: Session = Depends(get_db)):

    # 1. 유저 배터리 최종값 업데이트
    db.execute(
        text("UPDATE users SET battery = :bat WHERE user_id = :uid"),
        {"bat": req.finalBattery, "uid": req.userId}
    )

    # 2. 가장 최근 맵에 게임 결과 저장
    #    서브쿼리로 최신 map_id를 찾아서 업데이트
    db.execute(
        text("""
            UPDATE maps
            SET picked_trash_count = :trash,
                tree_count         = :tree
            WHERE map_id = (
                SELECT map_id FROM (
                    SELECT MAX(map_id) AS map_id FROM maps
                ) AS m
            )
        """),
        {"trash": req.trashCount, "tree": req.treeCount}
    )
    db.commit()  # 변경사항 DB에 확정 저장

    return {
        "success": True,
        "message": f"게임 종료! 쓰레기:{req.trashCount}개 | 나무:{req.treeCount}그루 | 배터리:{req.finalBattery}"
    }