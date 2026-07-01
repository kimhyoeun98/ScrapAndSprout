# ================================================================
#  pcg.py — PCG 맵 생성 라우터
#  POST /ai/pcg/map
# ================================================================

from fastapi import APIRouter
from pydantic import BaseModel
from services.gemini_service import generate_map

router = APIRouter()

class PcgRequest(BaseModel):
    pollutionLevel: float = 70.0  # 초기 오염도 (SRS 기본값 70)
    mapWidth      : int   = 30    # 맵 가로 타일 수
    mapHeight     : int   = 20    # 맵 세로 타일 수

@router.post("/pcg/map")
async def generate_pcg_map(req: PcgRequest):
    result = await generate_map(
        pollution  = req.pollutionLevel,
        map_width  = req.mapWidth,
        map_height = req.mapHeight
    )
    return result