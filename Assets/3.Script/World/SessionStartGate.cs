using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // 서버 공개 상태를 씬 진입과 로컬 조작 허용으로 변환합니다. Unity와 HTTP를 참조하지 않습니다.
    public sealed class SessionStartGate : IDisposable
    {
        private const int RequiredParticipants = 2;
        private readonly ISessionConnectionService session;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private SessionInfo state;
        private bool disposed;
        public bool IsLocal
        {
            get;
            private set;
        }
        public bool CanEnterScene
        {
            get;
            private set;
        }
        public bool CanPlay
        {
            get;
            private set;
        }
        public int ParticipantCount => state?.ParticipantCount ?? 0;
        public string Message
        {
            get;
            private set;
        }
        public event Action Changed;

        public SessionStartGate(ISessionConnectionService session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            Refresh(session.State);
            session.StateChanged += Refresh;
        }
        public void SetLocalMode(bool local)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SessionStartGate));
            IsLocal = local;
            Refresh(session.State);
        }
        private void Refresh(SessionInfo current)
        {
            bool enter = IsLocal || (current.IsJoined && current.ParticipantCount >= RequiredParticipants &&
                (current.Phase == ClientSessionPhase.Loading || current.Phase == ClientSessionPhase.Playing));
            bool play = IsLocal || (current.IsJoined && current.ParticipantCount >= RequiredParticipants && current.Phase == ClientSessionPhase.Playing);
            string message = play ? "" : !current.IsJoined ? "서버 연결이 종료되었습니다. 다시 입장해 주세요." :
                !enter ? $"상대방 입장을 기다리고 있습니다. ({current.ParticipantCount}/{RequiredParticipants})" :
                "두 참가자의 씬 준비 완료를 기다리고 있습니다.";
            bool changed = state == null || state.SessionId != current.SessionId || state.IsJoined != current.IsJoined ||
                CanEnterScene != enter || CanPlay != play || Message != message;
            state = current;
            CanEnterScene = enter;
            CanPlay = play;
            Message = message;
            if (changed)
                Changed?.Invoke();
        }
        public async Task WaitForParticipantsAsync(CancellationToken token)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SessionStartGate));
            token.ThrowIfCancellationRequested();
            Refresh(session.State);
            if (IsLocal)
                return;
            string sessionId = state.SessionId;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action check = () =>
            {
                if (!state.IsJoined || state.SessionId != sessionId)
                    completion.TrySetException(new InvalidOperationException("상대방 대기 중 서버 연결이 종료되거나 세션이 변경되었습니다."));
                else if (CanEnterScene)
                    completion.TrySetResult(true);
            };
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            using var cancellation = request.Token.Register(() => completion.TrySetCanceled());
            Changed += check;
            try
            {
                check();
                await completion.Task;
                request.Token.ThrowIfCancellationRequested();
                // 완료 직후 상대가 나간 경우 오래된 허용 결과로 씬을 전환하지 않습니다.
                if (!CanEnterScene || !state.IsJoined || state.SessionId != sessionId)
                    throw new InvalidOperationException("참가자 구성이 변경되었습니다. 다시 입장해 주세요.");
            }
            finally
            {
                Changed -= check;
            }
        }
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            session.StateChanged -= Refresh;
            lifetime.Cancel();
            lifetime.Dispose();
            Changed = null;
        }
    }
}
