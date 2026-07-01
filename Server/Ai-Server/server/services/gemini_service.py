# ================================================================
#  gemini_service.py — Gemini API + BSP PCG 맵 생성
#
#  [BSP 흐름]
#  1. BSP 알고리즘으로 맵을 구역으로 분할
#  2. Gemini가 각 구역에 오염도/역할 부여
#  3. 오염도 기반으로 쓰레기 스폰/식재 위치 결정
#  4. SRS FR-6.1 기반 날씨 초기값 계산
# ================================================================

from google import genai
import os
import json
import random
from dotenv import load_dotenv
from dataclasses import dataclass, field
from typing import Optional, List

load_dotenv(dotenv_path=os.path.join(os.path.dirname(__file__), '..', '.env'))
client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))


# ----------------------------------------------------------------
#  BSP 노드 구조체
#  x, y     : 구역 좌상단 좌표
#  width    : 구역 가로 크기
#  height   : 구역 세로 크기
#  left/right: 자식 노드 (None이면 리프 노드 = 최종 구역)
# ----------------------------------------------------------------
@dataclass
class BSPNode:
    x     : int
    y     : int
    width : int
    height: int
    left  : Optional['BSPNode'] = field(default=None, repr=False)
    right : Optional['BSPNode'] = field(default=None, repr=False)


# ----------------------------------------------------------------
#  BSP 분할
#
#  [원리]
#  전체 맵을 재귀적으로 절반씩 분할
#  분할 방향(가로/세로)은 구역 비율에 따라 자동 결정
#  최소 크기(min_size)보다 작아지면 분할 중단 → 리프 노드
# ----------------------------------------------------------------
def _bsp_split(node: BSPNode, min_size: int = 6) -> None:
    # 더 이상 분할 불가능하면 종료
    if node.width < min_size * 2 and node.height < min_size * 2:
        return

    # 분할 방향 결정
    # 가로가 세로의 1.5배 이상 → 세로로 분할
    # 세로가 가로의 1.5배 이상 → 가로로 분할
    # 나머지 → 랜덤
    if node.width > node.height * 1.5:
        split_horizontal = False
    elif node.height > node.width * 1.5:
        split_horizontal = True
    else:
        split_horizontal = random.random() > 0.5

    if split_horizontal:
        if node.height < min_size * 2:
            return
        split_pos = random.randint(min_size, node.height - min_size)
        node.left  = BSPNode(node.x, node.y,             node.width, split_pos)
        node.right = BSPNode(node.x, node.y + split_pos, node.width, node.height - split_pos)
    else:
        if node.width < min_size * 2:
            return
        split_pos = random.randint(min_size, node.width - min_size)
        node.left  = BSPNode(node.x,             node.y, split_pos,             node.height)
        node.right = BSPNode(node.x + split_pos, node.y, node.width - split_pos, node.height)

    # 자식 노드 재귀 분할
    _bsp_split(node.left,  min_size)
    _bsp_split(node.right, min_size)


def _get_leaves(node: BSPNode) -> List[BSPNode]:
    """리프 노드(최종 구역) 목록 반환"""
    if node.left is None and node.right is None:
        return [node]
    leaves = []
    if node.left:
        leaves.extend(_get_leaves(node.left))
    if node.right:
        leaves.extend(_get_leaves(node.right))
    return leaves


# ----------------------------------------------------------------
#  SRS FR-6.1 기반 초기 날씨 결정
#
#  0~30%  오염도 → 80% 확률로 맑음
#  70~100% 오염도 → 70% 확률로 산성비
#  나머지  → 흐림/약한비
# ----------------------------------------------------------------
def _calculate_initial_weather(pollution: float) -> str:
    if pollution <= 30:
        return "sunny" if random.random() < 0.8 else "cloudy"
    elif pollution >= 70:
        return "acid_rain" if random.random() < 0.7 else "sandstorm"
    else:
        return random.choice(["cloudy", "light_rain"])


# ----------------------------------------------------------------
#  Gemini에게 구역별 오염도/역할 부여 요청
#
#  [Gemini 역할]
#  BSP가 만든 구역 목록을 주면
#  Gemini가 각 구역에 오염도와 역할을 결정
#  → 게임 세계관(2087년 황폐화된 지구)에 맞게 자연스럽게 배치
# ----------------------------------------------------------------
async def _assign_zones_with_gemini(zones: list, total_pollution: float) -> list:
    zone_list = [
        {"id": i, "x": z.x, "y": z.y, "width": z.width, "height": z.height}
        for i, z in enumerate(zones)
    ]

    prompt = f"""
너는 2087년 황폐화된 지구의 2D 게임 맵 설계 AI야.
전체 오염도: {total_pollution}/100

아래 구역 목록에 각각 오염도와 역할을 부여해줘.
전체 오염도가 높을수록 pollutionLevel이 높은 구역이 많아야 해.

구역 목록:
{json.dumps(zone_list, ensure_ascii=False)}

각 구역에 대해 아래 형식으로 반환해:
- pollutionLevel: 0~100 사이 값
- role: "trash_zone"(쓰레기 스폰 구역) / "safe_zone"(꾸미기·NPC 구역) 중 하나
- 전체 오염도가 높을수록 trash_zone 비율이 높아야 해
- safe_zone은 반드시 1개 이상 포함 (NPC와 꾸미기 아이템이 여기에 배치됨)
- 맵의 왼쪽 구역은 trash_zone, 오른쪽 구역은 safe_zone이 되도록 배치해

반드시 아래 JSON 배열만 반환해. 다른 말 하지마.
[{{"id": 0, "pollutionLevel": 80, "role": "trash_zone"}}, ...]
"""
    try:
        response = client.models.generate_content(
            model = "gemini-2.5-flash",
            contents=prompt
        )
        text = response.text.strip()
        if "```" in text:
            text = text.split("```")[1].replace("json", "").strip()
        return json.loads(text)

    except Exception as e:
        print(f"[PCG] Gemini 구역 배정 실패 — 기본값 사용: {e}")
        return _default_zone_assign(zone_list, total_pollution)


def _default_zone_assign(zones: list, pollution: float) -> list:
    """Gemini 실패 시 오염도 기반 기본 배정"""
    result = []
    # 구역을 x 좌표 기준으로 정렬 — 왼쪽은 trash_zone, 오른쪽은 safe_zone
    max_x = max(z["x"] for z in zones) if zones else 1
    safe_zone_assigned = False

    for i, z in enumerate(zones):
        # x 좌표가 맵 오른쪽 절반이면 safe_zone 우선
        is_right_side = z["x"] > max_x * 0.6
        if is_right_side and not safe_zone_assigned:
            role = "safe_zone"
            safe_zone_assigned = True
        elif is_right_side:
            role = "safe_zone"
        elif random.random() < pollution / 100:
            role = "trash_zone"
        else:
            role = "safe_zone"
            safe_zone_assigned = True

        result.append({
            "id"            : i,
            "pollutionLevel": min(100, pollution + random.uniform(-20, 20)),
            "role"          : role
        })
    return result


# ----------------------------------------------------------------
#  스폰 포인트 생성
#
#  SRS FR-3.1: 쓰레기 최대 10개
#  trash_zone 구역 → 쓰레기 스폰 (오염도 비례)
#  safe_zone 구역  → 꾸미기 배치 가능 + NPC 위치
# ----------------------------------------------------------------
def _generate_spawn_points(zones: list, zone_data: list) -> dict:
    trash_points    = []
    plantable_areas = []
    npc_position    = {"x": 0, "y": 0}

    zone_map = {z["id"]: z for z in zone_data}

    for node in zones:
        idx  = zones.index(node)
        data = zone_map.get(idx, {})
        role = data.get("role", "low_pollution")
        pollution = data.get("pollutionLevel", 50)

        cx = node.x + node.width  // 2  # 구역 중앙 x
        cy = node.y + node.height // 2  # 구역 중앙 y

        if role == "trash_zone":
            # SRS FR-3.1: 오염도 비례 스폰 수 (최대 10개)
            count = min(5, max(2, int(pollution / 15)))  # 구역당 최소 2개

            for _ in range(count):
                trash_points.append({
                    # "x": random.randint(30, 40),  # worldX = -55+30~40 = -25 ~ -15
                    "x": random.randint(0, 35),  # worldX = -55+0~35 = -55~-20
                    "y": random.randint(node.y + 1, node.y + node.height - 1)
                })

        elif role == "safe_zone":
            # safe_zone: 꾸미기 배치 가능 영역
            plantable_areas.append({"x": cx, "y": cy,
                                    "width": node.width, "height": node.height})
            # safe_zone 중 첫 번째에 NPC 배치
            if npc_position == {"x": 0, "y": 0}:
                npc_position = {"x": cx, "y": cy}

    return {
        "trashSpawnPoints": trash_points[:20],  # SRS FR-3.1 최대 10개
        "plantableAreas"  : plantable_areas,
        "npcPosition"     : npc_position
    }


# ================================================================
#  메인 함수 — Unity Host에서 호출
#
#  [구역 구조]
#  TrashZone: x = 0 ~ map_width      (왼쪽 절반)
#  SafeZone:  x = map_width ~ map_width*2  (오른쪽 절반)
#
#  Unity PCGManager의 trashZoneWorldOffsetX / safeZoneWorldOffsetX와
#  타일 좌표 오프셋이 맞아야 함
# ================================================================
async def generate_map(pollution: float, map_width: int, map_height: int) -> dict:
    # 1. TrashZone BSP 분할 (x: 0 ~ map_width)
    #trash_root = BSPNode(0, 0, map_width, map_height)
    trash_root = BSPNode(0, 0, map_width, map_height)
    _bsp_split(trash_root, min_size=5)
    trash_leaves = _get_leaves(trash_root)

    # 2. SafeZone BSP 분할 (x: map_width ~ map_width*2)
    safe_root = BSPNode(map_width, 0, map_width, map_height)
    _bsp_split(safe_root, min_size=5)
    safe_leaves = _get_leaves(safe_root)

    print(f"[PCG] BSP 완료 — TrashZone: {len(trash_leaves)}구역, SafeZone: {len(safe_leaves)}구역")

    # 3. TrashZone → 모두 trash_zone role, Gemini로 오염도만 조정
    trash_zone_data = await _assign_trash_zones_with_gemini(trash_leaves, pollution)

    # 4. SafeZone → 모두 safe_zone role, 오염도 낮게
    safe_zone_data = [
        {"id": i, "pollutionLevel": max(0, pollution * 0.3 - random.uniform(0, 10)), "role": "safe_zone"}
        for i, _ in enumerate(safe_leaves)
    ]

    # 5. 스폰 포인트 생성 (TrashZone 기준)
    spawn_data = _generate_spawn_points(trash_leaves, trash_zone_data)

    # NPC는 SafeZone 첫 번째 구역 중앙
    if safe_leaves:
        npc_node = safe_leaves[0]
        spawn_data["npcPosition"] = {
            "x": npc_node.x + npc_node.width  // 2,
            "y": npc_node.y + npc_node.height // 2
        }

    # 6. 초기 날씨 결정
    initial_weather = _calculate_initial_weather(pollution)

    # 7. 전체 zones 합치기 (Trash + Safe)
    all_zones = []
    for i, z in enumerate(trash_leaves):
        all_zones.append({
            "x"             : z.x,
            "y"             : z.y,
            "width"         : z.width,
            "height"        : z.height,
            "pollutionLevel": trash_zone_data[i].get("pollutionLevel", pollution),
            "role"          : "trash_zone"
        })
    for i, z in enumerate(safe_leaves):
        all_zones.append({
            "x"             : z.x,
            "y"             : z.y,
            "width"         : z.width,
            "height"        : z.height,
            "pollutionLevel": safe_zone_data[i].get("pollutionLevel", 20),
            "role"          : "safe_zone"
        })

    return {
        "mapWidth"        : map_width * 2,   # 전체 맵 폭 (Trash + Safe)
        "mapHeight"       : map_height,
        "pollutionLevel"  : pollution,
        "initialWeather"  : initial_weather,
        "trashSpawnPoints": spawn_data["trashSpawnPoints"],
        "plantableAreas"  : spawn_data["plantableAreas"],
        "npcPosition"     : spawn_data["npcPosition"],
        "zones"           : all_zones
    }


async def _assign_trash_zones_with_gemini(zones: list, total_pollution: float) -> list:
    """TrashZone 구역별 오염도 조정 (role은 모두 trash_zone으로 고정)"""
    zone_list = [
        {"id": i, "x": z.x, "y": z.y, "width": z.width, "height": z.height}
        for i, z in enumerate(zones)
    ]

    prompt = f"""
너는 2087년 황폐화된 지구의 2D 게임 맵 설계 AI야.
전체 오염도: {total_pollution}/100

아래는 쓰레기 구역(TrashZone) 목록이야. 각 구역의 오염도만 결정해줘.
전체 오염도가 높을수록 pollutionLevel이 높아야 해.

구역 목록:
{json.dumps(zone_list, ensure_ascii=False)}

반드시 아래 JSON 배열만 반환해. 다른 말 하지마.
[{{"id": 0, "pollutionLevel": 80}}, ...]
"""
    try:
        response = client.models.generate_content(
            model="gemini-2.5-flash-preview-04-17",
            contents=prompt
        )
        text = response.text.strip()
        if "```" in text:
            text = text.split("```")[1].replace("json", "").strip()
        parsed = json.loads(text)
        # role 강제 주입
        for item in parsed:
            item["role"] = "trash_zone"
        return parsed
    except Exception as e:
        print(f"[PCG] Gemini TrashZone 배정 실패 — 기본값: {e}")
        return [
            {"id": i, "pollutionLevel": min(100, total_pollution + random.uniform(-15, 15)), "role": "trash_zone"}
            for i in range(len(zones))
        ]