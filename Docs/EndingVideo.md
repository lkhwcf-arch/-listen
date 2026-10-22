# 마지막 방 협동 정답 완료와 엔딩 영상

기준일: 2026-10-22. 두 플레이어가 각자의 마지막 7번 방 정답을 맞히면 엔딩 영상을 재생합니다. 먼저 맞힌 플레이어는 상대의 정답 완료를 기다립니다. 영상 종료 후 Result_UI에 탈출 시간을 표시하고 확인 버튼으로 종료합니다. 측정·사운드·결과의 최신 규격은 [게임 표현 가이드](GameFeedback.md)를 따릅니다.

## 1. 기능과 데이터 조건

```text
ROOM_A 1013 / RoomNo 7 / PuzzleID 5007 ─┐
                                      ├─ ExitRule=BothSolved, Next=0
ROOM_B 1014 / RoomNo 7 / PuzzleID 5008 ─┘

A 정답 제출 → 서버 슬롯 비교 → correct=true, doorAllowed=false
  └─ A: 대기 화면 + 이동·시점·상호작용 잠금
       └─ 1초 간격으로 기존 방 허가 API에 room_no=7 요청
B 정답 제출 → 서버가 양쪽 완료 확정 → correct=true, doorAllowed=true
  ├─ B: 즉시 영상 준비·재생
  └─ A: 다음 허가 조회에서 true → 영상 준비·재생
영상 실제 시작 → 탈출 시간 고정
영상 완료 → Result_UI 표시·조작 잠금 유지 → VideoCompleted 이벤트
확인 클릭 → 빌드 종료 / 에디터 Play 종료
```

B가 먼저 맞혀도 같은 흐름입니다. 영상 시작 시각을 프레임 단위로 동기화하지 않으며 먼저 맞힌 쪽에는 조회 간격과 통신 시간만큼 지연이 생길 수 있습니다.

마지막 방 번호를 코드에 7로 고정하지 않고 역할별 활성 ROOM 중 Next=0인 마지막 행에서 읽습니다. 마지막 행은 정확히 하나이고 ExitRule=BothSolved여야 합니다. 현재 데이터는 A/B 모두 7번 방입니다. 완료 권한은 서버 RoomProgression이 가지며 클라이언트가 정답을 추측하지 않습니다.

## 2. 계층별 코드와 구성 의도

| 계층 / 파일 | 책임과 연결 |
|---|---|
| 데이터 `Data/EndingVideoAssets.cs` | CSV 영상 키와 빌드 VideoClip 참조, 준비 제한 시간·조회·재시도 간격 검증 |
| 서비스 `World/GameEntryService.cs` | RoomPermissionService의 정답 처리·허가 캐시 반영이 끝난 뒤 RoomSolved(roomNo) 이벤트 발행 |
| 계약 `World/IRoomCompletionService.cs` | 정답 완료 이벤트·허가 캐시 조회·비동기 허가 조회를 공개하며 HTTP·토큰·Unity 객체를 숨김 |
| 규칙 `World/EndingCompletionController.cs` | Playing → WaitingForPartner → Ready 상태와 조회 간격·중복 방지·취소 수명 관리 |
| 조립 `Player/PlayerSpawnDriver.cs` | 플레이어 생성 시 CSV·역할·서비스·한국어 폰트를 Director에 명시적으로 주입 |
| Unity 조립 `World/EndingVideoDirector.cs` | 완료 상태를 대기 UI·공통 조작 잠금·영상 재생에 연결하며 플레이어 수명으로 정리 |
| 표현 `World/EndingWaitingView.cs` | 먼저 맞힌 사용자에게 한국어 대기·재시도 메시지 표시 |
| 재생 상태 `World/EndingPlaybackController.cs` | Idle → Preparing → Playing → Completed/Failed, 준비 시간 초과·늦은 콜백·중복 완료 차단 |
| 재생 계약 `IEndingVideoPlayback` | 순수 C# 상태 로직과 실제 디코더를 분리하는 포트 |
| Unity Adapter `World/EndingVideoView.cs` | VideoPlayer 이벤트를 포트로 변환하고 RenderTexture·전체 화면 UI·오디오·자원 정리 담당 |

정답 요청은 명령이고 RoomSolved는 처리 완료 이벤트입니다. Observer 방식으로 독립적인 엔딩 기능에 결과를 알리며 전역 이벤트 버스를 추가하지 않습니다. 상태 전이는 순수 C# 컨트롤러에서 관리하고 VideoPlayer·UI는 Adapter로 분리하여 가짜 서비스와 재생기로 검사할 수 있습니다. 별도의 State 클래스 계층은 현재 상태 수에 비해 불필요하므로 enum과 명시적 전이를 사용했습니다.

## 3. 서버 API와 데이터 연결

```text
정답 UI → IRoomAnswerService → GameEntryService → RoomPermissionService
  → ISessionRoomService → HTTP Gateway → Google Protobuf
  → POST /sessions/{id}/rooms/answer
  → 서버 ROOM → PUZZLE → PUZZLE_SLOT 비교 → BothSolved 판정
  → 응답 캐시 반영 → RoomSolved → EndingCompletionController

대기 중 → IRoomCompletionService.CheckRoomDoorAsync
  → 같은 서비스·Gateway → POST /sessions/{id}/rooms/door
  → 서버의 BothSolved 결과 true → Ready
```

door API는 허가 조회이며 물리적인 문 개방 명령이 아닙니다. 기존 두 API와 Protobuf를 재사용하여 신규 패킷·생성 코드·서버 도메인 규칙 변경은 필요하지 않습니다. 일반 스냅샷에는 퍼즐별 완료 정보가 없으므로 자기 정답 이후에만 허가 API를 조회합니다. 대기 요청은 동시에 하나만 실행하고 응답 뒤 최소 설정 간격을 둡니다. 마지막 방 Next=0이므로 이 조회로 방을 추가하지 않습니다.

온라인 데이터는 서버 번들이 기준입니다. 현재 등록된 16개 CSV의 내용은 서버와 클라이언트가 일치합니다. 서버 data에는 REGISTRY에서 제외된 기존 CSV 7개가 남아 있어 미등록/빈 테이블 경고가 발생하지만 오류는 없습니다. 이번 엔딩 변경은 CSV를 복사하거나 삭제하지 않았습니다. 클라우드 반영에는 최신 서버 이미지 배포가 필요합니다.

## 4. 영상 설정과 수명

Unity Project에서 `Assets/Resources/EndingVideo.asset`을 선택합니다.

| 필드 | 현재 값 / 의미 |
|---|---|
| path | Assets/2.Model/EndingVideo/EndingVideo.mp4 |
| clip | 위 경로의 실제 VideoClip. 파일 교체·meta GUID 변경 시 다시 연결 |
| prepareTimeoutMs | 15000ms, 디코더 준비 제한 시간 |
| completionPollIntervalMs | 1000ms, 먼저 정답을 맞힌 쪽의 완료 조회 간격 |
| retryIntervalMs | 5000ms, 영상 준비·재생 실패 후 재시도 간격 |

CSV GAME_CONFIG 16011은 string 경로이고 등록한 path와 일치해야 합니다. 임의 경로를 자동으로 대체하지 않습니다. 영상은 서버 다운로드·모델 프리팹 탐색 없이 이 VideoClip 참조로 빌드에 포함합니다.

검은 배경 위에 원본 영상 비율을 유지하고 첫 오디오 트랙이 있으면 활성화합니다. 정상 종료 뒤 Result_UI를 표시합니다. 준비 제한 시간 초과·디코더 오류는 완료와 구분하며 한국어 로그·화면을 표시하고 자동 재시도합니다. 플레이어 비활성화 시 표현 구독·화면·조작 잠금을 해제하고 영상을 일시 정지합니다. RenderTexture와 영상 상태는 재활성화를 위해 유지하며 파괴 시 해제합니다. 플레이어 제거 시 정답 서비스 구독과 진행 중 완료 조회도 취소합니다.

이동·시점·좌클릭은 기존 PlayerControlState.BlockForUi 토큰으로 잠급니다. 먼저 맞힌 사용자만 잠기므로 상대는 계속 입력할 수 있습니다. Time.timeScale·heartbeat·일반 세션 조회를 중지하지 않으며 Vivox 연결도 유지합니다. 세션 종료 시 서버 확인이 실패하면 엔딩을 추정하지 않고 대기·재시도를 유지합니다.

## 5. 검사와 실제 테스트

### 5.1 자동 검사

클라이언트 루트에서 .NET 10 SDK로 실행합니다.

```powershell
dotnet run --project Tools/Ending.Tests/Ending.Tests.csproj
```

협동 대기·양쪽 정답 순서·중복 조회·오류 후 복구·취소·늦은 응답, 영상 준비·재생·완료·준비 시간 초과·중복 이벤트를 검사합니다. 이 검사는 실제 디코더·스피커·두 PC의 프레임 출력까지 검증하지 않습니다.

### 5.2 두 클라이언트 온라인 검사

1. 최신 서버와 새 A/B 빌드로 같은 세션에 입장합니다.
2. 마지막 방에서 한쪽은 오답을 입력합니다. 어느 쪽에도 엔딩이 시작되면 안 됩니다.
3. A만 정답을 맞힙니다. A에는 한국어 대기 화면이 나오고 이동·시점·좌클릭이 잠겨야 합니다. B는 계속 조작·정답 입력이 가능해야 합니다.
4. B도 정답을 맞힙니다. 문 조작 없이 B 영상이 시작되고 A도 다음 완료 확인 후 영상을 시작해야 합니다.
5. 영상 비율·음성·조작 잠금·영상 종료 후 Result_UI 표시를 확인합니다. 확인 버튼은 빌드 종료 또는 에디터 Play 종료로 연결됩니다. Enter 종료는 사용하지 않습니다.
6. 새 세션에서 B 먼저/A 나중 순서도 확인합니다. 이전 세션의 정답 상태는 재사용하지 않습니다.
7. Console 또는 Player.log의 `[엔딩 대기]`, `[엔딩]` 메시지와 서버 rooms/answer·rooms/door 접근 로그를 확인합니다. 대기 중 doorAllowed=false는 정상입니다.

### 5.3 로컬 개발자 도구의 범위

Tools → Game → Room Clear Test는 개발자 문 허가로 콘텐츠를 확인하는 도구입니다. 정답 서비스가 없는 이 경로에는 엔딩을 설치하지 않아 문 열기만으로 영상을 시작하지 않습니다. Join의 단일 클라이언트 로컬 테스트는 상대 정답을 제공하지 않으므로 BothSolved를 완료할 수 없습니다. 협동 조건은 두 클라이언트 온라인으로, 독립 상태 전이는 위 자동 검사로 확인합니다.

영상이 안 나오면 EndingVideo.asset의 clip, GAME_CONFIG 16011, 양쪽 최종 방 Next·ExitRule·PUZZLE 연결, 서버의 두 정답 결과, 클라이언트 로그 순서로 확인합니다.

## 6. 결과 화면 연결

EndingVideoDirector가 영상 완료를 받은 뒤 EndingResultView로 Result_UI를 생성합니다. 탈출 시간은 씬 진입부터 VideoPlayer.started까지 한 번 측정합니다. 확인 버튼은 Application.Quit으로 연결하며 에디터에서는 Play를 종료합니다. 서버 기록 저장·명시적 퇴장·Vivox 종료 완료 대기는 후속 범위입니다. 별도 퇴장 요청이 없어도 기존 heartbeat 만료가 참가자를 정리합니다.

## 7. 이번 반영의 검증 결과

| 검사 | 결과 |
|---|---|
| 엔딩 협동 완료·재생 상태/수명 | 39개 통과, 반영된 클라이언트 검사 프로젝트로 실행 |
| 기존 정답 입력·API 연결 | 39개 통과 |
| 시작 조건·입장·접속 설정 | 12개·26개·31개 통과 |
| 실제 로컬 HTTP + Protobuf | A 선행/B 선행 2개 시나리오 통과 |
| 최신 클라이언트 CSV | 16개, 오류 0·경고 0 |
| 변경된 런타임 소스의 Unity DLL 참조 C# 컴파일 | 오류 0, SerializeField 참조 필드의 정적 분석 경고만 발생 |
| Unity 에디터 전체 임포트·Play / 실제 영상·음성 출력 | 배치 에디터 시작 실패로 미검증. 두 클라이언트 실제 검사는 위 가이드에 따라 별도 확인 필요 |
