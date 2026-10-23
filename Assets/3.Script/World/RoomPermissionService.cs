using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // One permission cache per game-entry lifetime, shared by every door. Open/closed presentation is separate.
    public sealed class RoomPermissionService
    {
        private readonly Func<int, string[], CancellationToken, Task<RoomDecision>> request;
        private readonly HashSet<int> permitted = new HashSet<int>();
        private readonly HashSet<int> pending = new HashSet<int>();
        private int generation;
        public RoomPermissionService(Func<int, string[], CancellationToken, Task<RoomDecision>> request)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
        }
        public bool HasPermission(int roomNo) => permitted.Contains(roomNo);
        public void Reset()
        {
            generation++;
            permitted.Clear();
            pending.Clear();
        }
        public Task<bool> CheckDoorAsync(int roomNo, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ValidateRoom(roomNo);
            return permitted.Contains(roomNo) ? Task.FromResult(true) : ExecuteAsync(roomNo, null, token);
        }
        public Task<bool> SubmitAnswerAsync(int roomNo, string[] answers, CancellationToken token)
        {
            if (answers == null)
                throw new ArgumentNullException(nameof(answers));
            return ExecuteAsync(roomNo, answers, token);
        }
        private async Task<bool> ExecuteAsync(int roomNo, string[] answers, CancellationToken token)
        {
            ValidateRoom(roomNo);
            token.ThrowIfCancellationRequested();
            if (!pending.Add(roomNo))
                throw new SessionResultException(SessionFailureCode.Busy);
            int version = generation;
            try
            {
                var decision = await request(roomNo, answers, token);
                token.ThrowIfCancellationRequested();
                if (version != generation)
                    throw new OperationCanceledException("이전 입장의 문 판정입니다.");
                if (decision == null || decision.RoomNo != roomNo || (answers == null && decision.Value != decision.DoorAllowed))
                    throw new SessionResultException(SessionFailureCode.InvalidResponse);
                if (decision.DoorAllowed)
                    permitted.Add(roomNo);
                return decision.Value;
            }
            finally
            {
                if (version == generation)
                    pending.Remove(roomNo);
            }
        }
        private static void ValidateRoom(int roomNo)
        {
            if (roomNo < 1)
                throw new ArgumentOutOfRangeException(nameof(roomNo));
        }
    }
}
