# ================================================================
#  main.py — FastAPI 서버 진입점
#  서버를 시작하면 이 파일이 가장 먼저 실행됨
#
#  실행 명령어:
#    uvicorn main:app --reload
#
#  API 문서 자동 생성:
#    http://localhost:8000/docs
# ================================================================

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

# ----------------------------------------------------------------
#  라우터 import
#  ❌ gemini(npc) 제거 → npc는 고정 대사로 대체해서 다시 연결
# ----------------------------------------------------------------
from routers import weather, trade, session, user, pcg

app = FastAPI(title="Scrap & Sprout AI Server")

# ----------------------------------------------------------------
#  CORS 설정 — Unity, Spring에서 접근 허용
# ----------------------------------------------------------------
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ----------------------------------------------------------------
#  라우터 등록
#
#  GET  /api/user/{userId}      ← 유저 정보 조회
#  POST /api/session/start      ← 게임 시작
#  POST /api/session/end        ← 게임 종료

#  POST /api/trade/sell         ← 아이템 판매
#  POST /api/trade/buy          ← 아이템 구매
#  POST /api/plant              ← 씨앗 식재
#  POST /ai/weather             ← 날씨 계산
# ----------------------------------------------------------------
app.include_router(weather.router, prefix="/ai")
app.include_router(trade.router,   prefix="/api")
app.include_router(session.router, prefix="/api")
app.include_router(user.router,    prefix="/api")
app.include_router(pcg.router, prefix="/ai")

# ----------------------------------------------------------------
#  헬스체크 — 서버 살아있는지 확인
#  GET http://localhost:8000/
# ----------------------------------------------------------------
@app.get("/")
def health_check():
    return {"status": "ok", "server": "Scrap & Sprout FastAPI"}
#서버 실행
#uvicorn main:app --reload --host 0.0.0.0 --port 8000