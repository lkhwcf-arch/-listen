# CSV 읽기와 로딩

이번 단계는 실제 컬럼을 정하지 않고 CSV에서 헤더와 문자열 행을 읽는 기능입니다.
핵심 타입은 EscapeGame.Data.Csv에 있고 Unity/파일 어댑터는 EscapeGame.Data.Infrastructure에 있습니다.

## Unity에서 확인

1. UTF-8, 쉼표 구분 CSV를 Assets 안에 넣습니다.
2. 빈 GameObject에 CsvLoadTester를 붙입니다.
3. Csv Asset에 CSV 파일을 연결합니다.
4. 컴포넌트 메뉴에서 Load CSV를 실행합니다. Edit/Play 모드 모두 사용할 수 있습니다.
5. Console에서 소스 이름, 헤더 수/목록, 데이터 행 수를 확인합니다.

CsvLoadTester는 자동 실행하지 않습니다. 기존 SampleScene도 변경하지 않았습니다.
아직 실제 데이터가 없으면 게임용 테이블을 새로 만들 필요가 없습니다.

## 코드에서 사용

```csharp
using EscapeGame.Data.Csv;
using EscapeGame.Data.Infrastructure;

var loader = new CsvLoader(new UnityTextAssetCsvSource(csvAsset), new CsvParser());
var document = await loader.LoadAsync(csvAsset.name, cancellationToken);
// document.Headers, document.Rows
// 실제 헤더가 정해지면 row.Get(columnName), row.TryGet(columnName, out value)
```

CsvLoader는 입력 어댑터와 CsvParser를 주입받습니다. CsvParser는 문자열만 해석하며 Unity를 참조하지 않습니다.
Utf8CsvFileSource는 외부 데스크톱 파일을 읽을 때 사용할 수 있습니다.
TextAsset 어댑터는 Unity 메인 스레드에서 접근하고 원본 bytes를 엄격한 UTF-8로 해석합니다.

## 규칙

- 첫 번째 비어 있지 않은 레코드가 헤더입니다. 헤더의 앞뒤 공백/탭을 정리하고 대소문자를 구분합니다.
- BOM, 빈 값, 따옴표 안의 쉼표/줄바꿈, 두 따옴표로 표현한 따옴표를 지원합니다.
- 공백 줄은 무시하고 명시적인 빈 셀과 첫 칸이 빈 행은 보존합니다.
- 중복/빈 헤더, 칸 수 불일치, 잘못된 따옴표는 CsvParseException으로 위치와 함께 알립니다.
- 문자열 셀의 공백과 앞자리 0은 유지합니다. 숫자·ID·목록 변환은 실제 컬럼 정의 후 추가합니다.
- 결과는 읽기 전용입니다. 플레이 중 변경 상태는 별도로 관리합니다.
- 서버 시작이나 세션 준비 보고에 자동으로 연결하는 기능은 이번 범위에 포함되지 않습니다.

실제 데이터 모델, 필수 컬럼·ID 검증, 전역 테이블 저장소, 핫 리로드는 아직 구성하지 않았습니다.
전체 코드 해설과 서버 사용법은 서버 프로젝트 docs/csv-reader.md에서 확인할 수 있습니다.
