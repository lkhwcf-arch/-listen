# CSV 읽기·메모리 검증·데이터 클래스 주입

기준일: 2026-10-19. 전체 씬·서버 연동 흐름은 [프로젝트 README](../README.md)를 참고합니다.

## 입력과 문법

`Common/Csv`의 파서는 Unity와 파일 시스템에 의존하지 않습니다. `ICsvTextSource`가 문자열 입력을 제공하고 `CsvLoader`가 소스와 파서를 연결합니다. `Utf8CsvFileSource`와 `UnityTextAssetCsvSource`는 엄격한 UTF-8 디코딩을 수행합니다.

```csharp
using EscapeGame.Data.Csv;
using EscapeGame.Data.Infrastructure;

var loader = new CsvLoader(new UnityTextAssetCsvSource(csvAsset), new CsvParser());
CsvDocument document = await loader.LoadAsync(csvAsset.name, cancellationToken);
string cell = document.Rows[0].Get(columnName);
```

- 첫 번째 비어 있지 않은 레코드가 헤더입니다. 헤더 앞뒤 공백은 정리하고 대소문자를 구분합니다.
- UTF-8 BOM, 인용부호 안 쉼표·줄바꿈, 두 따옴표로 표현한 따옴표를 지원합니다.
- 공백 줄은 무시하고 명시적인 빈 셀·첫 칸이 빈 행은 보존합니다.
- 빈/중복 헤더, 칸 수 불일치, 잘못된 따옴표는 위치를 포함한 해석 오류로 반환합니다.
- 문자열 공백·앞자리 0을 유지합니다. 원본 셀은 문자열이며 컬럼의 타입 변환은 데이터 모델의 책임입니다.

## 한 번 읽은 원본의 재사용

```text
파일 / TextAsset / 서버 CSV 문자열
  → CsvParser → CsvDocument / CsvRow
  → CsvDataSet → CsvValidator → CsvDataStore
  → CsvTableMapping<T> → CsvGameData → 소비자
```

`CsvDataSet`은 원본과 읽기·해석 오류를 보존합니다. `CsvValidator`는 메모리 스냅샷만 검사합니다. `CsvDataStore.InitializeAsync`는 같은 저장소의 최초 성공 등록을 재사용하며, `TryRegister`는 오류가 있는 새 원본으로 정상 원본을 교체하지 않습니다.

정상 새 원본을 등록하면 타입별 캐시를 비우고 Registered를 알립니다. 같은 원본을 다시 등록하면 변환을 반복하지 않습니다. `CsvGameData` 생성은 이미 변환한 테이블을 재사용합니다. 기존 소비자가 보유한 이전 묶음은 유지되므로 데이터 교체 시 새 묶음을 명시적으로 주입합니다.

## 타입과 검증 규칙

각 CSV 파일은 `Data/Tables`의 행 클래스에 대응합니다. 공통 `RoomData`, `AnchorData`, `CsvRowReader`를 재사용하며 `Table` 매핑에 시트 이름과 변환 함수를 한 번 정의합니다.

ID·순서·시간은 정수, Y/N은 bool, 비어 있는 선택 숫자는 nullable입니다. 정답·표시값은 문자열입니다. Unity 표현·물리·오디오에 필요한 소수는 float이고, GAME_CONFIG는 선언한 Type의 접근자만 허용합니다. 값 목록은 읽기 전용으로 제공하며 원본에 없는 게임 중 변경 상태를 행 모델에 섞지 않습니다.

스키마는 [CsvValidationRules.xml](../Assets/3.Script/Data/CsvValidation/Configuration/CsvValidationRules.xml), 조립은 [CsvValidator.cs](../Assets/3.Script/Data/CsvValidation/Runtime/CsvValidator.cs)입니다. 룸·앵커 공통 profile과 다음 규칙을 조합합니다.

| 규칙 | 검사 |
|---|---|
| Schema | 필수/미등록 컬럼·빈 값·자료형·범위·키 중복 |
| Reference | REGISTRY·테이블/ID 참조·활성 대상 관계 |
| Room | A/B 방·ALL 앵커·Next 순환·시작점 1개·전체 활성 방 연결 |
| Puzzle | Solver/Clue·입력 앵커·슬롯 수/범위·콘솔 관계 |
| ValueRelation | 카드·순서·포트·설정·사운드 관계 |
| Resource | 선택적인 Unity 프리팹·AudioClip 경로 확인 |

파일 이름별 검사 로직을 반복 등록하지 않습니다. 스키마에서 시트·태그·참조를 정의하고 공통 규칙이 해석합니다. 새 시트·새 게임 관계는 대응 스키마·매핑·필요한 추가 규칙을 함께 준비합니다. 서버 CsvSchema.ts와 규칙도 맞춰 갱신합니다.

## Editor 검사 창

**Tools → CSV → 검사기**에서 처음 검사하면 `Assets/11.CSV`를 읽습니다. **메모리 데이터 검사**는 같은 원본을 재검사하며, 파일을 수정했다면 **CSV 다시 읽고 검사**를 선택합니다. 오류·경고·파일·행·컬럼·원인을 검색하고 결과를 복사할 수 있습니다.

선택적인 리소스 검사는 `Assets/...` 또는 Resources 상대 경로를 확인하고 미발견을 경고합니다. CSV 검사 자체는 파일을 수정하지 않습니다. 앵커 이름과 실제 씬 연결은 별도로 `RoomSceneView`가 방 로드 전에 확인합니다.

## 런타임 연결과 검사

Join의 서버 입장은 서버 CSV를, 클라이언트 테스트는 빌드에 포함된 CSV를 선택합니다. 두 경로 모두 검증·타입 변환을 통과해야 Ingame에 진입합니다. GameContentBuilder는 로컬 CSV·스키마·프리팹 참조를 빌드용 에셋에 모으고, GameFlowBootstrapper는 데이터 묶음의 씬 간 수명을 유지합니다.

```powershell
Set-Location Tools/CsvValidation.Tests
dotnet run -- ../..
```

.NET 8 SDK용 검사 프로젝트는 CSV 규칙·등록/취소/캐시·23개 타입·2,861개 셀·입장/방 선택과 콘텐츠 회귀 검사를 포함합니다. 기존 96개에 27개를 추가해 총 123개이며, 이번에는 설치된 .NET 10 SDK의 임시 프로젝트에서 동일 소스를 실행했습니다. ExitRule은 SolvedEach/BothSolved만 허용하고 활성 방은 시작점 하나에서 모두 연결되어야 합니다. Unity Play와 실제 HTTP 검사는 루트 README의 `Tools/WorldPlayVerification` 안내를 따릅니다.
