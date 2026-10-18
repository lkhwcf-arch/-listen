using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data.Tables;

namespace EscapeGame.World
{
    // 서버 없는 테스트용 진행 상태입니다. 온라인 판정에는 사용하지 않습니다.
    public sealed class LocalRoomProgression
    {
        private readonly CsvGameData data;
        private readonly HashSet<int> solved = new HashSet<int>();
        private readonly HashSet<int> unlocked = new HashSet<int>();
        public IReadOnlyList<int> UnlockedRoomIds => Array.AsReadOnly(unlocked.OrderBy(id => id).ToArray());
        public LocalRoomProgression(CsvGameData data)
        {
            this.data = data;
            unlocked.Add(new RoomLoadPlan(data, "A").First.Id);
            unlocked.Add(new RoomLoadPlan(data, "B").First.Id);
        }
        public bool Submit(string role, int puzzleId, string[] answers)
        {
            var plan = new RoomLoadPlan(data, role);
            var puzzle = data.Puzzles.FirstOrDefault(row => row.Id == puzzleId && row.Active);
            if (puzzle == null || plan.Find(puzzle.SolverRoom) == null || !unlocked.Contains(puzzle.SolverRoom)) return false;
            var slots = data.PuzzleSlots.Where(slot => slot.PuzzleId == puzzleId).OrderBy(slot => slot.Slot).ToArray();
            if (answers == null || answers.Length != slots.Length || slots.Where((slot, index) =>
                !string.Equals(slot.Answer, answers[index], StringComparison.OrdinalIgnoreCase)).Any()) return false;
            solved.Add(puzzleId);
            var rooms = data.RoomsA.Cast<RoomData>().Concat(data.RoomsB).Where(room => room.Active).ToArray();
            foreach (var room in rooms.Where(room => unlocked.Contains(room.Id)))
            {
                var required = room.ExitRule == "BothSolved" ? rooms.Where(other => other.RoomNo == room.RoomNo) : new[] { room };
                if (room.Next != 0 && required.All(other => solved.Contains(other.PuzzleId))) unlocked.Add(room.Next);
            }
            return true;
        }
    }
}
