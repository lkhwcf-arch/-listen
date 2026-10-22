using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Player;
using EscapeGame.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeGame.Audio
{
    // 방 데이터는 경계/진행 연결에만 사용합니다. 사운드 시트·서버 오디오 요청에는 의존하지 않습니다.
    public sealed class GameFeedbackDriver : MonoBehaviour
    {
        private GameFeedbackAssets settings;
        private RoomLoadPlan plan;
        private FirstPersonPlayerRig rig;
        private PlayerInteractionDriver interaction;
        private PlayerGaitDriver gait;
        private IngameWorldDriver world;
        private readonly HashSet<int> loaded = new HashSet<int>();
        private readonly Dictionary<int, Transform> anchors = new Dictionary<int, Transform>();
        private readonly AudioCuePlaybackState playbackState = new AudioCuePlaybackState();
        private readonly List<RoomDoorwayAudio> boundaries = new List<RoomDoorwayAudio>();
        private GameObject outputRoot;
        private AudioSource[] loops, voices;
        private AmbientCrossfade fade;
        private int voiceCursor, currentRoom;
        private Vector3 previous;
        private bool suspended;
        public int CurrentRoomNo => plan?.Find(currentRoom)?.RoomNo ?? 0;
        public event Action<int> RoomEntered;
        public void Initialize(FirstPersonPlayerRig rig, CsvGameData data, string role, GameFeedbackAssets settings, RoomSceneView first)
        {
            if (plan != null) throw new InvalidOperationException("게임 효과는 한 번만 초기화합니다.");
            settings.Validate(); this.settings = settings; this.rig = rig;
            plan = new RoomLoadPlan(data, role);
            outputRoot = new GameObject("게임 사운드 출력"); SceneManager.MoveGameObjectToScene(outputRoot, gameObject.scene);
            loops = new AudioSource[settings.Ambient.Count]; fade = new AmbientCrossfade(Math.Max(1, loops.Length), settings.AmbientFadeSeconds);
            voices = new AudioSource[settings.EffectVoices];
            for (int i = 0; i < voices.Length; i++) voices[i] = MakeSource("효과음_" + i);
            interaction = rig.GetComponent<PlayerInteractionDriver>(); interaction.FeedbackEmitted += OnFeedback;
            gait = rig.GetComponent<PlayerGaitDriver>(); if (gait != null) gait.Stepped += OnStep;
            previous = rig.PlayerCamera.transform.position;
            RegisterRoom(first); EnterRoom(plan.First.Id, true);
        }
        public void BindWorld(IngameWorldDriver world)
        { this.world = world; world.RoomLoaded += OnRoomLoaded; foreach (var room in world.LoadedRooms.Values) RegisterRoom(room); }
        private void OnRoomLoaded(RoomData row, RoomSceneView view) => RegisterRoom(view);
        public void RegisterRoom(RoomSceneView view)
        {
            if (this == null || view == null || plan == null) return;
            var row = plan.Rooms.SingleOrDefault(item => item.RoomNo == view.RoomNo);
            if (row == null || !loaded.Add(row.Id)) return;
            foreach (var anchor in view.GetComponentsInChildren<AnchorInstance>(true))
            {
                if (anchor.Definition == null) continue;
                anchors[anchor.Definition.Id] = anchor.transform;
                var door = anchor.GetComponent<WorldDoorView>();
                if (door != null && row.Next != 0) boundaries.Add(new RoomDoorwayAudio(door, row.Id, row.Next, settings.DoorwayMarginMeters));
            }
        }
        private void OnFeedback(GameAudioCue cue, AnchorData anchor) => PlayCue(cue, anchor);
        private void OnStep() => PlayCue(GameAudioCue.Footstep);
        private void PlayCue(GameAudioCue cue, AnchorData anchor = null)
        {
            if (plan == null || suspended || !isActiveAndEnabled) return;
            var sound = settings.Find(cue); if (sound == null) return;
            // 닫기 피드백은 실제 Closeup 종료 이벤트의 마지막 프레임에 한해 허용합니다.
            bool closing = cue == GameAudioCue.CloseupClose || cue == GameAudioCue.ClickClose;
            long nowMs = (long)(Time.realtimeSinceStartupAsDouble * 1000);
            if (!playbackState.TryTake(cue, sound.clips.Length, sound.cooldownMs, sound.closeupOnly,
                interaction.Closeup.IsOpen, closing, nowMs, out int cursor)) return;
            var clip = sound.clips[cursor];
            var source = voices[voiceCursor];
            for (int i = 0; i < voices.Length; i++) if (!voices[i].isPlaying) { source = voices[i]; break; }
            voiceCursor = (voiceCursor + 1) % voices.Length; source.Stop();
            source.transform.position = anchor != null && anchors.TryGetValue(anchor.Id, out var origin) && origin != null ? origin.position : rig.transform.position;
            source.volume = sound.volume; source.spatialBlend = sound.spatial ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = sound.minDistance; source.maxDistance = sound.maxDistance;
            source.clip = clip; source.loop = false; source.Play();
        }
        private void EnterRoom(int roomId, bool immediate = false)
        {
            if (!immediate && currentRoom == roomId) return;
            currentRoom = roomId; int index = -1;
            for (int i = 0; i < settings.Ambient.Count; i++) if (settings.Ambient[i].roomNo == CurrentRoomNo) { index = i; break; }
            fade.Select(index, immediate); RoomEntered?.Invoke(CurrentRoomNo);
            Debug.Log($"[방 배경음] {CurrentRoomNo}번 방 / 전환 {settings.AmbientFadeSeconds:0.##}초");
        }
        private void LateUpdate()
        {
            if (plan == null || rig == null) return;
            var current = rig.PlayerCamera.transform.position;
            if (!suspended)
            {
                if ((current - previous).sqrMagnitude <= settings.MaximumCrossingSegmentMeters * settings.MaximumCrossingSegmentMeters)
                {
                    foreach (var boundary in boundaries)
                    {
                        if (currentRoom != boundary.FromId && currentRoom != boundary.ToId) continue;
                        bool crossed = boundary.Crossed(previous, current);
                        if (crossed && loaded.Contains(boundary.FromId) && loaded.Contains(boundary.ToId))
                        { EnterRoom(currentRoom == boundary.FromId ? boundary.ToId : boundary.FromId); break; }
                    }
                }
                else foreach (var boundary in boundaries) boundary.Reset();
                fade.Tick(Time.unscaledDeltaTime);
                for (int i = 0; i < loops.Length; i++)
                {
                    float gain = fade.Gain(i);
                    if (gain <= 0) { if (loops[i] != null && loops[i].isPlaying) loops[i].Stop(); continue; }
                    if (loops[i] == null) { loops[i] = MakeSource("방 배경음_" + settings.Ambient[i].roomNo); loops[i].clip = settings.Ambient[i].clip; loops[i].loop = true; }
                    loops[i].volume = settings.Ambient[i].volume * gain;
                    if (!loops[i].isPlaying) loops[i].Play();
                }
            }
            previous = current;
        }
        public void SuspendForEnding()
        {
            suspended = true;
            if (outputRoot != null) foreach (var source in outputRoot.GetComponentsInChildren<AudioSource>(true)) source.Stop();
        }
        private AudioSource MakeSource(string label)
        {
            var go = new GameObject(label); go.transform.SetParent(outputRoot.transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.dopplerLevel = 0; return source;
        }
        private void OnDisable()
        { if (outputRoot != null) foreach (var source in outputRoot.GetComponentsInChildren<AudioSource>(true)) source.Stop(); }
        private void OnDestroy()
        {
            if (interaction != null) interaction.FeedbackEmitted -= OnFeedback;
            if (gait != null) gait.Stepped -= OnStep;
            if (world != null) world.RoomLoaded -= OnRoomLoaded;
            if (outputRoot != null) Destroy(outputRoot); RoomEntered = null;
        }
    }
}
