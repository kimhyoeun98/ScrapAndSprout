from fastapi import APIRouter
from pydantic import BaseModel
from services.weather_service import calculate_weather

router = APIRouter()

class WeatherRequest(BaseModel):
    pollutionLevel: float
    elapsedDays: int

@router.post("/weather")
async def get_weather(req: WeatherRequest):
    result = calculate_weather(
        pollution=req.pollutionLevel,
        elapsed_days=req.elapsedDays
    )
    return result