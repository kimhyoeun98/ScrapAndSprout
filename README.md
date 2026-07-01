# ScrapAndSprout

`ScrapAndSprout`는 버려진 공간에서 쓰레기를 수거하고, 자원을 거래해 꾸미기 아이템을 배치하며 공간을 회복시키는 2D 협동 멀티플레이 게임입니다. Unity 클라이언트, Photon Fusion 기반 실시간 동기화, Spring Boot REST API, FastAPI PCG 서버, MySQL 데이터베이스 연동을 전제로 구성되어 있습니다.

## 1. 프로젝트 주제

플레이어는 로봇 캐릭터를 조작해 쓰레기 구역에서 자원을 수집하고, NPC 거래를 통해 골드를 얻은 뒤 꾸미기 아이템을 구매해 안전 구역을 정화합니다. 단순 수집을 넘어서 멀티플레이 협동, 자원 순환, 공간 꾸미기, 업적 달성을 하나의 플레이 흐름으로 연결하는 것이 핵심입니다.

## 2. 실행 방법

1. Unity Hub에서 이 폴더를 엽니다.
2. Unity Editor에서 `Assets/Scenes/LoginScene.unity` 씬을 엽니다.
3. 상단의 Play 버튼을 눌러 게임 클라이언트를 실행합니다.
4. 로그인 후 로비에서 방을 만들거나 방 코드로 참여합니다.
5. 대기실에서 캐릭터와 봇을 설정한 뒤 게임을 시작합니다.

DB, API 서버 주소, PCG 서버 주소 같은 개발 환경 설정은 [INSTALL.md](INSTALL.md)를 참고하세요.

## 3. 주요 기능

- 회원가입, 로그인, JWT 기반 API 요청
- Photon Fusion을 이용한 방 생성, 방 참여, 최대 4인 협동 플레이
- 대기실 캐릭터 선택 및 봇 추가
- 쓰레기 수거, 채굴/수집 미니게임, 인벤토리 반영
- NPC 거래를 통한 골드 획득 및 아이템 구매
- 꾸미기 아이템 배치와 정화 점수 계산
- PCG 서버 연동을 통한 맵 데이터 생성
- 업적, 랭킹, 결과 화면, 날씨/배터리/튜토리얼 시스템

## 4. 기술 스택

| 구분 | 기술 |
| --- | --- |
| 클라이언트 | Unity 6000.3.12f1, C# |
| 렌더링/UI | URP, Unity UI, TextMeshPro |
| 멀티플레이 | Photon Fusion |
| 서버 연동 | REST API, JSON, JWT |
| 백엔드 | Spring Boot, FastAPI |
| 데이터베이스 | MySQL 8.0+ |

## 5. 프로젝트 구조

```text
ScrapAndSprout/
├── Assets/
│   ├── Scenes/          # Login, Lobby, WaitingRoom, TrashZone, Deco 등
│   └── Scripts/         # 게임 로직, 네트워크, UI, PCG, 업적
├── Packages/
└── ProjectSettings/
```

## 6. 주요 씬 흐름

`LoginScene`에서 로그인한 뒤 `LobbyScene`으로 이동합니다. 로비에서는 방을 생성하거나 방 코드로 참여할 수 있고, `waitingRoomScene`에서 캐릭터와 봇을 설정합니다. 게임 시작 후 `TrashZoneScene`에서 수집과 PCG 맵 플레이가 진행되며, `DecoScene`에서 획득 자원으로 꾸미기 아이템을 배치합니다.

## 7. 데이터베이스

DB 복원 파일은 상위 폴더의 `scrap_sprout_db`에 있습니다.

- `01_schema.sql`: `scrap_sprout` 데이터베이스 생성
- `02_tables.sql`: `player`, `room`, `map`, `inventory`, `deco_detail`, `achievement` 테이블 생성
- `03_data.sql`: 개발/테스트용 샘플 데이터
- `restore.sql`: 위 SQL 파일을 순서대로 실행하는 진입 파일

## 8. 참고 사항

현재 저장소에는 Unity 클라이언트와 DB 스키마가 포함되어 있습니다. Spring Boot 서버와 FastAPI 서버는 별도 실행 환경이 필요하며, Unity 코드의 기본 서버 주소는 `Assets/Scripts/UI/ApiManager.cs`와 `Assets/Scripts/PCG/PCGManager.cs`에서 확인할 수 있습니다.
