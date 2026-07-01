# ================================================================
#  user.py — 유저 정보 조회 라우터
#  담당 기능: DB에서 유저 정보를 읽어서 Unity/Spring에 반환
#  호출 예시: GET http://localhost:8000/api/user/1
# ================================================================

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from sqlalchemy import text
from database import get_db  # database.py에 있는 DB 연결 함수

router = APIRouter()

# ----------------------------------------------------------------
#  GET /api/user/{userId}
#  Unity에서 게임 시작 전 유저 정보 불러올 때 호출
#
#  파라미터 (URL):
#    userId: int — 조회할 유저 ID (예: /api/user/1)
#
#  반환값:
#    userId   : 유저 고유 번호
#    userName : 플레이어 닉네임
#    level    : 캐릭터 레벨
#    battery  : 현재 배터리 잔량
# ----------------------------------------------------------------
@router.get("/user/{userId}")
def get_user(userId: int, db: Session = Depends(get_db)):
    
    # DB에서 해당 userId의 유저 정보 조회
    user = db.execute(
        text("SELECT user_id, user_name, level, battery FROM users WHERE user_id = :uid"),
        {"uid": userId}
    ).fetchone()  # 한 행만 가져옴

    # 유저가 없으면 404 에러 반환
    if not user:
        raise HTTPException(status_code=404, detail="유저를 찾을 수 없습니다")

    # 조회 성공 → 딕셔너리로 변환해서 JSON으로 반환
    return {
        "userId"  : user[0],
        "userName": user[1],
        "level"   : user[2],
        "battery" : user[3]
    }