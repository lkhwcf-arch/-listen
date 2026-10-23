# 세션 역할 경계

2026-10-23 갱신. [종합 점검](ArchitectureReview.md)과 [최신 구현 규격](RefactorConventions.md)을 함께 봅니다. 게임 요청/응답은 HTTP+Protobuf이며 health/OpenAPI 등 운영 경로의 JSON과 구분합니다.

현재는 같은 Unity 프로젝트에서 개발합니다. 별도 프로젝트/DLL 배포는 구성하지 않습니다.

## 공개 계약

`Session/Contracts`의 `ISessionConnectionService`, `SessionInfo`, `SessionCommandResult`, `SessionFailureCode`, `ClientSessionPhase`만 소비자에게 제공합니다.

- `SessionInfo`는 변경할 수 없는 상태 값입니다. 토큰, Wire DTO, Apply 메서드가 없습니다.
- `StateChanged`는 반영 완료된 새 상태 값을 전달합니다. 이전에 받은 값은 바뀌지 않습니다.
- `PhaseChanged`는 실제 단계가 바뀐 경우에만 한 번 전달하며, 호출 시 공개 상태도 이미 갱신되어 있습니다.
- 명령은 성공/취소/실패 결과를 반환합니다. 예상되는 통신 오류를 UI 예외로 전달하지 않습니다.
- 한국어 안내는 Presentation이 공개 실패 코드를 해석합니다.

## 책임과 의존성

| 영역 | 책임 | 참조 |
|---|---|---|
| Presentation | 입력, 상태 표시, 문구 | 공개 계약 |
| Unity 연결 | 씬 준비, Update, 수명 | 공개 계약, 공통 시간/준비 검사 |
| SessionClient/Coordinator | 상태 소유, 인증 정보, 요청 순서 | 공개 계약, ISessionGateway |
| HTTP Gateway | HTTP 결과/DTO를 내부 데이터와 오류로 변환 | ISessionGateway, API Client |
| API Client | 생성 HTTP 계약·Google Protobuf, 경로/헤더·전송/응답 검증 | UnityWebRequest 전송 어댑터, 생성 메시지 |
| Bootstrapper | 구체 구현 구성 및 종료 | 각 구현의 조립 지점 |

UI/Unity 자동화 -> 공개 계약 <- SessionClient -> ISessionGateway <- HttpSessionGateway

`ClientSessionState`, `SessionCoordinator`, `SessionRequestRunner`, Gateway 데이터, API Client 및 Wire DTO는 internal 구현입니다. 인증 토큰은 이 내부에서만 이동합니다.

## 실행 흐름

1. Bootstrapper가 서비스와 Unity 드라이버를 구성합니다. 아직 서버 요청은 보내지 않습니다.
2. UI 또는 테스트 메뉴가 공개 서비스에 입장을 요청합니다.
3. 서비스가 Factory로 통신 구현을 만들고 Coordinator에 주입합니다.
4. 응답 검증과 변환 후 내부 상태를 갱신하고 읽기 전용 상태를 알립니다.
5. Unity 드라이버는 입장 후 준비 검사/자동 조회를 진행합니다.
6. 실제 씬과 필수 객체의 준비를 확인한 뒤 공개 명령으로 로딩 완료를 보고합니다.

## 시간과 수명

- 간격은 int ms, 경과 시간은 long ms입니다. 기존 공통 시간 모듈을 재사용합니다.
- Unity 메인 스레드에서 요청과 Tick을 호출합니다.
- 드라이버 비활성화는 해당 자동 작업을 취소합니다.
- Bootstrapper 제거는 서비스의 진행 중 요청을 취소하고 상태 구독을 해제합니다.
- 서버가 최종 phase를 결정합니다. 클라이언트 준비 보고는 플레이 시작 명령이 아닙니다.

## 현재 제한과 향후 분리

현재 소스는 Unity의 기존 어셈블리 안에 있습니다. internal은 같은 어셈블리 내부 접근까지 막지는 않습니다. 공개 계약만으로 UI를 별도 컴파일하고, Unity/HTTP 없이 세션을 검사하여 의존성 방향을 검증합니다.

향후 어셈블리/라이브러리 분리 시 이 계약을 유지하고 구현을 옮깁니다. 전송 교체, P2P/호스트 권한, 전용 서버 전환에는 각각 동기화와 권한 설계가 추가로 필요합니다.

정상 퇴장·Vivox 자동 연결·HTTP/Protobuf 실통신은 구현했습니다. 클라우드 배포 도구도 서버 저장소에 있습니다. 기존 참가자의 신분 복구와 다중 세션은 후속 범위입니다.


## 생존 확인과 연결 종료

- 공개 계약은 HeartbeatAsync, Disconnect, Disconnected 이벤트와 SessionConnectionStatus/DisconnectReason을 제공합니다.
- 게임 phase(Waiting/Loading/Playing)와 연결 상태(NotConnected/Connected/Disconnected)는 별개입니다.
- 입장 확인 시점부터 3000ms 간격으로 전송하고 마지막 성공한 생존 확인 응답 이후 10000ms면 로컬 연결을 종료합니다.
- SessionHeartbeatSettings는 요청 제한 2000ms < 전송 간격 3000ms < 연결 제한 10000ms를 검증합니다.
- UnityWebRequest.timeout API 경계에서만 ms를 정수 초로 올림 변환합니다. 서버와 클라이언트 설정은 현재 각각 구성하며 자동 협상하지 않습니다.
- 일반 조회/보고와 생존 확인은 SessionRequestRunner가 소유하는 독립 실행 경로입니다. 경로별 최대 하나, 일반 명령 대기 예약은 하나입니다.
- 정상 상태 조회 성공은 생존 확인 응답 제한 시간을 갱신하지 않습니다. 단발 통신 오류는 재시도하며 만료된 참가자/없는 세션/토큰 누락은 즉시 종료합니다.
- 늦은 성공 응답은 이미 지난 연결 제한 시간을 되돌리지 못합니다.
- 종료 시 토큰과 로컬 상태를 지우고 연결 수명의 요청을 취소합니다. StateChanged 이후 Disconnected를 한 번 전달합니다.
- 공개 상태에 참가 토큰, DTO, 전송 예외를 넣지 않습니다. Presenter만 종료 이벤트를 한국어 알림으로 해석하며 View/Dialog는 통신을 모릅니다.
- 확인 버튼은 알림만 닫습니다. 재입장은 사용자 명령으로 새 ID와 토큰을 발급받는 신규 입장이며 기존 세션 복구는 아닙니다.
- 새 참가자 ID를 감지하면 기존 조회와 준비 보고 자동화도 다시 시작합니다. 이전 연결의 늦은 결과는 새 연결에 반영하지 않습니다.
- SessionHeartbeatDriver는 Update와 취소/구독 수명만 담당합니다. 비활성화 시 요청을 취소하며 제거 시 Bootstrapper가 서비스 전체를 정리합니다.
- PlayerSettings.runInBackground를 활성화하여 다른 창으로 전환해도 생존 확인 Update가 진행되도록 합니다. OS 절전/강제 일시정지까지 보장하지 않습니다.
- 별도 알림창은 SessionDisconnectDialog Factory가 기존 Canvas와 TMP 폰트로 생성하며 화면 수치의 float는 Unity 표시 API에만 사용합니다.

정상 퇴장 API·Vivox 자동 연결·Protobuf 실통신은 구현했습니다. 기존 세션 신분 복구는 후속 범위입니다.


## 정상 퇴장과 음성 연결

공개 세션 서비스의 LeaveAsync는 Leaving을 공개하고 기존 연결 요청을 취소한 뒤 퇴장 명령을 독점 실행합니다. 성공 여부와 무관하게 로컬 인증 정보를 제거하며 통신 실패는 LeaveUnconfirmed로 안내합니다. 정상 퇴장에서는 연결 끊김 팝업을 띄우지 않습니다.

SessionVoiceCoordinator는 공개 게임/음성 계약만 참조하여 입장 후 자동 음성 연결, 퇴장/연결 종료 시 즉시 송수신 차단과 로그아웃을 조정합니다. 같은 세션/참가자의 반복 상태 이벤트는 음성 재접속을 유발하지 않습니다.

VoiceChatService가 연결과 정리를 직렬화하고, VivoxVoiceGateway만 Unity Services/Vivox SDK를 참조합니다. SDK가 취소를 지원하지 않는 작업은 완료를 기다린 뒤 보상 정리합니다. 프로세스 내 다른 기능의 로그인을 임의로 종료하지 않습니다.

음성 화면은 IVoiceChatService.StateChanged를 구독하는 별도 Presenter입니다. 게임 접속 Presenter와 HTTP 서비스는 Vivox를 알지 못합니다. 음성 실패가 게임 연결 상태를 직접 변경하지 않습니다.

현재 VoiceBootstrapper는 Join 씬의 게임 수명 객체에 연결되어 있습니다. 과거 SampleScene/SessionJointest 검사는 초기 개발 기록입니다. Unity Authentication 익명 인증을 사용하고 서버가 시작마다 발급하는 고유 세션 ID에서 채널명을 계산합니다. 채널명 계산에는 서버 접속 IP를 사용하지 않습니다.

에디터에서 실제 Vivox 초기화/인증/로그인/AudioOnly 채널 입장, 정상 퇴장/재입장, 서버 중단 후 음성 종료를 확인했습니다. 두 PC 간 실제 음성 송수신과 새 Windows 빌드는 사용자 검토 항목입니다.

기능별 상세 해설과 검사는 [SessionLeaveAndVoice.md](SessionLeaveAndVoice.md)를 참고하세요.
