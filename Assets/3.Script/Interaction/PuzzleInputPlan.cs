using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EscapeGame.Data.Tables;
using EscapeGame.World;

namespace EscapeGame.Interaction
{
    public enum PuzzleInputKind
    {
        Dial, Sequence
    }

    // 정답은 보관하지 않습니다. 입력 선택지·슬롯 표시와 제출 대상만 불변 정의로 전달합니다.
    public sealed class PuzzleInputPlan
    {
        public int AnchorId
        {
            get;
        }
        public int RoomNo
        {
            get;
        }
        public int PuzzleId
        {
            get;
        }
        public int SlotCount => SlotLabels.Count;
        public PuzzleInputKind Kind
        {
            get;
        }
        public IReadOnlyList<string> Choices
        {
            get;
        }
        public IReadOnlyList<string> SlotLabels
        {
            get;
        }
        private PuzzleInputPlan(AnchorData anchor, PuzzleData puzzle, PuzzleInputKind kind, string[] choices, string[] labels)
        {
            AnchorId = anchor.Id;
            RoomNo = anchor.RoomNo;
            PuzzleId = puzzle.Id;
            Kind = kind;
            Choices = Array.AsReadOnly(choices);
            SlotLabels = Array.AsReadOnly(labels);
        }
        public static PuzzleInputPlan Create(CsvGameData data, string role, AnchorData anchor, PrefabFeaturePlan features)
        {
            if (data == null || anchor == null || features == null)
                throw new ArgumentNullException("입력 계획에 데이터·앵커·기능 선언이 필요합니다.");
            bool dial = features.Has(FeatureScripts.Dial), console = features.Has(FeatureScripts.Console);
            if (!dial && !console)
                return null;
            if (dial && console)
                throw new InvalidOperationException("한 대상에 DialLock과 ButtonConsole을 함께 선언할 수 없습니다.");
            if (!anchor.Active || !(anchor is AnchorInteractData) || features.PrefabId != anchor.PrefabId)
                throw new InvalidOperationException("활성 입력 앵커와 프리팹 기능 선언이 일치해야 합니다.");
            if (anchor.PlayerType != "ALL" && anchor.PlayerType != role)
                throw new InvalidOperationException("현재 플레이어의 입력 앵커가 아닙니다.");
            var puzzles = data.Puzzles.Where(row => row.Active && row.InputAnchor == anchor.Id).ToArray();
            if (puzzles.Length != 1)
                throw new InvalidOperationException("입력 앵커에 활성 PUZZLE이 정확히 하나 필요합니다: " + anchor.Id);
            var puzzle = puzzles[0];
            var room = new RoomLoadPlan(data, role).Find(puzzle.SolverRoom);
            if (room == null || room.RoomNo != anchor.RoomNo || room.PuzzleId != puzzle.Id || room.Role != "Solver")
                throw new InvalidOperationException("현재 플레이어의 정답 입력 방과 PUZZLE.SolverRoom이 일치하지 않습니다.");
            var slots = data.PuzzleSlots.Where(row => row.PuzzleId == puzzle.Id).OrderBy(row => row.Slot).ToArray();
            if (puzzle.SlotCount < 1 || slots.Length != puzzle.SlotCount || !slots.Select(row => row.Slot).SequenceEqual(Enumerable.Range(1, slots.Length)))
                throw new InvalidOperationException("PUZZLE_SLOT의 개수·순서가 PUZZLE.SlotCount와 일치해야 합니다.");
            string[] choices;
            if (dial)
            {
                var definitions = data.Locks.Where(row => row.AnchorRef == anchor.Id).ToArray();
                if (definitions.Length != 1)
                    throw new InvalidOperationException("DialLock 대상에 LOCK 행이 정확히 하나 필요합니다.");
                var definition = definitions[0];
                choices = definition.DialFace switch
                {
                    "Number1_9" => Enumerable.Range(1, 9).Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    "Number0_9" => Enumerable.Range(0, 10).Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray(),
                    "Letter9" => definition.DialLetters.Count == 9 ? definition.DialLetters.ToArray() : throw new InvalidOperationException("Letter9는 DialLetters 9개가 필요합니다."),
                    "Color6" => data.ColorLetters.OrderBy(row => row.Id).Select(row => row.Color).Distinct(StringComparer.Ordinal).ToArray(),
                    _ => throw new InvalidOperationException("지원하지 않는 LOCK.DialFace입니다: " + definition.DialFace)
                };
                if (definition.DialFace == "Color6" && choices.Length != 6)
                    throw new InvalidOperationException("Color6는 COLOR_LETTER에 서로 다른 색 6개가 필요합니다.");
            }
            else
            {
                var definitions = data.Consoles.Where(row => row.AnchorRef == anchor.Id).ToArray();
                if (definitions.Length != 1 || definitions[0].SlotCount != puzzle.SlotCount)
                    throw new InvalidOperationException("ButtonConsole의 CONSOLE 행과 퍼즐 슬롯 수가 일치해야 합니다.");
                choices = definitions[0].Buttons.ToArray();
            }
            if (choices.Length == 0 || choices.Any(string.IsNullOrWhiteSpace) || choices.Distinct(StringComparer.Ordinal).Count() != choices.Length)
                throw new InvalidOperationException("입력 선택지는 비어 있지 않은 고유 값이어야 합니다.");
            return new PuzzleInputPlan(anchor, puzzle, dial ? PuzzleInputKind.Dial : PuzzleInputKind.Sequence, choices, slots.Select(row => row.Display).ToArray());
        }
    }
}
