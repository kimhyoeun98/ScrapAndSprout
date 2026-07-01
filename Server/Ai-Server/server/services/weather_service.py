import random

def calculate_weather(pollution: float, elapsed_days: int) -> dict:
    """
    날씨 계산 함수
    - pollution   : 현재 오염도 (0~100)
    - elapsed_days: 게임 경과 일수 (누적될수록 악천후 확률 감소)

    개요서 F06 — 동적 날씨 생성
    개요서 F07 — 기상 기반 페널티 (배터리 배율 반환)
    개요서 F08 — 누적 수거/식재에 따라 악천후 확률 점진적 감소
    """

    # ── 오염도 구간별 날씨 후보 ──────────────────
    if pollution >= 70:
        weather_options = ["acid_rain", "sandstorm"]
        severity = "high"
    elif pollution >= 30:
        weather_options = ["cloudy", "light_rain"]
        severity = "medium"
    else:
        weather_options = ["sunny", "clear"]
        severity = "low"

    # ── F08: 경과 일수만큼 악천후 확률 감소 ───────
    # elapsed_days 1일마다 1%씩 감소, 최대 30% 감소
    day_reduction = min(elapsed_days * 0.01, 0.30)
    bad_weather_chance = max(0.0, (pollution / 100) - day_reduction)

    # ── 날씨 결정 ────────────────────────────────
    if random.random() < bad_weather_chance:
        weather = random.choice(weather_options)
    else:
        weather = "sunny"

    # ── F07: 날씨별 배터리 소모 배율 ─────────────
    # 산성비: 1.5배 / 황사: 1.0배(시야제한) / 나머지: 1.0배
    battery_multiplier = {
        "acid_rain" : 1.5,  # 개요서 F07 — 배터리 소모율 1.5배
        "sandstorm" : 1.0,  # 개요서 F07 — 시야 제한(Fog)은 Unity에서 처리
        "light_rain": 1.2,  # 보통 비 — 중간 페널티
        "cloudy"    : 1.0,
        "sunny"     : 1.0,
        "clear"     : 1.0,
    }.get(weather, 1.0)

    return {
        "weather"          : weather,
        "severity"         : severity,
        "pollutionLevel"   : pollution,
        "badWeatherChance" : round(bad_weather_chance * 100, 1),
        "batteryMultiplier": battery_multiplier,  # ✅ Unity에서 배터리 소모에 곱함
        "dayReduction"     : round(day_reduction * 100, 1)  # 얼마나 확률이 줄었는지
    }