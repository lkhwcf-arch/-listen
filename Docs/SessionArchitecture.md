# 세션 역할 경계

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
| API Client | UnityWebRequest, HTTP 경로/헤더, JSON | Unity, Wire DTO/검증 |
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

퇴장/재접속, Protobuf 실통신 전환, 클라우드 자동 배포는 아직 후속 작업입니다.
