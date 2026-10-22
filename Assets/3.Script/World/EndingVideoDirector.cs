using System;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Player;
using TMPro;
using UnityEngine;

namespace EscapeGame.World
{
    // 자기 정답 이후 대기·조작 잠금·영상 표현을 조립합니다. 문 열림/출구 이동을 관찰하지 않습니다.
    public sealed class EndingVideoDirector : MonoBehaviour
    {
        private FirstPersonPlayerRig rig;
        private PlayerPointerDriver pointer;
        private EndingVideoAssets assets;
        private UnityEngine.Video.VideoClip clip;
        private EndingCompletionController completion;
        private EndingPlaybackController playback;
        private EndingVideoView videoView;
        private EndingResultView resultView;
        private EscapeRunClock runClock;
        private EndingWaitingView waiting;
        private IDisposable controlBlock;
        private long retryAt;
        private bool completed, subscribed;
        private string reportedError, playbackError;
        public EndingPlaybackPhase Phase => completed ? EndingPlaybackPhase.Completed : playback?.Phase ?? EndingPlaybackPhase.Idle;
        public bool IsPlaying => Phase == EndingPlaybackPhase.Preparing || Phase == EndingPlaybackPhase.Playing;
        public event Action VideoCompleted;

        public void Initialize(FirstPersonPlayerRig rig, CsvGameData data, string role, IRoomCompletionService service, TMP_FontAsset font, EscapeRunClock runClock = null)
        {
            if (this.rig != null) throw new InvalidOperationException("엔딩은 플레이어별로 한 번만 초기화합니다.");
            if (rig == null || data == null || service == null || font == null) throw new ArgumentNullException(nameof(rig));
            var terminal = new RoomLoadPlan(data, role).Rooms.Where(room => room.Next == 0).ToArray();
            if (terminal.Length != 1 || terminal[0].ExitRule != RoomExitRules.BothSolved)
                throw new InvalidOperationException("엔딩 방은 Next=0, ExitRule=BothSolved인 활성 방 하나여야 합니다.");
            assets = Resources.Load<EndingVideoAssets>("EndingVideo")
                ?? throw new InvalidOperationException("Resources/EndingVideo 에셋이 없습니다.");
            assets.Validate();
            var config = data.GameConfigs.SingleOrDefault(row => row.Id == EndingVideoAssets.ConfigId)
                ?? throw new InvalidOperationException("GAME_CONFIG 16011 엔딩 영상 경로가 없습니다.");
            clip = assets.Resolve(config.Value);
            pointer = rig.GetComponent<PlayerPointerDriver>() ?? throw new InvalidOperationException("엔딩 조작 잠금에 플레이어 포인터가 필요합니다.");
            this.rig = rig;
            this.runClock = runClock ?? new EscapeRunClock(NowMs);
            waiting = new EndingWaitingView(gameObject.scene, font);
            completion = new EndingCompletionController(terminal[0].RoomNo, service, assets.CompletionPollIntervalMs);
            if (isActiveAndEnabled) Observe();
        }
        private void OnEnable() { if (completion != null) Observe(); }
        private void Observe()
        {
            if (!subscribed) { completion.Changed += ApplyCompletion; subscribed = true; }
            ApplyCompletion();
        }
        private void ApplyCompletion()
        {
            if (completion.Phase == EndingCompletionPhase.Playing) return;
            if (controlBlock == null)
            {
                controlBlock = pointer.Controls.BlockForUi();
                rig.GetComponent<PlayerInteractionDriver>()?.Closeup?.Close();
            }
            if (completed) { if (resultView != null) resultView.SetVisible(true); else videoView?.SetVisible(true); return; }
            if (completion.Phase == EndingCompletionPhase.Disconnected)
            { waiting.Render(true, "서버 연결이 종료되었습니다.\n게임을 종료한 뒤 다시 입장해 주세요."); return; }
            if (playback != null) videoView?.SetVisible(true);
            if (completion.Phase == EndingCompletionPhase.WaitingForPartner)
            {
                waiting.Render(true, completion.LastError == null
                    ? "정답을 맞혔습니다.\n다른 플레이어의 정답 입력을 기다리고 있습니다."
                    : "다른 플레이어의 정답 완료를 확인하고 있습니다.\n연결 상태를 확인한 뒤 다시 시도합니다.");
                if (completion.LastError != null && reportedError != completion.LastError)
                { reportedError = completion.LastError; Debug.LogWarning("[엔딩 대기] " + completion.LastError); }
            }
            else if (completion.Phase == EndingCompletionPhase.Ready && playback == null && NowMs >= retryAt) StartVideo();
        }
        private void LateUpdate()
        {
            if (completion == null || completed) return;
            long now = NowMs;
            completion.Tick(now);
            if (playback != null)
            {
                playback.Tick(now);
                if (playback.Phase == EndingPlaybackPhase.Failed) PlaybackFailed(playback.Error);
            }
            if (completion.Phase == EndingCompletionPhase.Ready && playback == null && now >= retryAt) StartVideo();
        }
        private void StartVideo()
        {
            try
            {
                videoView = new EndingVideoView(clip, gameObject.scene);
                videoView.PlaybackStarted += OnVideoStarted;
                playback = new EndingPlaybackController(videoView, assets.PrepareTimeoutMs);
                playback.Completed += OnCompleted;
                waiting.Render(false, ""); playback.Start(NowMs);
                Debug.Log("[엔딩] 두 플레이어의 마지막 방 정답 완료를 확인했습니다. 영상 준비 중입니다.");
            }
            catch (Exception error) { PlaybackFailed(error.Message); }
        }
        private void PlaybackFailed(string message)
        {
            if (playbackError != message) { playbackError = message; Debug.LogWarning("[엔딩] 영상 재생 실패: " + message); }
            ClearPlayback(); retryAt = NowMs + assets.RetryIntervalMs;
            waiting.Render(true, "엔딩 영상을 재생하지 못했습니다.\n잠시 후 다시 시도합니다.");
        }
        private void OnVideoStarted()
        {
            runClock.FreezeAtVideoStart(NowMs);
            rig.GetComponent<EscapeGame.Audio.GameFeedbackDriver>()?.SuspendForEnding();
        }
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnCompleted()
        {
            completed = true;
            try
            {
                var feedback = Resources.Load<GameFeedbackAssets>("GameFeedback");
                var font = Resources.Load<InteractionPresentationSettings>("InteractionPresentation").Font;
                if (!runClock.ElapsedMs.HasValue) throw new InvalidOperationException("영상 재생 시작 시각이 기록되지 않았습니다.");
                resultView = new EndingResultView(feedback?.ResultPrefab, gameObject.scene, font, runClock.ElapsedMs.Value, QuitGame);
                videoView?.SetVisible(false);
                Debug.Log("[엔딩] 결과 표시 / 탈출 시간 " + EscapeRunClock.Format(runClock.ElapsedMs.Value));
            }
            catch (Exception error) { Debug.LogError("[엔딩 결과] " + error.Message); waiting.Render(true, "결과 화면을 표시하지 못했습니다. 설정을 확인해 주세요."); }
            VideoCompleted?.Invoke();
        }
        private static long NowMs => (long)(Time.realtimeSinceStartupAsDouble * 1000);
        private void ClearPlayback()
        {
            if (videoView != null) videoView.PlaybackStarted -= OnVideoStarted;
            if (playback != null) { playback.Completed -= OnCompleted; playback.Dispose(); playback = null; videoView = null; }
        }
        private void OnDisable()
        {
            if (subscribed) { completion.Changed -= ApplyCompletion; subscribed = false; }
            videoView?.SetVisible(false); resultView?.SetVisible(false); waiting?.Render(false, ""); controlBlock?.Dispose(); controlBlock = null;
        }
        private void OnDestroy()
        {
            completion?.Dispose(); ClearPlayback(); resultView?.Dispose(); waiting?.Dispose(); controlBlock?.Dispose();
            VideoCompleted = null;
        }
    }
}
