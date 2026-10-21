# 에디터 방 클리어·CSV 오브젝트 테스트

2026-10-21. 실제 게임 로더를 재사용하여 방 외형과 CSV 내부 오브젝트를 함께 생성하는 개발자 도구입니다. 플레이어 빌드에는 포함되지 않습니다.

## 사용 순서

1. Play를 끄고 Tools → Game → Rebuild Content References를 실행합니다.
2. Ingame 씬을 직접 열고 Play합니다. Join을 통한 실제 입장 세션과 함께 사용하지 않습니다.
3. Tools → Game → Room Clear Test를 열고 역할 A/B 선택 → 미리보기 시작을 누릅니다.
4. 씬의 미로드 RoomSceneView가 있으면 원본을 숨기고 복제합니다. 없으면 CSV RoomPrefabID로 생성합니다.
5. 실제 RoomSceneView.Load가 현재 역할의 활성 앵커·프리팹·기능을 구성합니다. 플레이어 행은 소품 생성에서 제외합니다.
6. 플레이어 생성 버튼은 기존 PlayerSpawnDriver를 재사용합니다. WASD·마우스 시점·Closeup을 확인할 수 있으며 정답 서버 판정은 이 도구의 범위가 아닙니다.
7. 이 방 클리어 → 다음 방 자동 생성을 누르면 CSV Next로 다음 방과 내부 오브젝트를 생성하고 출구·입구를 정렬합니다. 이전 방은 유지합니다.
8. 종료/상태 초기화 또는 창 닫기로 복제한 방·소품·플레이어를 정리하고 원본 방의 활성 상태를 복원합니다.

필수 앵커·프리팹이 없거나 입구·출구 연결이 실패하면 후보 방을 제거하고 클리어를 확정하지 않습니다. 실제 서버 상태·LocalRoomProgression·해금 상태를 우회하지 않습니다. 데이터 변경 후에는 미리보기를 종료하고 다시 시작합니다.

## 코드 책임

| 파일 | 책임 |
|---|---|
| Editor/DeveloperRoomProgression.cs | 정수 ID 집합으로 개발자 해금·클리어와 Changed 이벤트를 소유합니다. |
| Editor/RoomClearPreviewSession.cs | 복제본 생성·실제 게임 로더 호출·배치·원본 활성 상태 복원·수명을 소유합니다. |
| World/RoomSceneView.cs | 실제 게임과 미리보기가 공유하는 CSV 오브젝트 스폰·기능 구성·데이터 주입입니다. |
| World/RoomConnectionAlignment.cs | 출입구 기준으로 방 전체를 이동·회전합니다. |
| Player/PlayerSpawnDriver.cs | 실제 플레이어와 미리보기가 공유하는 카메라·이동·시점·상호작용 조립입니다. |
| Editor/RoomClearTestWindow.cs | 메뉴·버튼·상태 표시를 담당합니다. |
| Editor/RoomPreviewChecks.cs | 임시 큐브로 역할 선택·스폰·실패·재시도·정리를 검사합니다. |

자세한 온라인 대기 조건과 수동 검사는 [온라인 시작 대기와 CSV 방 미리보기](SessionStartAndPreview.md)를 참고합니다.

## 연결점 작성 규칙

현재 연결점은 사용자님이 제작 중입니다. 코드는 위치를 추측하거나 새 연결점을 임의로 만들지 않습니다. Play를 끈 상태에서 다음과 같이 작성하고 씬/프리팹을 저장한 뒤 다시 Play합니다.

| 방 | 입구: 이전 방과 맞닿는 곳 | 출구: 다음 방으로 나가는 곳 |
|---|---|---|
| R01 | 첫 방이라 불필요 | R01_DoorPoint |
| R02 | R02_EntryPoint | R02_DoorPoint |
| R03 | R03_EntryPoint | R03_DoorPoint |
| R04 | R04_EntryPoint | R04_DoorPoint |
| R05 | R05_EntryPoint | R05_DoorPoint |
| R06 | R06_EntryPoint | R06_DoorPoint |
| R07 | R07_EntryPoint | 마지막 방이라 불필요 |

출구인 R02_DoorPoint를 R02의 입구로 재사용하지 않습니다. 입구와 출구를 구분해야 R01 → R02 → R03을 순서대로 이어 붙일 수 있습니다.

1. 씬 첫 방의 RoomSceneView 아래 R01_DoorPoint를 확인합니다. 미리보기는 해당 방을 복제하여 그 연결점을 읽습니다.
2. Room_02.prefab의 내부에 빈 GameObject `R02_EntryPoint`와 `R02_DoorPoint`를 만듭니다. 하위 그룹 안에 있어도 되지만 같은 이름은 정확히 하나여야 합니다. R03 이후도 같은 규칙입니다. Room_01 프리팹을 직접 생성할 때는 그 프리팹에도 R01_DoorPoint가 필요합니다.
3. 연결점은 문턱의 바닥 중앙에 둡니다. 이동 도구를 Local로 바꿔 파란색 Z+ 축이 **각 방의 바깥쪽**, 초록색 Y+ 축이 위를 향하게 회전합니다. 입구 Z+는 이전 방 쪽, 출구 Z+는 다음 방 쪽입니다.
4. 문턱을 기준으로 잡아야 바닥 높이가 이어집니다. 방 루트·부모는 양수의 균등 스케일을 사용합니다. 도구는 문 폭이나 모델 크기를 강제로 바꾸지 않습니다.
5. R01 클리어 시 다음 방을 통째로 회전하여 두 Z+를 반대로 맞추고, 연결점의 월드 위치를 일치시킵니다. R01은 이동하지 않습니다. R02의 출구는 R03 생성 때 사용합니다.
6. 빈 Transform은 문 애니메이션과 충돌체를 열어 주지 않습니다. 모델에 실제 통로가 있어야 하며, 문 개방·충돌체 처리는 별도 기능입니다. 기존 DoorPoint를 CSV 문 스폰에도 쓰는 경우 기준 위치를 바꾸면 문 배치에도 영향이 있으므로 함께 확인합니다.

`Tools > Game > Verify Room Connections` 메뉴는 임시 오브젝트로 23개 배치 검사를 실행하고 즉시 정리합니다. 실제 모델 없이 연결 수학을 확인하는 용도이며, 실제 통로의 벽·문·충돌체·시야 검증은 R01/R02 연결점 제작 후 별도로 해야 합니다.

방 ID·진행 상태는 정수입니다. 이 모듈의 Vector3/Quaternion은 Unity 모델의 위치·회전 배치용으로만 사용하며 시간·서버 진행 계산에는 사용하지 않습니다.


## 현재 실제 콘텐츠 확인 사항

2026-10-21 Unity Play에서 로컬 CSV 기준 A/B 1번 방 내부 오브젝트·플레이어 생성과 B 2번 방 연결을 확인했습니다. A 2번 방은 ANCHOR_INTERACT ID 2034의 R2A_LOCK 앵커 누락으로 중단됩니다. 데이터·모델 준비 상태이며 자동으로 임의 앵커를 만들지 않습니다. 상세 검사 결과는 SessionStartAndPreview.md를 참고합니다.
