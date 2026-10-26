# WASD 이동: Input System과 CHARACTER_STAT

> 문서 대조일: **2026-10-26**. 현재 코드·설정을 기준으로 설명을 점검했습니다. 본문의 과거 검사 수치·실행 기록은 해당 날짜의 결과입니다. [현재 상태·문서 검토 범위](문서_현행화_점검표.md)를 함께 확인합니다.

> 2026-10-20 후속 변경: 최신 R01/CSV 재배치 기준의 마우스 시점·공통 조작 잠금·충돌 표현 및 검토 결과는 [PlayerLookAndCollision.md](PlayerLookAndCollision.md)를 우선합니다. 이 문서의 이전 개수·카드 포함 경로·CSV 일치 기록은 당시 기준입니다.


2026-10-20. 이번 검토 단위는 로컬 플레이어의 WASD·가감속·중력·바닥/벽 충돌입니다. 공통 스폰과 A/B 손 모델을 사용합니다. 마우스 시점·Closeup·좌클릭·정답/문은 현재 연결되어 있습니다. 점프·달리기 동작은 현재 이동 계약에 없습니다.

## 데이터와 단위

원본 `EXCEL/SILENT_Settings_Datatable.xlsx`의 CHARACTER_STAT 행 7/8과 현재 CSV를 확인했습니다. 런타임은 CSV를 읽습니다. 시트 저장만으로 CSV나 서버 데이터가 자동 갱신되지는 않습니다.

`GAME_CONFIG.ID=16010 → Value=17001 → CHARACTER_STAT.ID=17001`을 선택합니다. A/B 모두 같은 선택 규칙이며 역할로 17002를 선택하지 않습니다. 현재 CSV에는 17001 행만 있습니다. 과거 QA 프로필 17002가 현재도 존재한다고 가정하지 않습니다.

| 컬럼 | 기본 17001 | 내부 정수 | 기능 |
|---|---:|---:|---|
| WalkSpeed | 3 | 3000 mm/s | 최고 이동속도 |
| Acceleration | 10 | 10000 mm/s² | 키를 누를 때 가속도 |
| Deceleration | 12 | 12000 mm/s² | 이동 키를 모두 놓을 때 감속도 |
| EyeHeight | 1.6 | 1600 mm | 기존 발 기준 카메라 높이 |
| CapsuleHeight / CapsuleRadius | 1.8 / 0.3 | 1800 / 300 mm | 기존 충돌 캡슐 크기 |
| StepOffset | 0.3 | 300 mm | 작은 단차를 넘는 높이 |
| FOV | 70 | 70000 millidegree | 기존 카메라 시야각 |

Unity 1단위=1m를 기준으로 해석합니다. 공통 CharacterStatUnits에서 소수를 정수로 반올림하고 범위를 검증합니다. PlayerCharacterSettings는 설정 선택과 이 공통 정의 전달을 맡으며 CSV 검사도 같은 정수 정의를 사용합니다. 이동속도·가감속은 동작 가능한 양수와 고정소수점 안전 범위를 검사합니다. MouseSensitivity/PitchLimit은 시점, HeadBob은 보행, InteractDistance는 상호작용이 소비합니다. CloseUpOpenMs의 패널 열기 보간은 현재 적용하지 않습니다.

## 코드와 연결

```text
검증 CSV / GAME_CONFIG
  → PlayerCharacterSettings (불변 정의·선택·검증·단위 변환)
  → PlayerSpawnPlan.Character ──────────────────┐
                                              │ 주입
InputSystem_Actions / Player / Move            │
  → InputSystemMoveInput (개인 복제 입력 액션)  │
  → PlayerMoveCommand (정수 축 명령)           ↓
  → PlayerMovementMotor.Tick(command, elapsedMs)
      속도·가감속·대각선 제한·중력·잔여 변위
  → PlayerMoveStep (정수 mm 변위)
  → PlayerMovementDriver / CharacterController.Move
      로컬→월드 방향, m 변환, Unity 충돌
```

| 파일 | 추가/수정과 의도 |
|---|---|
| PlayerCharacterSettings.cs | 추가. 기존 PlayerSpawnPlan의 설정 조회·단위 변환을 추출해 스폰과 이동이 공유합니다. 정의만 보유합니다. |
| PlayerSpawnPlan.cs | 수정. Character 설정을 보유하며 기존 높이 등의 접근자는 공통 설정으로 전달합니다. |
| PlayerMoveCommand.cs | 추가. 입력 축을 -1000..1000 정수로 전달합니다. 장치나 Unity 객체 참조가 없습니다. |
| InputSystemMoveInput.cs | 추가. IPlayerMoveInput의 Adapter. 기존 Move만 Clone해 Keyboard&Mouse 바인딩으로 사용합니다. 기존 방향키도 유지됩니다. 원본/UI/Attack/Jump 액션 수명을 건드리지 않습니다. |
| PlayerMovementMotor.cs | 추가. 변경 가능한 이동 속도와 누적 잔여량의 소유자입니다. Unity·입력 장치·HTTP와 독립적입니다. |
| PlayerMovementDriver.cs | 추가. Update·포커스·활성 수명·시계와 CharacterController를 연결합니다. 공통 PlayerControlState와 Closeup 잠금이 현재 이동을 차단합니다. |
| LocalPlayerFactory.cs | 수정. 기존 카메라·캡슐 생성 후 Input·Clock·설정을 Driver에 주입합니다. 실패 시 생성물/입력을 정리합니다. |
| PlayerPresentationSettings.cs / 에셋 | 수정. 기존 입력 에셋 참조, 정수 프레임 제한·접지·낙하 제한을 추가합니다. 이동속도를 중복 저장하지 않습니다. |
| Room_01_Final.prefab | 수정. Room_01_A_Structure 메시를 사용하는 비 Convex MeshCollider 1개를 추가합니다. 카드·서랍·독립 문에는 자동으로 충돌체를 만들지 않습니다. |
| RoomClearTestWindow.cs | 수정. 기존 생성 버튼·정리 흐름을 재사용하며 WASD 안내로 갱신합니다. |
| PlayerMovementChecks.cs | 추가. 원본 씬을 변경하지 않는 임시 바닥/벽 충돌·입력 수명 검사입니다. |

Factory는 조립, Adapter는 장치/물리 기술 경계, Command는 입력 전달 책임을 맡습니다. 매 프레임 로컬 호출을 전역 이벤트로 바꾸지 않습니다. 기존 Spawned 완료 이벤트는 유지합니다. 현재 MotionApplied 완료 이벤트를 손 애니메이션과 보행 표현이 관찰합니다.

## 계산과 수명

- 키 입력이 있으면 Acceleration으로 목표 벡터에 접근하고 모든 키를 놓으면 Deceleration으로 정지합니다. 방향을 바꾸면 Acceleration으로 새 방향에 접근합니다.
- 대각선 명령은 정수 벡터 길이로 제한해 WalkSpeed를 넘지 않습니다.
- 1ms 단위로 적분하고 속도를 정수 μm/s로 유지합니다. mm 미만의 변위는 다음 Tick에 넘겨 프레임마다 버리지 않습니다.
- 기존 StopwatchMillisecondClock을 주입합니다. 긴 중단 뒤 최대 100ms까지만 진행하고 나머지는 따라잡지 않습니다. Time.timeScale=0은 정지하며 0 이외 slow motion 배율 처리는 이번 범위에 없습니다.
- 시트에는 중력 컬럼이 없어 프로젝트 Physics.gravity.y를 -9810mm/s²로 변환합니다. 현재 월드 Y축 아래 중력만 지원합니다. 최대 낙하속도 50000mm/s와 접지 보정 2mm는 에셋의 정수 엔진 어댑터 설정입니다.
- 아래 충돌 시 낙하 속도·잔여량을 초기화합니다. 기존 R01_Player 발 위치를 임의 재배치하지 않습니다.
- 포커스 해제·비활성화·SetMovementEnabled(false)는 입력/속도를 해제하고 Destroy는 복제 InputAction을 Dispose합니다.

Unity Vector/Transform/CharacterController와 Input System을 읽는 경계에는 float가 필요합니다. 핵심 이동 상태·가감속·경과 시간은 정수입니다. 실제 Physics 충돌과 실제 프레임의 명령 시점까지 재현 가능한 결정적 시뮬레이션이라는 의미는 아닙니다. 위치 동기화·서버 권위 이동·replay는 별도 기능입니다.

## 사용자님 검토

1. 임포트·컴파일이 끝나면 PlayerPresentation 에셋의 Input Actions가 기존 InputSystem_Actions를 참조하는지 확인합니다.
2. Ingame → Play → Tools > Game > Room Clear Test → A → 미리보기 시작 → 플레이어 생성.
3. Game 뷰를 클릭하고 W/S/A/D로 이동합니다. 앞 방향은 플레이어의 Y 회전 기준이며 마우스 시점도 연결되어 있습니다.
4. W+D가 직진보다 빠르지 않고, 키를 놓으면 짧게 감속하고 멈추는지 확인합니다. 기본값은 약 300ms에 최고속도, 키를 완전히 놓으면 약 250ms에 정지합니다.
5. 바닥을 통과하지 않고 구조 벽에 막히는지 확인합니다. 계단·메시 틈은 실제 씬에서 별도 검토합니다. 문 허가·개폐와 충돌 설정은 현재 연결되어 있습니다. 각 원본 프리팹의 충돌체·경첩 배치는 실제 화면에서 확인합니다.
6. Unity 밖으로 포커스를 옮겼다 돌아온 뒤 키를 놓으면 계속 움직이지 않아야 합니다. 미리보기 종료 후 기존 카메라/오디오가 복원되는지도 확인합니다.
7. B로 다시 시작해 같은 스폰 규칙·속도와 B 손 모델을 확인합니다.
8. Play 중 Tools > Game > Verify Player Movement를 실행해 입력 참조/바인딩/복제 수명/임시 바닥·벽/disable·destroy/구조 Collider를 검사합니다. 기존 Verify Player Spawn도 사용할 수 있습니다.

순수 검사: 프로젝트 루트에서 `dotnet run --project Tools/PlayerSpawn.Tests -- .` (.NET 8 SDK). 스폰·정수 이동 검사 45개를 같은 소스의 임시 net10 프로젝트에서 통과했습니다. Unity 6.2 참조 컴파일은 Editor/빌드 정의를 검사합니다. 실제 Play/키 입력/물리/메뉴 실행은 여기에서 통과했다고 간주하지 않습니다.

온라인은 서버 CSV를 사용합니다. 서버 없는 검사는 로컬 CSV입니다. 현재 생성·입력 수명과 준비 조건은 [진입 안내](EntryConsistency.md), 모델과 걷기 전환은 [손 안내](PlayerHandAnimation.md)를 따릅니다.
