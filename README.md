# 2인 협동 방탈출 Unity 클라이언트

기준일: **2026-10-21**. 현재 코드와 검증 결과를 기준으로 설명합니다. 이전 기획서와 차이가 있으면 현재 요구사항과 데이터 테이블을 우선합니다.

두 클라이언트가 음성으로 단서를 주고받으며 퍼즐을 해결하는 Unity 6 프로젝트입니다. Join에서 환경 config에 등록한 서버로 자동 입장하거나 **클라이언트 테스트 A/B**를 선택하고, 검증한 CSV를 데이터 클래스로 주입한 뒤 Ingame에서 방을 구성합니다. 현재 로컬 콘텐츠 Play 확인 범위는 **A/B R01과 B R02**이며, 다음 공간을 열어도 이미 로드한 공간은 유지합니다.

## 1. 실행과 현재 범위

| 항목 | 구성 |
|---|---|
| Unity | 6000.2.8f1 / Unity 6.2 |
| 시작 / 게임 씬 | [Join](Assets/1.Scene/Join.unity) / [Ingame](Assets/1.Scene/Ingame.unity) |
| 게임 통신 | UnityWebRequest, HTTP + Google Protobuf 바이너리 |
| 음성 | Vivox 16.12.1, Unity Authentication 3.4.1 |
| UI / 렌더링 | uGUI·TextMeshPro / URP 17.2.0 |
| 로컬 데이터 | [Assets/11.CSV](Assets/11.CSV), 23개 CSV |
| 서버 | 별도 저장소 TSC_REST, Node.js 24 이상 |

1. Unity Hub에서 프로젝트를 열고 패키지 복원·컴파일을 마칩니다.
2. Join을 열어 Play 모드로 들어갑니다.
3. Join 활성화 시 config의 현재 환경 주소로 자동 입장합니다. 에디터는 `http://127.0.0.1:3000`, Development Build는 `http://192.168.1.34:3001`입니다. 주소 입력창은 없으며, 입장 버튼은 같은 config 설정으로 재시도합니다. 온라인은 두 참가자가 모일 때까지 Join에서 대기합니다.
4. 서버 없이 확인할 때는 `Assets/StreamingAssets/Configuration/connection.json`의 editor 환경 `autoConnect`를 false로 설정하고 **클라이언트 테스트 A/B**를 누릅니다. 로컬 버튼은 에디터에서만 표시하며 Development Build를 포함한 모든 빌드에서 제외합니다.
5. 온라인은 CSV 등록과 두 참가자 접속 후 Ingame을 로드합니다. WorldRuntime에서 해당 역할의 R01과 플레이어를 준비하고, 양쪽 ready 후 Playing이 될 때 대기 화면·조작 잠금을 해제합니다. 로컬 테스트는 상대방 없이 즉시 로드합니다. 현재 로컬 CSV에서는 A/B 첫 방·플레이어 생성을 확인했습니다. A 2번 방은 R2A_LOCK 앵커 누락으로 중단됩니다. 온라인은 서버 CSV 번들을 기준으로 별도 확인해야 합니다.

접속 설정은 [환경별 접속 설정 안내](Docs/ConnectionConfiguration.md)를 따릅니다. **Development Build 체크 = 테스트 주소, 체크 해제 = 안정화 주소**입니다. 안정화 주소는 아직 비어 있으므로 클라우드 주소 등록 전 일반 빌드는 검증에서 중단됩니다.

현재 빌드 목록은 Join, Vivox 수동 샘플, Ingame이며 **첫 씬은 Join**입니다. Windows 빌드는 Build Profiles에서 생성하고 출력 폴더 전체를 공유합니다. 소스·CSV·프리팹 변경은 새 빌드에 반영해야 합니다.

| 완료한 기능 | 후속 범위 |
|---|---|
| 서버 입장·A/B 역할·조회·ready·heartbeat·퇴장 | 기존 참가자 신분 복구, 다중 세션·영구 저장 |
| CSV 메모리 등록·검증·23개 데이터 클래스·주입 | 새 시트 추가 시 스키마·모델 정의 |
| 서버/로컬 소스 선택, Join → Ingame 수명 유지 | 완성된 메뉴와 Ingame 연결 종료 UI |
| R01 로더 조립·공통 플레이어 스폰·WASD 정수 이동 | A R02 앵커 보완, 전체 방 콘텐츠·실제 이동 연결 검증 |
| 서버 슬롯 정답 비교와 해금 상태 반영 | 입력 UI·문 연출·오답 잠금·승패 규칙 |
| Vivox 자동 연결 코드 | 두 PC 실제 음성 송수신·장치 검증 |

## 2. 데이터 선택부터 방 로드까지

![Join 모드 선택과 CSV 주입 및 방 로드 흐름](Docs/Images/client-data-flow.png)

[확대 가능한 SVG](Docs/Images/client-data-flow.svg)

| 단계 | 코드와 동작 |
|---|---|
| 입력 | `SessionConnectionView`가 서버 입장 또는 로컬 역할 선택 이벤트를 보냅니다. |
| 조정 | `GameEntryPresenter`가 `GameEntryService.EnterAsync`를 호출하고 상태·오류를 표시합니다. |
| 서버 소스 | join으로 참가 토큰을 받은 뒤 `/data`에서 CSV를 받습니다. `SessionCoordinator`가 최초 상태 조회까지 같은 요청 경로에서 순차 처리해 자동 조회와의 경합을 방지합니다. |
| 로컬 소스 | `GameContentAssets.ReadLocalData`가 빌드에 포함된 CSV TextAsset을 읽습니다. 기존 서버 참가 중이면 먼저 퇴장합니다. |
| 공통 등록 | `CsvParser` → `CsvDataSet` → `CsvValidator` → `CsvDataStore.TryRegister` → `CsvGameData` 순서입니다. |
| 수명 | `GameFlowBootstrapper`가 DontDestroyOnLoad로 유지됩니다. 씬 로드 시작 전까지 화면 요청을 취소할 수 있고 시작 후에는 게임 소유 수명으로 넘겨 Join UI 제거가 입장을 취소하지 않게 합니다. |
| 방 구성 | Ingame의 WorldRuntime에 IngameWorldDriver와 R01_Runtime 참조가 연결됩니다. Bootstrapper가 로더를 명시적으로 초기화하여 Start 실행 순서에 기대지 않습니다. |
| 플레이어 | 로더가 준비한 첫 방을 PlayerSpawnDriver에 주입하고 R01_Player 앵커에 카메라·캡슐·입력/이동을 구성합니다. 실패하면 입장 실패로 전달합니다. |
| 준비 보고 | `WorldReady && PlayerReady`와 현재 Ingame 씬을 함께 확인합니다. 방/플레이어 초기화 실패로 준비가 보고되지 않으며 이번 입장의 서버 자리를 정리합니다. |

서버 동기화 실패 시 Join에서 오류를 표시하고 이번 입장에서 확보한 자리를 정리합니다. 서버 실패를 로컬 CSV로 대체하지 않습니다. 데이터 소스를 바꾸는 동안 이전 씬이 준비 완료를 보고하거나 이전 계획으로 새 스냅샷을 적용하지 않도록 구분합니다.

## 3. 코드 구조와 디자인패턴

![클라이언트 계층과 이벤트 및 의존성 구조](Docs/Images/client-architecture.png)

[확대 가능한 SVG](Docs/Images/client-architecture.svg)

MVC의 데이터·표현·제어 책임을 Unity 수명에 맞춰 나누고, 화면은 View/Presenter로 구성합니다. 모든 기능을 하나의 패턴으로 강제하지 않고 책임에 맞게 조합합니다.

| 모듈 | 책임 / 적용 방식 |
|---|---|
| `Configuration` | 환경별 불변 설정 선택과 공통 자동 입장 명령. Unity 파일 입력과 빌드 검사 어댑터를 분리합니다. |
| `World/GameEntryService` | 소스 선택·검증·씬 진입 조정. 세션 계약, 로컬 읽기 함수, 검증기, 씬 전환 함수를 생성자로 주입합니다. |
| `World/GameFlowBootstrapper` | Unity 조립과 수명 소유자. 별도의 전역 데이터 Singleton 없이 서비스를 보유합니다. |
| `Data/CsvDataStore`, `Tables/CsvGameData` | 불변 원본, 타입별 변환 캐시, 소비자에 주입하는 데이터 묶음입니다. |
| `RoomLoadPlan`, `LocalRoomProgression` | Unity 오브젝트와 독립적인 선택·로컬 진행 규칙입니다. |
| `RoomSceneView`, `IngameWorldDriver` | 프리팹 생성·앵커 탐색·로드 이벤트·기존 방 유지입니다. |
| `SessionClient` | `ISessionConnectionService`, `ISessionContentService`를 구현하는 Facade입니다. 인증 정보는 내부에 유지합니다. |
| `SessionCoordinator`, `SessionRequestRunner` | 요청 순서·취소·중복·연결 수명과 결과 반영을 조정합니다. |
| `HttpSessionGateway`, `SessionApiClient` | Adapter와 Protobuf HTTP 경계입니다. View는 HTTP 경로·토큰·Wire DTO를 다루지 않습니다. |
| `CsvTableMapping<T>`, 검증 규칙 | 시트와 변환 함수를 묶은 매핑, 설정과 규칙 객체의 조합을 재사용합니다. |
| `StateChanged`, `PhaseChanged`, `RoomsChanged`, `Registered` | 반영 완료 후 통지하는 Observer 방식입니다. Driver는 Unity Update·활성/비활성 수명을 담당합니다. |
| Voice 모듈 | 세션 이벤트를 음성 연결 목표로 바꾸는 Coordinator, 서비스, Vivox Gateway Adapter입니다. |

```text
Assets/3.Script/
  Common/Csv/                 CSV 파서·입력 포트·문서/행
  Common/Timing/, Readiness/   정수 시계·간격·준비 조건
  Data/                       원본·등록소·매핑·GameContentAssets
    Tables/                   23개 행 모델·공통 Room/Anchor·CsvGameData
    CsvValidation/            설정·규칙·결과·한국어 메시지
    Infrastructure/           파일·TextAsset 어댑터
  World/                      입장 수명·방 로더·첫 방/플레이어 준비·로컬 진행
  Player/                     스폰·입력 Adapter·정수 이동/시점·공통 조작 상태·Unity 충돌/모델 보정
  Session/                    계약·코어·자동 Driver·화면
  Networking/                 HTTP·Protobuf·Gateway
  Voice/                      음성 계약·서비스·Vivox·화면
  Editor/                     CSV 검사 창·콘텐츠 참조 생성
  Generated/Protocol/         Protobuf 생성물
Tools/                        .NET·Unity Play 검사·도식 생성
```

## 4. CSV 읽기·검증·클래스 주입

CSV는 **UTF-8, 쉼표 구분, 첫 번째 비어 있지 않은 레코드가 헤더**입니다. 예전 기획서처럼 앞 4행을 건너뛰지 않습니다. BOM·인용부호 안 쉼표/줄바꿈·따옴표 이스케이프를 지원하며, 문자열 공백과 앞자리 0은 보존합니다.

| 구성 | 역할 |
|---|---|
| `CsvParser` / `CsvLoader` | 문법 해석과 입력 어댑터 연결. 파서는 Unity에 의존하지 않습니다. |
| `CsvDataSet` | 읽은 원본 문서와 읽기/해석 오류를 보존하는 불변 스냅샷입니다. |
| `CsvValidator` | 파일을 다시 열지 않고 메모리 원본을 검사합니다. |
| `CsvDataStore` | 정상 원본만 등록합니다. 잘못된 재등록은 기존 정상 원본·캐시를 바꾸지 않습니다. |
| `CsvTableMapping<T>` | 시트 이름과 변환 함수를 한 번 정의하고 동일 시트/타입의 변환 캐시를 재사용합니다. |
| `CsvGameData` | 전체 테이블을 읽기 전용 모델 목록으로 제공합니다. 새 스냅샷 등록 후에는 새 묶음을 주입합니다. |

스키마 원본은 [CsvValidationRules.xml](Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml)입니다. 공통 방·앵커 profile을 재사용하고 시트 이름·컬럼 형식·참조 대상과 게임 관계 검사를 분리합니다. 서버는 대응 규칙을 `CsvSchema.ts`와 전용 관계 규칙으로 정의하므로 변경 시 양쪽을 맞춰 검사해야 합니다. CHARACTER_STAT의 정수 변환·범위·눈높이/캡슐/단차 관계는 `CharacterStatUnits`를 CSV 등록과 실제 플레이어 생성에서 공유합니다.

검사 범위는 CSV 문법·UTF-8, 필수/미등록 컬럼, 자료형·범위·Y/N, 키 중복, REGISTRY·테이블 참조, Next 순환·활성 방 시작점 1개·전체 활성 방 연결, ExitRule 허용값(SolvedEach/BothSolved), ROOM/ANCHOR 공통 ID 영역, Solver/Clue·입력 앵커 관계, 슬롯 수·순서, 사운드·설정값 관계입니다. 선택적인 Unity 리소스 경로 검사는 Editor 어댑터가 수행하며 미발견을 경고합니다. 런타임은 필요한 앵커·프리팹이 없으면 해당 방 로드를 완료하지 않습니다.

23개 모델은 `AnchorInteractData`, `AnchorNonInteractData`, `RoomAData`, `RoomBData`, `ArrowData`, `CardData`, `CharacterStatData`, `ColorLetterData`, `ComponentData`, `ConsoleData`, `FormulaData`, `FrameData`, `GameConfigData`, `LockData`, `PaperData`, `PrefabData`, `PuzzleData`, `PuzzleSlotData`, `RegistryData`, `SoundData`, `SoundLinkData`, `TvData`, `WireData`입니다. 공통 필드와 변환은 `RoomData`, `AnchorData`, `CsvRowReader`로 재사용합니다.

ID·순서·ms는 정수, Y/N은 bool로 변환합니다. 정답·표시값은 숫자처럼 보여도 문자열로 유지합니다. `EyeHeight`, `CapsuleRadius`, `LengthM`, `Volume` 같은 Unity 표현·물리·오디오 수치는 float로 보존합니다. `GAME_CONFIG`는 선언된 Type에 맞는 접근자만 허용합니다.

```csharp
// 검증·등록한 데이터 묶음을 필요한 소비자에 전달합니다.
CsvGameData data = flow.Service.Data;
var anchors = data.InteractAnchors;

// using EscapeGame.Session.Contracts;
// 퍼즐 입력 기능이 연결할 공통 진입점입니다.
PuzzleSubmissionResult result = await flow.Service.SubmitPuzzleAsync(puzzleId, answers, token);
// result.Outcome: Rejected=0(제출 거절), Incorrect=1(오답), Correct=2(정답)
// 통신 실패·취소는 예외 경로로 전달됩니다.
```

**Tools → CSV → 검사기**는 처음 검사할 때 파일을 읽습니다. **메모리 데이터 검사**는 같은 원본을 재검사하며, 파일 수정 후에는 **CSV 다시 읽고 검사**를 사용합니다. 결과의 파일·행·컬럼·원인을 검색·복사할 수 있습니다.

## 5. R01 스폰과 이후 공간 추가

1. 역할에 맞는 ROOM_A 또는 ROOM_B에서 활성 방과 Next 연결을 읽습니다.
2. 두 ANCHOR 테이블을 합쳐 RoomNo 일치, Active=Y, PlayerType=내 역할 또는 ALL인 행만 선택합니다.
3. AnchorID와 이름이 정확히 일치하는 Transform을 찾고 `PrefabID → PREFAB.Path → GameContentAssets`로 프리팹을 찾습니다. 앵커 이름에 특정 접미사를 강제하지 않습니다.
4. 필요한 참조를 먼저 확인한 뒤 생성합니다. 앵커 아래 로컬 위치 0·회전 identity를 적용하고 프리팹 로컬 스케일을 유지합니다.
5. `AnchorInstance`가 `AnchorData`와 `CsvGameData`를 보유하며, 기능 컴포넌트는 `IAnchorDataReceiver.Initialize`로 주입받습니다.
6. 성공한 방만 등록하고 반복 스냅샷으로 방·프리팹을 중복 생성하지 않습니다.

현재 Ingame은 **R01_Runtime → Room_01_Final** 구조입니다. 최신 Git Pull에서 카드·서랍이 분리되어 삭제된 embedded 경로 2개를 제거했고, 유효한 천장·입구 문 바인딩 2개를 유지합니다. A 소품 묶음은 최신 CSV의 `R01A_PropsPoint → PREFAB 13075`로 생성합니다. 활성 앵커는 CSV에서 역할별로 선택하며 플레이어 행은 전용 Factory가 처리합니다. 2026-10-21 현재 로컬 데이터의 A/B 첫 방 스폰은 성공했습니다. A 2번 방에는 CSV ID 2034가 요구하는 R2A_LOCK 앵커가 없어 후속 로드를 차단합니다. 이 숫자는 현재 콘텐츠 현황이며 자동 검사는 개수를 고정하지 않고 선택된 행 ID와 실제 바인딩을 대조합니다.

서버 모드는 서버 `RoomProgression`의 정답·Solver·해금·Playing 검사를 거친 `unlockedRoomIds`를 조회해 `RoomsChanged`로 반영합니다. 로컬 모드는 `LocalRoomProgression`을 사용합니다. `SolvedEach`는 해당 퍼즐, `BothSolved`는 같은 방 번호 양쪽 퍼즐 완료를 조건으로 문을 허가합니다. 정답 제출자는 자기 문과 Next를 먼저 허가받고, 상대는 자기 문 확인 때 허가받습니다. 이후 문 열기·닫기는 RoomPermissionService 캐시를 사용하며 추가 통신이 없습니다. Next=0이면 추가 방이 없습니다. ExitRule은 `RoomExitRules`에서 공통 해석하며 알 수 없는 값은 검증과 런타임에서 차단합니다. 활성 방은 시작점 하나에서 모두 연결되어야 하며, 비활성 방으로 끊긴 연결도 허용하지 않습니다.

**R02 이후 콘텐츠를 추가할 때**:

- ROOM_A/B의 Active·RoomPrefabID·PuzzleID·Next와 PREFAB의 실제 프리팹 경로를 맞춥니다.
- 방 프리팹에 ANCHOR와 이름이 같은 앵커를 배치합니다. 방 프리팹 자체의 저장 위치·회전을 다음 공간의 배치에 맞춥니다.
- 역할별 오브젝트 프리팹과 값 테이블을 준비합니다. 씬에 미리 둔 방은 `sceneRooms`로 연결할 수 있고 없는 방은 RoomPrefabID 경로에서 생성합니다.
- CSV·프리팹 변경 시와 빌드 전 `GameContentBuilder`가 [GameContent.asset](Assets/Resources/GameContent.asset)의 참조를 갱신합니다. 수동 메뉴는 **Tools → Game → Rebuild Content References**입니다.

미완성인 다음 방은 로드를 보류하고 원인을 알리며 기존 방은 유지합니다. 에디터에서 에셋을 준비해 빌드하는 방식이며 실행 중 원격 프리팹 다운로드는 지원하지 않습니다.

## 6. 세션·음성·통신 경계

| 서버 단계 | 의미 |
|---|---|
| Waiting | 참가 인원 부족 |
| Loading | 두 명이지만 씬 준비 미완료 참가자가 있음 |
| Playing | 두 참가자 모두 준비 보고 완료 |

기본 자동 조회·ready 재시도는 1000ms, heartbeat는 3000ms, 정상 heartbeat 응답 단절 기준은 10000ms입니다. heartbeat 요청 제한은 2000ms, 일반 HTTP 제한은 10000ms입니다. 정수 ms·Stopwatch를 사용하고 Unity API 경계에서만 초 단위로 바꿉니다. heartbeat와 일반 요청 경로를 분리하고 연결 버전으로 이전 연결의 늦은 응답을 무시합니다.

Vivox는 `StateChanged → SessionVoiceCoordinator → VoiceChatService → VivoxVoiceGateway` 순서입니다. 세션 ID 기반 채널을 사용하고 참가 토큰을 채널명에 쓰지 않습니다. 로컬 테스트는 서버에 입장하지 않으므로 음성 연결 목표도 만들지 않습니다. Unity Cloud/Vivox 설정과 인터넷 연결은 별도 필요합니다.

기존 두 PC 검사에서 채널 연결 표시는 확인했으나 **실제 양방향 송수신·Bluetooth 장치 검증은 보류** 상태입니다. 연결 표시만으로 음성 검증 완료로 판단하지 않습니다. 게임 서버는 음성을 중계하지 않습니다.

게임 API는 **HTTP + Google Protobuf 바이너리**입니다. 메시지는 .proto, 경로·인증·미디어 타입은 OpenAPI로 관리합니다. 생성 도구가 C# 메시지와 호출 인터페이스/기본 클래스를 생성하고, 클라 게임 기능은 공개 계약으로 접근합니다. config JSON은 로컬 설정 파일입니다. 새 문·정답 API, 코드별 설명, 생성·배포 및 검사 방법은 [문·정답 API 안내](Docs/RoomApiAndProtobuf.md)를 참고합니다.

## 7. 검사 실행과 확인된 결과

Unity 없는 검사는 **.NET 8 SDK**로 실행합니다.

```powershell
Set-Location Tools/CsvValidation.Tests
dotnet run -- ../..
```

현재 공통 순수 C# 회귀 검사 **295개**, 별도 시작 조건 검사 **12개**, 스폰·이동·시점·조작 상태 검사 **77개**가 통과했습니다. 이번 클라이언트 CSV 스냅샷 **23개·3,320개 셀**을 검사했습니다. Unity 로컬 데이터 검증은 오류 **0개**, 경고 **82개**였으며 개별 경고 확인이 필요합니다. 셀·앵커 개수는 콘텐츠 변경으로 달라질 수 있어 고정 성공 기준으로 사용하지 않습니다. 화면 제거 후 씬 진입 유지, 게임 소유자 취소, 초기화 실패 시 참가 자리 정리와 캐릭터 정수 검증을 포함합니다. .NET 8 SDK가 없어 같은 소스를 임시 net10 프로젝트에서 실행했습니다. Unity 6.2 런타임/에디터 도구/Windows 런타임 분리 참조 컴파일도 통과했습니다. 실제 Unity Play·빌드 검증과 구분합니다.

별도 임시 Unity 프로젝트에서 Play 검사를 실행합니다. 원본 씬을 덮어쓰지 않고 결과·로그를 `Temp/WorldPlayVerification`에 보관합니다.

```powershell
# 현재 CSV 선택·씬/플레이어·중복 방지 검사. B 콘텐츠 누락 시 불완료 종료 코드 2
./Tools/WorldPlayVerification/Run.ps1

# 서버 포함 시나리오는 B 콘텐츠 배치가 완료된 뒤 실행
./Tools/WorldPlayVerification/Run.ps1 -IncludeServer `
  -ServerRoot D:/acardemy/TSC_REST -NodePath C:/경로/node.exe
```

NodePath는 Node.js 24 이상, UnityPath는 설치된 Unity 실행 파일을 지정합니다. 기본 UnityPath는 위 Unity 버전의 Windows 설치 경로입니다. 서버 포함 검사는 임시 포트 **3099**를 사용합니다.

과거의 27/33개 Unity Play 및 18개 C#/TS HTTP 통과 기록은 당시 콘텐츠 기준입니다. 현재 Play 검사 도구는 Player/Input System/URP 의존성을 포함하고 CSV에서 기대 바인딩을 계산합니다. B 콘텐츠 미완성이면 정상 B 플레이·온라인 전체 시나리오를 통과로 표시하지 않고 종료 코드 2로 중단합니다. 2026-10-21에는 별도로 Unity Play 스폰·정리·대기 UI 검사 38개와 실제 프리팹 확인을 수행했습니다. 전체 모델의 실제 충돌·두 PC 동시 검증과는 구분합니다. 현재 참조·수명 검토는 아래 메뉴와 Docs/EntryConsistency.md를 따릅니다.

## 8. 도식과 추가 문서

PNG와 확대 가능한 SVG를 함께 보관합니다. Windows PowerShell의 System.Drawing과 맑은 고딕을 사용하는 생성기로 다시 만들 수 있습니다.

```powershell
./Tools/Documentation/BuildReadmeDiagrams.ps1 -Project Client -OutputDirectory ./Docs/Images
```

- [CSV 상세 안내](Docs/CsvReader.md)
- [세션 책임 분리](Docs/SessionArchitecture.md), [퇴장·음성 안내](Docs/SessionLeaveAndVoice.md): 과거 화면·씬 설명은 이 README의 현재 구성을 우선합니다.
- 서버 실행·API·데이터 판정은 TSC_REST 저장소 루트 README를 참고합니다.

Git에는 코드·CSV·프리팹·필요한 `.meta`·Packages·ProjectSettings·검사 소스·문서를 공유합니다. Library, Temp, Logs, 의존성 캐시와 검사 bin/obj는 재생성합니다. 로컬 경로·자격 증명·개인 배포 설정은 공유하지 않습니다.


## 에디터 방 클리어 테스트

`Ingame` 씬을 Play한 뒤 `Tools > Game > Room Clear Test`를 엽니다. 역할 A/B를 고르고 미리보기를 시작하면 방별 클리어 버튼으로 CSV `Next`에 연결된 다음 방 외형을 생성하고 현재 방의 DoorPoint에 다음 방의 EntryPoint를 맞춰 연결합니다. 이전 방은 유지합니다. 도구는 Editor 폴더에만 존재하며 빌드에는 포함되지 않습니다.

내부 오브젝트·앵커 스폰과 서버 클리어 상태는 이번 도구에서 처리하지 않습니다. 다음 프리팹이 없으면 클리어를 확정하지 않고 오류를 표시합니다. 창 닫기·Play 종료 시 생성한 미리보기만 정리합니다. 사용법과 코드 책임은 [개발자 방 클리어 안내](Docs/DeveloperRoomClear.md)를 따릅니다.


## 1번 방 공통 앵커 플레이어 생성

시트의 공통 스폰 행에서 AnchorID를 선택하고 같은 위치·회전으로 1인칭 플레이어를 생성합니다. A는 큐브, B는 실린더를 카메라 앞 손·무전기 대체 모델로 표시합니다. 스폰·모델 구조는 [플레이어 스폰 안내](Docs/PlayerSpawn.md)를 따릅니다. Input System의 기존 Player/Move와 CHARACTER_STAT을 연결한 WASD·정수 가감속·중력·구조 충돌을 추가했습니다. 설계·변경 파일·검토 순서는 [WASD 이동 안내](Docs/PlayerMovement.md)를 참고하세요. 마우스 시점·공통 조작 잠금·캡슐 skin 설정·모델의 벽 끼임 보정을 추가했습니다. 설계·파일별 설명·검토 절차는 [마우스 시점과 충돌 안내](Docs/PlayerLookAndCollision.md)를 따릅니다. 전용 클로즈업 UI·좌클릭 상호작용·실제 다음 방 연결은 이 단위 검토 뒤 진행합니다.

## 씬 진입 정합성 검토

수정 의도·파일별 기능·연결도·현재 제한은 [씬 진입 정합성 안내](Docs/EntryConsistency.md)에 있습니다.

1. Play 종료 상태에서 **Tools > Game > Verify Ingame Wiring**: 씬 로더·첫 방·프리팹 루트·포함 모델 경로 검사.
2. Join의 **클라이언트 테스트 A**로 실제 씬 진입을 확인합니다. UI가 사라진 뒤에도 데이터/역할/플레이어가 유지되어야 합니다.
3. 정상 입장 후 **Tools > Game > Verify Gameplay Readiness**: 플레이어 비활성화 동안 준비가 해제되는지 검사.
4. B 모델/이동만 확인할 때는 **Ingame 단독 Play > Room Clear Test**를 사용합니다. 현재 로컬 B 첫 방과 플레이어 생성은 확인했으며, 온라인은 서버 CSV와 두 PC Playing을 별도로 검증합니다.

Git Pull 후 서버와 최신 로컬 CSV는 ANCHOR_INTERACT / ANCHOR_NONINTERACT / CARD / PREFAB 4개에서 다릅니다. 최신 배치의 마우스/충돌 검토는 로컬 A 테스트를 사용합니다. 온라인은 서버 CSV 번들을 사용하므로 같은 콘텐츠라고 가정하지 않습니다. 다음 방의 공간 정렬은 아직 개발자 미리보기 도구에 연결되어 있으며, 실제 런타임 문/정답 UI와 방 이동 연결은 후속 범위입니다.


## 마우스 시점·충돌 검토

Game 화면 좌클릭 → 마우스 회전/WASD, ESC → 커서 해제 및 이동·시점 정지, 좌클릭 → 복귀입니다. 포커스 복귀도 좌클릭을 기다립니다. 상하 회전은 CHARACTER_STAT.PitchLimit, 감도는 MouseSensitivity를 사용합니다.

- **Tools > Game > Verify Player Look And Collision**: 임시 벽으로 모델 당김/숨김/복원과 입력 소유권 검사.
- **Tools > Game > Inspect Player Collision**: 실제 방에서 최근 측면 접촉 collider·캡슐 반경/skin 출력. 문 앞 조기 충돌의 정확한 물체는 이 로그로 확인합니다.

마우스 기능을 추가한 당시에는 실제 커서·방 물리·위 메뉴 실행을 자동 검사하지 않았습니다. 이후 충돌 검토에서 Unity MCP로 누락된 문·서랍 Collider와 단면 메시의 뒷면 감지 누락을 재현했습니다. 문/가구 Collider 보완과 양방향 표면 조회는 [충돌 수정 안내](Docs/CollisionFixes.md)를 따릅니다. **Tools > Game > Verify R01 Collision**은 별도 임시 씬에서 실제 프리팹·캡슐과 모델 표면 조회를 검사합니다. 서버 데이터와 정상 B 콘텐츠 문제는 별도입니다.

적용 후 본 프로젝트의 Unity MCP에서 **R01 충돌 18개 네이티브 물리 검사 통과**를 확인했습니다. 문·가구 Collider/메시 일치·캡슐 차단, 실제 방 네 방향 벽 차단, 단면 양방향 모델 보호·복원, 전역 backface 설정 보존을 포함합니다. 플레이 화면의 실제 조작과 빌드 검증은 별도입니다.


## 데이터 기반 상호작용: 첫 기능 단위

[기획 작업자용 데이터 테이블 적용 가이드](Docs/DesignerDataTableGuide.md) · [구현 의도·기능별 코드 연결](Docs/DataDrivenInteraction.md)

`PREFAB.Components → COMPONENT.Script/Params → AnchorFeatureFactory → WorldInteractionTarget` 순서로 기능을 자동 구성합니다. 활성 ANCHOR_INTERACT만 클릭 대상이 되며, 스크립트를 프리팹마다 Inspector에서 수동으로 붙이지 않습니다. PREFAB CSV에는 Components 헤더가 필요합니다. 셀은 비어 있어도 됩니다.

이번 단위는 화면 중앙 대상 탐색·강조와 좌클릭 Closeup 패널·모델 미리보기·복귀입니다. PlayerPointerDriver가 입력/커서를 소유하고, 시점과 상호작용이 같은 프레임 입력을 사용합니다. Closeup은 공통 PlayerControlState 잠금으로 이동·시점을 함께 막습니다. 거리는 CHARACTER_STAT.InteractDistance에서 정수 mm로 정규화합니다.

Play 종료 상태에서 `Tools/Game/데이터 기반 상호작용 검사`으로 검사하고, 로컬 A 입장 뒤 `Tools/Game/클로즈업 테스트 대상 생성`으로 실제 선언 기반 임시 큐브를 만들어 좌클릭·돌아가기·Esc를 확인할 수 있습니다. 현재 R01의 다이얼 앵커 2030은 N이며 연결 프리팹 13045의 기능 목록도 비어 있습니다. 해당 콘텐츠를 임의로 활성화하지 않았습니다. 문은 CSV의 Door 선언으로 자동 구성됩니다. 좌클릭 문 허가·열기·닫기 연결은 아래의 개발자 미리보기 문 조작 단위를 참고합니다.

원본 Asset 엑셀의 PREFAB!B5에서 Components 추출 제외를 해제했습니다. 다만 원본의 PREFAB 73행과 현재 클라이언트 CSV 76행 및 일부 경로가 아직 다릅니다. 전체 추출 결과를 바로 덮어쓰지 말고 가이드의 임시 출력·비교 순서를 따릅니다. 서버와 클라이언트의 기존 PREFAB 경로/행 차이를 일괄 동기화한 작업은 아닙니다.

검사: 클라이언트 공통 234개(신규 상호작용/Closeup 33개 포함), 플레이어 72개, 에디터 런타임·에디터 도구·Windows 런타임 컴파일 통과. 기존 비활성 참조 경고 39개는 유지하며 콘텐츠 오류를 자동으로 숨기지 않습니다. Unity 물리·UI 자원 검사와 실제 마우스 조작 검사는 별도로 구분합니다.

수동 조작·실패·수명·빌드 검사 절차: [상호작용 테스트 가이드](Docs/InteractionTestGuide.md).

실행 확인: 연결된 Unity에서 임시 씬의 상호작용 검사 21개가 통과했습니다. 실제 좌클릭 조작·빌드 검증은 테스트 가이드에 따라 사용자님이 확인하실 항목입니다.

## 정답 입력 UI·API 연결 단위

[설계·파일별 코드 해설·기획 적용·테스트 가이드](Docs/AnswerApiConnection.md). LOCK/CONSOLE·PUZZLE·PREFAB 기능 선언으로 좌클릭 입력 UI를 생성하고 공개 IRoomAnswerService를 통해 기존 HTTP+Google Protobuf API에 연결합니다. 정답·오답·요청 실패·취소를 구분하며 중복 제출과 닫힌 패널의 늦은 응답을 제어합니다. 문 허가·열기·닫기 연결은 아래의 개발자 미리보기 문 조작 단위와 [개발자 방 테스트 안내](Docs/DeveloperRoomClear.md)를 참고합니다.

이번 작업 시작 시 최신 로컬 CSV에는 다른 작업자분이 추가 중인 PREFAB 13079·13080·13081 참조 오류가 있었습니다. CSV·엑셀·프리팹은 이번 단위에서 수정하지 않습니다. 앞의 전체 데이터 통과 수치는 이전 정상 데이터 기록이며 현재 데이터 전체 검증 결과와 구분합니다. 검증 가능한 서버 데이터로 정답 단위 검사를 실행하고 이전 정상 데이터로 회귀를 확인합니다.

이번 단위 검증: 정답 분리 39개, 이전 정상 데이터 회귀 273개, 실제 C#·서버 Protobuf HTTP 22개, Unity UI·서비스 연결 9개, 서버 157개와 에디터/Windows 컴파일 통과. 최신 CSV 전체 검증과 실제 조작·빌드는 별도입니다.


## 2026-10-21 온라인 시작 대기와 방 미리보기 보완

온라인 입장 성공만으로 Ingame을 즉시 로드하던 흐름을 변경했습니다. SessionStartGate는 서버 공개 상태를 관찰하여 두 참가자 접속 시 씬 로드를 허용하고, Playing 확인 시 조작과 대기 화면을 해제합니다. ready는 기존처럼 실제 씬·방·플레이어 준비 완료 보고이며 조작 잠금과 별개입니다. 상대 퇴장·연결 종료 시 다시 잠급니다. 로컬 테스트는 대기를 적용하지 않습니다.

Tools/Game/Room Clear Test는 Ingame 직접 Play 전용입니다. 방 외형만 만들던 방식에서 실제 RoomSceneView.Load를 공유하여 CSV 내부 오브젝트도 생성합니다. 원본 방은 숨기고 복제하여 사용하며, 다음 방 생성·소품 구성·출입구 연결이 성공한 뒤 개발자 클리어를 확정합니다. 종료 시 복제본·소품·플레이어를 정리하고 원본 활성 상태를 복원합니다. 실제 게임 입장 세션과는 동시에 사용하지 않습니다.

세부 코드 설명·도식·수동 검사는 [온라인 시작 대기와 CSV 방 미리보기](Docs/SessionStartAndPreview.md), 개발자 메뉴 사용법은 [방 클리어 테스트](Docs/DeveloperRoomClear.md)를 참고합니다.

실제 검사 결과와 현재 A 2번 방의 누락 앵커(ID 2034, R2A_LOCK)는 [최신 검증 결과](Docs/SessionStartAndPreview.md)에 기록했습니다.

### 개발자 방 미리보기의 문 조작

Room Clear Test의 플레이어도 CSV Door를 좌클릭하여 열고 닫습니다. 생성된 방의 문 개폐 테스트와 다음 방 생성은 분리합니다. 다음 방으로 이동하기 전에는 클리어 버튼으로 준비·연결을 완료해야 합니다. 개발자 문 판정은 에디터 전용 IRoomDoorService 어댑터로 분리하며 온라인 문은 기존 GameEntryService의 Protobuf 판정을 사용합니다. 공통 문 상태·회전과 정답 Closeup 입력을 분리합니다. 사용 순서는 [개발자 방 테스트 안내](Docs/DeveloperRoomClear.md)를 참고하십시오.


### Stable 빌드 텍스처 압축

최근 Stable 빌드에 포함된 일반 텍스처 170개에 BC7, 노멀맵 94개에 BC5를 적용했습니다. 원본 이미지·실제 해상도·게임 참조를 유지하고 Standalone 플랫폼의 압축 설정만 변경했습니다. Windows64 Stable 재빌드의 BuildReport 용량은 4.56GB에서 2.16GB로 약 52.6% 감소했습니다. UI 스프라이트 5개와 EXR 원본 2개는 보존했습니다. 적용 기준·의도·확인 결과·기획/아트 적용·테스트·복구는 [텍스처 압축 안내](Docs/TextureCompression.md), 파일별 변경은 [적용 목록](Docs/TextureCompressionApplied.json)을 참고합니다.


### 문 월드 Y축·관찰자 기준 Closeup 수정

임포트 모델의 로컬 Y축 대신 월드 Y축으로 문을 열고 닫습니다. Closeup은 클릭 당시 관찰자의 수평 방향과 CSV CamOffset을 조합해 보던 면을 유지하고, 미리보기 UI의 종횡비를 맞췄습니다. 큐브·실린더는 PlayerPresentation의 명시적 URP 머티리얼 참조를 사용합니다. 실제 A/B 문 높이 유지·자물쇠 앞면을 확인했고, 표현 검사 22개·공통 회귀 295개와 Windows Stable 빌드를 통과했습니다. 원인·코드 연결·정면 기준·표본 화면·새 빌드와 테스트 순서는 [문 회전과 Closeup 수정](Docs/InteractionPoseFix.md)을 참고합니다.


### 왼쪽 경첩 기준 문 개폐

문 프리팹의 DoorHinge 기준점을 고정하고 문짝 위치와 회전을 함께 계산합니다. 공통 Door와 R01_Door에 방 안쪽 기준 왼쪽 경첩을 연결했습니다. 실제 A/B 문에서 경첩 고정·중심 이동·닫힌 위치 복원을 확인했고 표현 검사 22개·Play 미리보기 54개와 Stable 빌드를 통과했습니다. 프리팹 기준점과 CSV 연결, 테스트·새 빌드는 [왼쪽 경첩 개폐 안내](Docs/DoorHingeFix.md)를 참고합니다.
