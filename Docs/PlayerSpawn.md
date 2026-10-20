# 1번 방 플레이어 생성: 첫 검토 단위

2026-10-20. 공통 스폰 앵커·카메라·대체 모델의 첫 검토 단위를 설명합니다. 현재는 [WASD 이동](PlayerMovement.md)도 추가 연결되었습니다. 이후 클로즈업은 전용 UI 패널로 구성하며, 큐브/실린더는 몸체가 아니라 카메라 앞 손·무전기의 임시 모델입니다.

## 사용과 검토

1. Unity의 컴파일·임포트가 끝나면 Ingame 씬을 다시 엽니다. 1번 방 참조를 현재 시트의 Room_01_Final로 맞췄으며 조명과 기존 카메라는 유지합니다.
2. Play → Tools > Game > Room Clear Test → A 역할 → 미리보기 시작 → 플레이어 생성 버튼을 누릅니다.
3. Game 뷰에서 큐브가 오른쪽 아래에 보이고, Hierarchy의 LocalPlayer_A가 시트의 공통 스폰 앵커 위치·방향을 따르는지 확인합니다. 카메라는 발 기준 높이 1600mm 위에 있습니다.
4. 미리보기를 종료하면 생성한 플레이어를 제거하고 기존 카메라·오디오를 복원합니다. B로 다시 시작하면 같은 스폰 앵커에서 실린더가 표시됩니다.
5. 플레이어 생성 버튼은 이미 생성한 경우 중복 생성하지 않습니다. 다른 정상 게임 플레이어가 있으면 Factory가 중복 생성을 거절합니다.
6. 스폰 기능은 CharacterController·카메라·모델을 준비합니다. 후속 검토 단위인 [WASD 이동](PlayerMovement.md)이 현재 Factory에 연결되어 이동과 중력을 담당합니다. 앵커 높이를 바닥으로 재계산하거나 보정하지 않습니다. R01_Player는 플레이어 발 기준점으로 배치해야 합니다.

정상 Join → Ingame 전환도 같은 Driver/Factory를 사용합니다. 다만 서버 CSV가 아직 이전 A/B 스폰 행을 사용하면 온라인 생성은 실패합니다. 서버 데이터를 로컬 데이터로 몰래 대체하지 않습니다. 서버 CSV 일치와 기존 방 내부 오브젝트 로드/ready 문제는 별도 검토입니다. 개발자 창은 현재 로컬 CSV로 첫 검토를 진행할 수 있습니다.

## 데이터와 코드

```text
시트: RoomNo + Active + ALL/현재 역할 + 스폰 PrefabID
  → PlayerSpawnPlan: 첫 방의 스폰 행 정확히 하나 선택
  → 실제 AnchorID Transform의 월드 위치·회전
  → LocalPlayerFactory
     ├─ 공통 CharacterController
     └─ FirstPersonCamera
        └─ VisualRoot
           └─ A 큐브 / B 실린더 / 추후 등록한 모델 프리팹
```

| 코드 | 기능과 의도 |
|---|---|
| PlayerSpawnPlan | Unity와 독립적인 시트 선택·캐릭터 설정 검증. 공통/전용 스폰이 동시에 선택되면 임의 우선순위 없이 거절합니다. |
| PlayerPresentationSettings | 스폰 프리팹 ID, 캐릭터 설정 선택 ID, 클리핑, 모델 위치·회전·크기와 역할별 모델 프리팹을 보관합니다. |
| LocalPlayerFactory | 앵커 유일성·중복 플레이어 검사 후 생성합니다. 임시 모델 충돌체는 꺼서 몸체 충돌 형태에 영향을 주지 않게 합니다. |
| FirstPersonPlayerRig | 화면 모델과 카메라 소유권. 같은 씬의 화면 출력 Game 카메라/AudioListener를 기록·비활성화하고 플레이어 비활성화 시 복원합니다. 기존 카메라의 태그가 없어도 동작하며 RenderTexture 카메라는 제외합니다. |
| PlayerSpawnDriver | Unity 수명·조립. 첫 방을 찾아 공통 Factory를 사용하고 스폰 완료 이벤트를 보냅니다. |

`Assets/Resources/PlayerPresentation.asset`의 Model A/B에 작업이 끝난 손·무전기 프리팹을 등록하면 생성 코드를 바꾸지 않고 교체할 수 있습니다. null이면 A Cube/B Cylinder를 사용합니다. 모델 프리팹은 별도 Camera·AudioListener·게임 입력 스크립트가 없는 시각 모델로 준비합니다. 프리팹 자체 크기는 유지하며 표시 위치는 VisualRoot의 정수 mm 설정으로 조정합니다. Model Size Mm은 기본 도형 크기만 지정합니다.

CHARACTER_STAT은 GAME_CONFIG의 설정 선택 행 16010이 가리키는 프로필을 사용합니다. A/B를 이유로 17001/17002를 각각 선택하지 않습니다. 시트의 소수 m 값을 한 번 정수 mm로 정규화하고 시야각은 정수 millidegree로 보관합니다. Unity 카메라·충돌체·Transform 적용 경계에서만 float로 변환합니다. 정수 단위 사용이 물리 결정성을 보장한다는 의미는 아닙니다.

일반 방 오브젝트 로더에는 외부 생성기가 담당하는 PrefabID를 전달하여 플레이어 스폰 행을 제외합니다. 스폰 마커 13001을 일반 프리팹 경로로 Instantiate하지 않습니다. 내부 오브젝트와 플레이어가 같은 행을 중복 처리하지 않도록 분리한 것입니다.

## 실제 실행 확인 범위

순수 C# 정의 검사와 Unity 참조 컴파일을 수행합니다. Unity Play에서의 실제 렌더링·모델 가시성·카메라 전환은 사용자님 검토 대상입니다. 구현하지 않은 다음 기능을 이번 검토의 통과 항목으로 간주하지 않습니다.
