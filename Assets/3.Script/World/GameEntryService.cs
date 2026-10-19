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
    public sealed class GameEntryService : IDisposable
    {
        private readonly ISessionConnectionService session;
        private readonly ISessionContentService content;
        private readonly Func<CsvDataSet> readLocal;
        private readonly CsvValidator validator;
        private readonly Func<CancellationToken, Task> enterScene;
        private LocalRoomProgression local;
        private IReadOnlyList<int> pendingRooms;
        public CsvGameData Data { get; private set; }
        public string Role { get; private set; }
        public bool IsLocal { get; private set; }
        public bool IsBusy { get; private set; }
        public string Message { get; private set; } = "접속 설정을 확인하고 있습니다.";
        public IReadOnlyList<int> UnlockedRoomIds { get; private set; } = Array.Empty<int>();
        public event Action Changed;
        public event Action<IReadOnlyList<int>> RoomsChanged;
        public GameEntryService(ISessionConnectionService session, ISessionContentService content,
            Func<CsvDataSet> readLocal, CsvValidator validator, Func<CancellationToken, Task> enterScene)
        {
            this.session = session; this.content = content; this.readLocal = readLocal;
            this.validator = validator; this.enterScene = enterScene;
            content.RoomsChanged += OnRoomsChanged;
        }
        public async Task EnterAsync(bool useLocal, string address, string testRole, CancellationToken token)
        {
            if (IsBusy) return;
            Data = null; local = null; pendingRooms = null;
            UnlockedRoomIds = Array.Empty<int>();
            IsBusy = true; Message = useLocal ? "로컬 CSV 검증 중" : "서버 접속 및 데이터 동기화 중"; Changed?.Invoke();
            bool joinedHere = false;
            bool entered = false;
            try
            {
                if (useLocal && session.State.IsJoined)
                {
                    await session.LeaveAsync(token);
                    token.ThrowIfCancellationRequested();
                }
                if (!useLocal && !session.State.IsJoined)
                {
                    var join = await session.JoinAsync(address, token);
                    token.ThrowIfCancellationRequested();
                    if (join.Outcome != SessionCommandOutcome.Completed) throw new InvalidOperationException("입장 실패: " + join.Code);
                    joinedHere = true;
                }
                var source = useLocal ? readLocal() : await content.DownloadDataAsync(token);
                var store = new CsvDataStore();
                if (!store.TryRegister(source, validator, out var report, token))
                    throw new InvalidOperationException(string.Join("\n", report.Issues.Where(issue => issue.Severity == EscapeGame.Data.CsvValidation.Contracts.CsvValidationSeverity.Error).Take(5)));
                var data = new CsvGameData(store);
                string role = useLocal ? testRole : session.State.Role;
                var plan = new RoomLoadPlan(data, role);
                token.ThrowIfCancellationRequested();
                Data = data; Role = role; IsLocal = useLocal;
                local = useLocal ? new LocalRoomProgression(data) : null;
                UnlockedRoomIds = useLocal ? local.UnlockedRoomIds : pendingRooms ?? Array.AsReadOnly(new[] { plan.First.Id });
                Message = "데이터 등록 완료. Ingame 로딩 중"; Changed?.Invoke();
                await enterScene(token);
                entered = true;
            }
            catch (OperationCanceledException) { Message = "입장이 취소되었습니다."; }
            catch (Exception error) { Message = error.Message; }
            finally
            {
                IsBusy = false;
                if (!entered)
                {
                    Data = null; local = null; UnlockedRoomIds = Array.Empty<int>();
                    if (joinedHere && session.State.IsJoined) await session.LeaveAsync(CancellationToken.None);
                }
                Changed?.Invoke();
            }
        }
        public async Task<PuzzleSubmissionResult> SubmitPuzzleAsync(int puzzleId, string[] answers, CancellationToken token)
        {
            if (Data == null) throw new InvalidOperationException("게임 데이터가 등록되지 않았습니다.");
            token.ThrowIfCancellationRequested();
            if (IsLocal)
            {
                var result = local.Submit(Role, puzzleId, answers);
                if (result.Correct) OnRoomsChanged(local.UnlockedRoomIds);
                return result;
            }
            else
            {
                return await content.SubmitPuzzleAsync(puzzleId, answers, token);
            }
        }
        private void OnRoomsChanged(IReadOnlyList<int> ids)
        {
            pendingRooms = Array.AsReadOnly(ids.ToArray());
            if (Data == null) return;
            UnlockedRoomIds = pendingRooms;
            RoomsChanged?.Invoke(UnlockedRoomIds);
        }
        public void Dispose() => content.RoomsChanged -= OnRoomsChanged;
    }
}
