using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data.Tables;
using EscapeGame.Data;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // 서버 없는 테스트용 진행 상태입니다. 온라인 판정에는 사용하지 않습니다.
    public sealed class LocalRoomProgression
    {
        private readonly CsvGameData data;
        private readonly HashSet<int> solved = new HashSet<int>();
        private readonly HashSet<int> unlocked = new HashSet<int>();
        private readonly HashSet<int> permitted = new HashSet<int>();
        public IReadOnlyList<int> UnlockedRoomIds => Array.AsReadOnly(unlocked.OrderBy(id => id).ToArray());
        public LocalRoomProgression(CsvGameData data)
        {
            this.data = data;
            unlocked.Add(new RoomLoadPlan(data, "A").First.Id);
            unlocked.Add(new RoomLoadPlan(data, "B").First.Id);
        }
        public PuzzleSubmissionResult Submit(string role, int puzzleId, string[] answers)
        {
            var plan = new RoomLoadPlan(data, role);
            var puzzle = data.Puzzles.FirstOrDefault(row => row.Id == puzzleId && row.Active);
            if (puzzle == null || plan.Find(puzzle.SolverRoom) == null || !unlocked.Contains(puzzle.SolverRoom))
                return PuzzleSubmissionResult.FromDecision(false, false);
            var slots = data.PuzzleSlots.Where(slot => slot.PuzzleId == puzzleId).OrderBy(slot => slot.Slot).ToArray();
            if (answers == null || answers.Length != slots.Length || slots.Where((slot, index) =>
                !string.Equals(slot.Answer, answers[index], StringComparison.OrdinalIgnoreCase)).Any())
                return PuzzleSubmissionResult.FromDecision(true, false);
            solved.Add(puzzleId);
            CheckDoor(role, plan.Find(puzzle.SolverRoom).RoomNo);
            return PuzzleSubmissionResult.FromDecision(true, true);
        }
        public RoomDecision DecideRoom(string role, int roomNo, string[] answers)
        {
            var room = new RoomLoadPlan(data, role).Rooms.FirstOrDefault(row => row.RoomNo == roomNo);
            if (room == null) throw new EscapeGame.Session.SessionResultException(SessionFailureCode.RoomNotFound);
            bool value;
            if (answers == null) value = CheckDoor(role, roomNo);
            else
            {
                var result = Submit(role, room.PuzzleId, answers);
                if (!result.Accepted) throw new EscapeGame.Session.SessionResultException(SessionFailureCode.SubmissionNotAllowed);
                value = result.Correct;
            }
            return new RoomDecision(roomNo, value, permitted.Contains(room.Id));
        }
        public bool CheckDoor(string role, int roomNo)
        {
            var room = new RoomLoadPlan(data, role).Rooms.FirstOrDefault(row => row.RoomNo == roomNo);
            if (room == null || !unlocked.Contains(room.Id)) return false;
            if (permitted.Contains(room.Id)) return true;
            var rooms = data.RoomsA.Cast<RoomData>().Concat(data.RoomsB).Where(row => row.Active);
            var required = RoomExitRules.RequiresBothPuzzles(room.ExitRule) ? rooms.Where(other => other.RoomNo == room.RoomNo) : new[] { room };
            if (!required.All(other => solved.Contains(other.PuzzleId))) return false;
            permitted.Add(room.Id);
            if (room.Next != 0) unlocked.Add(room.Next);
            return true;
        }
    }
}
