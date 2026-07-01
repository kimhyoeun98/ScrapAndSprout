# # ================================================================
# #  npc.py — NPC 대화 라우터
# #  담당 기능: 날씨/오염도에 따라 NPC 대사 반환
# #  ❌ Gemini AI 제거 → 날씨별 고정 대사로 대체
# #
# #  호출 예시:
# #    POST http://localhost:8000/api/npc/dialog
# # ================================================================

# from fastapi import APIRouter, Depends
# from pydantic import BaseModel
# from sqlalchemy.orm import Session
# from sqlalchemy import text
# from database import get_db
# import random

# router = APIRouter()

# # ----------------------------------------------------------------
# #  요청 데이터 구조 — Unity에서 보내는 JSON 형태
# # ----------------------------------------------------------------
# class NpcRequest(BaseModel):
#     userId        : int
#     npcId         : int
#     currentWeather: str   # "acid_rain", "sandstorm", "sunny" 등
#     pollutionLevel: float # 0~100

# # ----------------------------------------------------------------
# #  날씨별 고정 대사 테이블
# # ----------------------------------------------------------------
# NPC_DIALOGS = {
#     "acid_rain": [
#         "이 비 좀 봐... 내 부품 다 녹슬겠어. 빨리 거래하고 들어가자고.",
#         "산성비에 외장이 다 망가지겠어. 제발 빨리 끝내자.",
#     ],
#     "sandstorm": [
#         "앞이 하나도 안 보이잖아! 거래할 거면 빨리 말해.",
#         "황사 때문에 눈을 못 뜨겠어. 거래? 빨리 해.",
#     ],
#     "sunny": [
#         "오늘 날씨 좋네! 어서 와, 뭐가 필요해?",
#         "맑은 날엔 기분이 좋아. 좋은 물건 있으면 가져와봐!",
#     ],
#     "cloudy": [
#         "흐린 날엔 기분도 좀 칙칙하지. 그래도 거래는 해줄게.",
#         "구름이 많네. 뭐 필요한 거 있어?",
#     ],
# }

# # 날씨별 가격 배율
# PRICE_MODIFIERS = {
#     "acid_rain" : 0.7,
#     "sandstorm" : 0.8,
#     "cloudy"    : 0.9,
#     "sunny"     : 1.0,
# }

# # ----------------------------------------------------------------
# #  POST /api/npc/dialog — NPC 대사 요청
# #  Unity에서 NPC에게 말 걸 때 호출
# #
# #  반환값:
# #    dialog        : NPC 대사 텍스트
# #    priceModifier : 거래 가격 배율 (0.7~1.0)
# # ----------------------------------------------------------------
# @router.post("/npc/dialog")
# def get_npc_dialog(req: NpcRequest, db: Session = Depends(get_db)):

#     # DB에서 유저 존재 확인
#     user = db.execute(
#         text("SELECT user_id FROM users WHERE user_id = :uid"),
#         {"uid": req.userId}
#     ).fetchone()

#     if not user:
#         return {"dialog": "누구세요? 등록된 유저가 아닌 것 같은데요.", "priceModifier": 1.0}

#     # 해당 날씨 대사 목록에서 랜덤 선택 — 없으면 기본 대사
#     dialogs = NPC_DIALOGS.get(req.currentWeather, ["어서오게, 거래할 물건이 있나?"])
#     dialog  = random.choice(dialogs)

#     # 가격 배율 — 없으면 기본값 1.0
#     price_modifier = PRICE_MODIFIERS.get(req.currentWeather, 1.0)

#     return {
#         "dialog"       : dialog,
#         "priceModifier": price_modifier
#     }