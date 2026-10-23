using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Data.CsvValidation.Runtime;
using EscapeGame.Data.Tables;
using EscapeGame.Session.Contracts;

namespace EscapeGame.World
{
    // 데이터 소스 선택과 등록을 조정합니다. 씬 전환은 Unity 조립 지점이 주입합니다.
    public sealed class GameEntryService : IRoomAnswerService, IRoomCompletionService, IRoomCompletionConnection, IDisposable
    {
        private readonly ISessionConnectionService session;
        private readonly ISessionContentService content;
        private readonly Func<CsvDataSet> readLocal;
        private readonly CsvValidator validator;
        private readonly Func<CancellationToken, Task> enterScene;
        private readonly CancellationTokenSource lifetime;
        private readonly CancellationToken ownerToken;
        private bool disposed;
        private bool waitingForParticipants;
        public bool IsWaitingForParticipants => waitingForParticipants;
        public SessionStartGate StartGate
        {
            get;
        }
        private LocalRoomProgression local;
        private readonly RoomInteractionService interactions;
        public RoomPermissionService RoomPermissions => interactions.Permissions;
        public bool IsAvailable => interactions.IsAvailable;
        public event Action AvailabilityChanged
        {
            add
            {
                interactions.AvailabilityChanged += value;
            }
            remove
            {
                interactions.AvailabilityChanged -= value;
            }
        }
        private IReadOnlyList<int> pendingRooms;
        public CsvGameData Data
        {
            get;
            private set;
        }
        public string Role
        {
            get;
            private set;
        }
        public bool IsLocal
        {
            get;
            private set;
        }
        public bool IsBusy
        {
            get;
            private set;
        }
        public string Message
        {
            get;
            private set;
        } = "탈출하기를 눌러 게임에 입장하세요.";
        public IReadOnlyList<int> UnlockedRoomIds
        {
            get;
            private set;
        } = Array.Empty<int>();
        public event Action Changed;
        public event Action<IReadOnlyList<int>> RoomsChanged;
        public event Action<int> RoomSolved
        {
            add
            {
                interactions.RoomSolved += value;
            }
            remove
            {
                interactions.RoomSolved -= value;
            }
        }
        public bool HasDoorPermission(int roomNo) => interactions.HasDoorPermission(roomNo);
        public GameEntryService(ISessionConnectionService session, ISessionContentService content,
            Func<CsvDataSet> readLocal, CsvValidator validator, Func<CancellationToken, Task> enterScene,
            CancellationToken ownerLifetime = default)
        {
            this.session = session;
            this.content = content;
            this.readLocal = readLocal;
            this.validator = validator;
            this.enterScene = enterScene;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(ownerLifetime);
            ownerToken = lifetime.Token;
            content.RoomsChanged += OnRoomsChanged;
            session.StateChanged += OnSessionStateChanged;
            interactions = new RoomInteractionService(RequestRoomAsync, ownerToken);
            StartGate = new SessionStartGate(session);
            StartGate.Changed += OnStartGateChanged;
        }
        public async Task EnterAsync(bool useLocal, string address, string testRole, CancellationToken token)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(GameEntryService));
            if (IsBusy)
                return;
            using var request = CancellationTokenSource.CreateLinkedTokenSource(token, ownerToken);
            token = request.Token;
            interactions.Reset();
            StartGate.SetLocalMode(useLocal);
            Data = null;
            local = null;
            pendingRooms = null;
            UnlockedRoomIds = Array.Empty<int>();
            IsBusy = true;
            Message = useLocal ? "로컬 CSV 검증 중" : "서버 접속 및 데이터 동기화 중";
            Changed?.Invoke();
            bool joinedHere = false;
            bool entered = false;
            try
            {
                token.ThrowIfCancellationRequested();
                if (useLocal && session.State.IsJoined)
                {
                    await session.LeaveAsync(token);
                    token.ThrowIfCancellationRequested();
                }
                if (!useLocal && !session.State.IsJoined)
                {
                    var join = await session.JoinAsync(address, token);
                    if (join.Outcome != SessionCommandOutcome.Completed)
                        throw new InvalidOperationException("입장 실패: " + join.Code);
                    joinedHere = true;
                    token.ThrowIfCancellationRequested();
                }
                var source = useLocal ? readLocal() : await content.DownloadDataAsync(token);
                var store = new CsvDataStore();
                if (!store.TryRegister(source, validator, out var report, token))
                    throw new InvalidOperationException(string.Join("\n", report.Issues.Where(issue => issue.Severity == EscapeGame.Data.CsvValidation.Contracts.CsvValidationSeverity.Error).Take(5)));
                var data = new CsvGameData(store);
                string role = useLocal ? testRole : session.State.Role;
                var plan = new RoomLoadPlan(data, role);
                token.ThrowIfCancellationRequested();
                Data = data;
                Role = role;
                IsLocal = useLocal;
                interactions.SetAvailable(true);
                local = useLocal ? new LocalRoomProgression(data) : null;
                UnlockedRoomIds = useLocal ? local.UnlockedRoomIds : pendingRooms ?? Array.AsReadOnly(new[] { plan.First.Id });
                if (!useLocal)
                {
                    // DownloadDataAsync는 같은 요청 수명에서 최신 snapshot까지 반영합니다.
                    // 이미 두 명이면 대기 화면을 알리지 않고 씬 로딩으로 진행합니다.
                    waitingForParticipants = !StartGate.CanEnterScene;
                    if (waitingForParticipants)
                    {
                        Message = StartGate.Message;
                        Changed?.Invoke();
                    }
                    try
                    {
                        await StartGate.WaitForParticipantsAsync(token);
                    }
                    finally
                    {
                        waitingForParticipants = false;
                    }
                }
                Message = "데이터 등록 완료. Ingame 로딩 중";
                Changed?.Invoke();
                // 커밋 경계: 로드 시작 전까지 화면 요청을 취소할 수 있습니다.
                // 씬 전환이 시작되면 지속 게임 수명으로 넘겨 Join 화면 제거가 입장을 취소하지 않게 합니다.
                token.ThrowIfCancellationRequested();
                ownerToken.ThrowIfCancellationRequested();
                await enterScene(ownerToken);
                ownerToken.ThrowIfCancellationRequested();
                entered = true;
            }
            catch (OperationCanceledException)
            {
                Message = "입장이 취소되었습니다.";
            }
            catch (Exception error)
            {
                Message = error.Message;
            }
            finally
            {
                IsBusy = false;
                if (!entered)
                {
                    interactions.Reset();
                    Data = null;
                    local = null;
                    UnlockedRoomIds = Array.Empty<int>();
                    if (joinedHere && session.State.IsJoined)
                        await session.LeaveAsync(CancellationToken.None);
                }
                Changed?.Invoke();
            }
        }
        public async Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int puzzleId, string[] answers, CancellationToken token)
        {
            if (Data == null)
                throw new InvalidOperationException("게임 데이터가 등록되지 않았습니다.");
            token.ThrowIfCancellationRequested();
            var submittedData = Data;
            PuzzleSubmissionResult result;
            if (IsLocal)
            {
                result = local.Submit(Role, puzzleId, answers);
                if (result.Correct)
                    OnRoomsChanged(local.UnlockedRoomIds);
            }
            else
            {
                result = await content.SubmitPuzzleAsync(puzzleId, answers, token);
            }
            if (!disposed && ReferenceEquals(submittedData, Data) && result.Correct)
            {
                var room = new RoomLoadPlan(Data, Role).Rooms.FirstOrDefault(row => row.PuzzleId == puzzleId);
                if (room != null)
                    interactions.PublishSolved(room.RoomNo);
            }
            return result;
        }
        private void OnRoomsChanged(IReadOnlyList<int> ids)
        {
            if (pendingRooms != null && pendingRooms.SequenceEqual(ids))
                return;
            pendingRooms = Array.AsReadOnly(ids.ToArray());
            if (Data == null)
                return;
            UnlockedRoomIds = pendingRooms;
            RoomsChanged?.Invoke(UnlockedRoomIds);
        }
        public Task<bool> SubmitRoomAnswerAsync(int roomNo, string[] answers, CancellationToken token)
            => interactions.SubmitRoomAnswerAsync(roomNo, answers, token);
        public Task<bool> CheckRoomDoorAsync(int roomNo, CancellationToken token)
            => interactions.CheckRoomDoorAsync(roomNo, token);
        private async Task<RoomDecision> RequestRoomAsync(int roomNo, string[] answers, CancellationToken token)
        {
            if (Data == null)
                throw new InvalidOperationException("게임 데이터가 등록되지 않았습니다.");
            if (IsLocal)
            {
                var decision = local.DecideRoom(Role, roomNo, answers);
                OnRoomsChanged(local.UnlockedRoomIds);
                return decision;
            }
            var rooms = content as ISessionRoomService ?? session as ISessionRoomService
                ?? throw new InvalidOperationException("방 상호작용 계약이 연결되지 않았습니다.");
            return answers == null ? await rooms.CheckRoomDoorAsync(roomNo, token) : await rooms.SubmitRoomAnswerAsync(roomNo, answers, token);
        }
        Task<RoomAnswerResult> IRoomAnswerService.SubmitAnswerAsync(int roomNo, string[] answers, CancellationToken token)
            => ((IRoomAnswerService)interactions).SubmitAnswerAsync(roomNo, answers, token);
        private void OnStartGateChanged()
        {
            if (!waitingForParticipants)
                return;
            Message = StartGate.Message;
            Changed?.Invoke();
        }
        private void OnSessionStateChanged(SessionInfo state)
        {
            if (!IsLocal && !state.IsJoined)
                interactions.Reset();
        }
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            lifetime.Cancel();
            content.RoomsChanged -= OnRoomsChanged;
            session.StateChanged -= OnSessionStateChanged;
            interactions.Reset();
            StartGate.Changed -= OnStartGateChanged;
            StartGate.Dispose();
            lifetime.Dispose();
            interactions.Dispose();
        }
    }
}
