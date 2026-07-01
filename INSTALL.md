# INSTALL

이 문서는 `ScrapAndSprout` 클라이언트를 정상 실행하기 위해 필요한 개발 환경 설정을 정리합니다. 게임 실행 자체의 짧은 흐름은 [README.md](README.md)의 실행 방법을 참고하세요.

## 1. 요구 사항

- Unity Hub
- Unity Editor `6000.3.12f1`
- MySQL `8.0` 이상
- Spring Boot API 서버 실행 환경
- FastAPI PCG 서버 실행 환경
- Photon Fusion 사용을 위한 네트워크 연결

## 2. Unity 클라이언트 설정

1. Unity Hub를 실행합니다.
2. `Add project from disk`를 선택합니다.
3. `D:\newScas\ScrapAndSprout` 폴더를 선택합니다.
4. Unity 버전이 `6000.3.12f1`인지 확인하고 프로젝트를 엽니다.
5. 패키지 복원이 끝날 때까지 기다립니다.

프로젝트의 주요 실행 씬은 다음 순서로 등록되어 있습니다.

```text
Assets/Scenes/LoginScene.unity
Assets/Scenes/LobbyScene.unity
Assets/Scenes/waitingRoomScene.unity
Assets/Scenes/TutorialScene.unity
Assets/Scenes/TrashZoneScene.unity
Assets/Scenes/DecoScene.unity
Assets/Scenes/MainGame.unity
```

## 3. DB 설정

MySQL 클라이언트에서 `D:\newScas\scrap_sprout_db` 폴더로 이동한 뒤 아래 명령을 실행합니다.

```bash
mysql -uroot -p < restore.sql
```

파일을 하나씩 실행해야 하는 경우에는 아래 순서로 실행합니다.

```bash
mysql -uroot -p < 01_schema.sql
mysql -uroot -p scrap_sprout < 02_tables.sql
mysql -uroot -p scrap_sprout < 03_data.sql
```

DB 이름은 기본적으로 `scrap_sprout`입니다. 다른 이름을 사용하려면 `01_schema.sql`의 데이터베이스 이름을 변경하고, 서버 설정도 같은 이름으로 맞춰야 합니다.

## 4. 서버 설정

Unity 클라이언트는 Spring Boot API 서버와 FastAPI PCG 서버가 준비되어 있다는 전제로 동작합니다.

- Spring Boot API 서버: 로그인, 회원가입, 플레이어 정보, 거래, 꾸미기 배치, 업적 처리
- FastAPI PCG 서버: 쓰레기 구역 맵 생성 데이터 제공
- MySQL DB: 플레이어, 방, 맵, 인벤토리, 꾸미기, 업적 데이터 저장

Unity 클라이언트는 기본적으로 다음 주소를 사용합니다.

| 용도 | 기본 주소 | 설정 파일 |
| --- | --- | --- |
| Spring Boot API | `http://172.31.51.36:8080` | `Assets/Scripts/UI/ApiManager.cs` |
| FastAPI PCG | `http://172.31.51.36:8000` | `Assets/Scripts/UI/ApiManager.cs`, `Assets/Scripts/PCG/PCGManager.cs` |
| Achievement API | `http://172.31.51.36:8080/api/achievements` | `Assets/Scripts/Achievement/AchievementManager.cs` |

로컬 서버로 실행한다면 위 주소를 예를 들어 `http://localhost:8080`, `http://localhost:8000`처럼 변경합니다.

## 5. 클라이언트 실행 전 확인

1. `scrap_sprout` DB가 생성되어 있는지 확인합니다.
2. Spring Boot API 서버 주소가 Unity 설정값과 같은지 확인합니다.
3. FastAPI PCG 서버 주소가 Unity 설정값과 같은지 확인합니다.
4. Photon Fusion 연결이 가능한 네트워크 환경인지 확인합니다.
5. Unity에서 `Assets/Scenes/LoginScene.unity` 씬을 열어 둡니다.

## 6. 멀티플레이 확인

- Host는 로비에서 방을 생성합니다.
- Client는 같은 방 코드를 입력해 참여합니다.
- 대기실에서 캐릭터를 선택하고 게임을 시작합니다.
- Host가 씬 전환 권한을 가지며, `TrashZoneScene`과 `DecoScene` 전환은 Photon Fusion을 통해 동기화됩니다.

## 7. 자주 확인할 문제

- Unity 버전이 다르면 패키지 임포트나 씬 로드가 실패할 수 있습니다.
- API 서버 주소가 현재 네트워크 환경과 맞지 않으면 로그인, 거래, PCG 호출이 실패합니다.
- Photon 연결이 30초 이상 지연되면 로비로 복귀하도록 구현되어 있습니다.
- DB가 복원되지 않았거나 서버 DB 설정과 이름이 다르면 회원가입/로그인 API가 실패합니다.
- 서버 코드가 없는 환경에서는 Unity 클라이언트 단독 실행만 가능하며, 로그인/거래/PCG 기능은 정상 동작하지 않습니다.
