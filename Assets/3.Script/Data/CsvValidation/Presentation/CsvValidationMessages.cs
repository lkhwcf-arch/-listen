using System.Linq;
using EscapeGame.Data.CsvValidation.Contracts;

namespace EscapeGame.Data.CsvValidation.Presentation
{
    public static class CsvValidationMessages
    {
        public static string Format(CsvValidationIssue issue) => $"[{issue.Severity}] {issue.Sheet}.csv:{issue.Line} [{issue.Column}] {Describe(issue)}";
        public static string Describe(CsvValidationIssue issue)
        {
            string detail = string.Join(", ", issue.Details.Where(x => x.Length > 0));
            string message = issue.Code switch
            {
                CsvValidationCode.ReadFailed => "UTF-8 파일 읽기에 실패했습니다.",
                CsvValidationCode.ParseError => "CSV 문법 오류입니다.",
                CsvValidationCode.UnknownSchema => "검사 스키마가 없습니다. 문법과 일반 ID 중복만 검사합니다.",
                CsvValidationCode.MissingColumn => "필수 컬럼이 없습니다.",
                CsvValidationCode.UnknownColumn => "정의되지 않은 컬럼입니다.",
                CsvValidationCode.EmptyTable => "데이터 행이 없습니다.",
                CsvValidationCode.EmptyValue => "필수 값이 비어 있습니다.",
                CsvValidationCode.Whitespace => "값 앞뒤에 공백이 있습니다.",
                CsvValidationCode.InvalidValue => "자료형·범위 또는 허용 값이 맞지 않습니다.",
                CsvValidationCode.DuplicateKey => "키 또는 값의 조합이 중복됩니다.",
                CsvValidationCode.MissingRegistry => "REGISTRY를 읽을 수 없습니다.",
                CsvValidationCode.MissingTable => "등록된 테이블이 없거나 읽을 수 없습니다.",
                CsvValidationCode.UnknownSheet => "등록되지 않았거나 지원하지 않는 대상 시트입니다.",
                CsvValidationCode.MissingReference => "참조 대상을 찾을 수 없습니다.",
                CsvValidationCode.InactiveReference => "활성 행이 비활성 대상을 참조합니다.",
                CsvValidationCode.UnregisteredTable => "REGISTRY에 등록되지 않은 CSV입니다.",
                CsvValidationCode.RoomMissing => "해당 플레이어의 방에 RoomNo가 없습니다.",
                CsvValidationCode.RoomCycle => "다음 방 연결에 자기 참조 또는 순환이 있습니다.",
                CsvValidationCode.SlotCount => "퍼즐 슬롯 수와 실제 슬롯 개수가 다릅니다. 선언/실제:",
                CsvValidationCode.SlotRange => "슬롯 번호가 1부터 선언된 슬롯 수까지의 범위를 벗어났습니다.",
                CsvValidationCode.PuzzleRoom => "방의 PuzzleID 또는 역할이 퍼즐과 맞지 않습니다.",
                CsvValidationCode.InputRoom => "입력 앵커의 방·플레이어가 풀이 방과 맞지 않습니다.",
                CsvValidationCode.ConsoleSlots => "콘솔과 퍼즐의 슬롯 수가 다릅니다.",
                CsvValidationCode.InvalidDistance => "거리 값을 함께 지정하고 MinDist ≤ MaxDist로 설정하세요.",
                CsvValidationCode.InvalidLetters => "Letter9에는 서로 다른 9개 문자가 필요합니다.",
                CsvValidationCode.MissingResource => "Assets 또는 Resources 경로에서 리소스를 찾지 못했습니다.",
                _ => issue.Code.ToString()
            };
            return detail.Length == 0 ? message : message + " " + detail;
        }
    }
}
