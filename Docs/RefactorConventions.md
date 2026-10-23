# 점진적 리팩토링과 향후 구현 규격

2026-10-23 추가: Join은 [Start_UI·WaitingRoom_UI 메뉴](JoinMenu.md)로 연결했습니다. config 주소 선택과 두 ready 조건은 유지하며 탈출하기로 입장하고, 이미 두 명이면 Join 대기 UI 생성을 생략합니다. 이 문서의 이전 구조 점검 검사 수와 최신 메뉴 검사 수는 구분하며 최신 결과는 README/JoinMenu를 따릅니다.

기준일: 2026-10-23. [상세 점검·수정 근거](ClientArchitectureReview.md), [종합 도식](ArchitectureReview.md)를 함께 봅니다. 실제 반영 코드와 보류 범위를 구분합니다.

## 1. 계층과 의도

```text
Bootstrapper: 생성·의존성·씬 수명
 ├─ GameEntryService: 데이터 소스 선택·등록·씬 진입
 │   └─ RoomInteractionService: 방 명령·허가 캐시·완료 이벤트
 │       └─ SessionClient → Gateway → 생성 API → HTTP/Protobuf
 ├─ IngameWorldDriver → RoomSceneAssembler → RoomSceneView
 │   └─ CSV 방·앵커·프리팹·컴포넌트 정의
 └─ PlayerSpawnDriver → LocalPlayerFactory
     ├─ 이동·시점·상호작용 Driver
     ├─ PlayerGaitDriver / GameFeedbackDriver
     └─ EndingVideoDirector → 영상·결과 View
```

입장 서비스에서 방 허가 명령을 분리한 것은 씬 전환과 퍼즐 요청의 수명을 구별하기 위해서입니다. 기존 공개 메서드는 호환 Facade로 남겨 호출부를 한 번에 교체하지 않습니다. RoomSolved는 허가 캐시를 반영한 뒤에 발행합니다. UI 요청과 결과 이벤트를 섞지 않습니다.

## 2. 데이터 정의와 실행 상태

콘텐츠 공통 계약 원본은 서버 `protocol/data/content-contract.json`입니다. 생성기는 TS/C# 기능명·퍼즐 종류·파라미터 정의를 생성하며, CSV 검증과 런타임 기능 계획이 같은 목록을 사용합니다. 이름을 추가해도 실제 Handler가 자동 구현되는 것은 아닙니다. LetterSign·ScreenText, WIRE·ARROW 등 확장 항목의 실제 콘텐츠 범위를 별도로 확인해야 합니다.

`ContentFeatureRule`은 미지원 기능·잘못된 파라미터·필수 의존 선언을 실행 전에 거부합니다. 기존 고정 검사 코퍼스는 현재 콘텐츠가 줄거나 변해도 검증 규칙을 계속 검사하도록 Tools/CsvValidation.Tests/Fixtures/Content에 보존합니다. 코퍼스는 서버 커밋 40cfefc의 데이터에 Components 선언을 보강한 검사 자료이며 런타임 데이터가 아닙니다.

사운드는 사용자 결정에 따라 CSV와 분리했습니다. GameFeedback 에셋이 사운드 설정 원본이고 CHARACTER_STAT.HeadBob만 기존 캐릭터 설정을 따릅니다. 프로젝트 전체의 설정을 무조건 같은 형식으로 강제하지 않습니다.

## 3. 요청과 이벤트

Protobuf 응답은 SessionApiClient에서 코어 계약으로 한 번 변환합니다. HttpSessionGateway는 오류 분류를 담당합니다. 과거 DTO·검증 파일은 호환 검사 때문에 남아 있지만 실제 새 요청 경로에서 중간 성공 DTO를 다시 만들지 않습니다.

ClientSessionState는 같은 해금 집합을 매 조회마다 다시 통지하지 않습니다. SnapshotApplied는 생존·상태 관찰을 위해 유지합니다. 방 로드 실패는 같은 스냅샷마다 프리팹 생성을 반복하지 않으며, 수동 재시도 명령으로 다시 시도합니다. 서버 heartbeat 3초·만료 10초는 변경하지 않았습니다.

## 4. 조립·패턴·자원 소유권

실제 입장과 개발자 방 미리보기는 RoomSceneAssembler의 배치 정책을 공유합니다. 기본 Authored는 프리팹에 저장된 좌표를 유지하고, ConnectionPoints는 이전 출구와 다음 입구를 맞춥니다. 프리팹·앵커가 잘못 연결된 상태를 임의 추측으로 숨기지 않습니다.

GameContentBuilder는 PREFAB에 선언된 참조와 활성 REGISTRY CSV만 빌드 에셋에 연결합니다. 작업 중인 모델 폴더 전체를 무차별 수집하지 않습니다. 플레이어 손은 PlayerPresentation의 직접 참조를 사용합니다.

A/B 손 모델은 기존 ModelFor(role) 선택 경로에 연결했습니다. 카메라 앞 모델에는 별도 게임 이동 권한이 없으며 Animator root motion을 끕니다. 모델 자체 Collider는 기존 규칙대로 제거합니다. 모델 가림 보정은 표현용 Bounds 근사로 처리하고 전체 Renderer 모서리 계산을 초기화 시 캐시합니다. 실제 애니메이션별 정밀 충돌을 구현한 것은 아닙니다. 모델 표시 위치·회전은 PlayerPresentation에서 조절하며 화면 검증이 필요합니다.

엔딩 설정 오류가 첫 방 플레이어 생성까지 실패시키지 않게 분리했습니다. 엔딩 대기 중 연결 종료는 독립 계약으로 관찰하고 조회를 취소합니다. 영상 숨김·재표시와 결과 화면 수명도 구분합니다.

## 5. 반복 조회와 자원 수명 규격

RoomLoadPlan은 한 CsvGameData 스냅샷에 종속되는 불변 정의 계획입니다. 읽기 전용 방 목록·첫 방·ID 조회·역할별 앵커 결과를 재사용하고 재입장 시 새 계획을 만듭니다. 전역 캐시나 Unity 오브젝트 캐시로 확장하지 않습니다. CsvGameData는 REGISTRY.Active=Y만 런타임 행으로 주입하며, 비활성/미등록 원본 검증과 명시적 도구 매핑은 별도로 유지합니다.

배경음은 전환 중/최종 프레임/최초 선택/재활성화 때만 source에 적용합니다. GameAudioCue의 기존 직렬화 번호는 고정하고 새 번호는 재사용하지 않습니다. GameFeedbackDriver의 월드 연결은 같은 소유자에 대해 멱등이고 다른 입장에는 새 Driver를 만듭니다.

영상 완료 후 결과 UI가 생성되면 영상 포트·RenderTexture를 즉시 해제합니다. 결과 UI만 유지하며 영상 재생 중 Disable/Enable과 결과 표시 후 Disable/Enable의 수명을 구분합니다. 구독 해제·취소·Dispose는 늦은 콜백과 중복 정리에 안전해야 합니다.

## 6. 앞으로 기능을 추가하는 순서

1. 기능 명령·완료 조건과 상태 소유자를 정의합니다.
2. 변경 가능한 값과 콘텐츠 ID를 검증 가능한 데이터 또는 전용 설정으로 분리합니다.
3. 순수 규칙·수명 모델을 구현하고 I/O와 Unity를 Adapter에 둡니다.
4. Bootstrapper/Factory에서 의존성을 주입합니다. 소비자가 독립적일 때만 결과 이벤트를 사용합니다.
5. 취소·중복 요청·Disable/Destroy·재입장 상태 초기화를 검사합니다.
6. 실제 거리·씬 배치·사용자 UI 등 실패 가능성이 있는 경계를 집중 검사합니다.
7. 네트워크로 보내야 하는 상태와 로컬 표현을 구별하고, 서버 권한 변경은 별도 설계합니다.

이번 변경은 assembly 전체 분리나 네트워크 라이브러리 완성이 아닙니다. asmdef 경계 정리·구형 DTO 삭제·모든 콘텐츠의 Unity Play 검증·프로파일링은 후속 범위입니다. 조명 앵커 이전과 Light Probe 베이크는 보류 상태입니다.
