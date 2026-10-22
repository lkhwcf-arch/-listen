using System;
using System.Collections.Generic;
using EscapeGame.Audio;
using UnityEngine;

namespace EscapeGame.Data
{
    // 사운드 CSV와 독립적인 클라이언트 표현 설정입니다. Inspector가 설정 원본입니다.
    [CreateAssetMenu(menuName = "EscapeGame/게임 사운드·결과 화면 설정")]
    public sealed class GameFeedbackAssets : ScriptableObject
    {
        [Serializable] public sealed class Effect
        {
            public GameAudioCue cue;
            public AudioClip[] clips;
            [Range(0, 1)] public float volume = .6f;
            [Min(0)] public int cooldownMs;
            public bool closeupOnly, spatial;
            [Min(.01f)] public float minDistance = 1;
            [Min(.01f)] public float maxDistance = 15;
        }
        [Serializable] public sealed class RoomAmbient
        {
            [Min(1)] public int roomNo;
            public AudioClip clip;
            [Range(0, 1)] public float volume = .4f;
        }
        [SerializeField] private Effect[] effects = Array.Empty<Effect>();
        [SerializeField] private RoomAmbient[] ambient = Array.Empty<RoomAmbient>();
        [SerializeField] private GameObject resultPrefab;
        [SerializeField] private float stepDistanceMeters = .75f, headBobReturnSeconds = .12f, ambientFadeSeconds = 3;
        [SerializeField] private int effectVoices = 12;
        [SerializeField] private float doorwayMarginMeters = .05f, maximumCrossingSegmentMeters = 2;
        private Dictionary<GameAudioCue, Effect> index;
        public GameObject ResultPrefab => resultPrefab;
        public IReadOnlyList<RoomAmbient> Ambient => ambient;
        public float StepDistanceMeters => stepDistanceMeters;
        public float HeadBobReturnSeconds => headBobReturnSeconds;
        public float AmbientFadeSeconds => ambientFadeSeconds;
        public int EffectVoices => effectVoices;
        public float DoorwayMarginMeters => doorwayMarginMeters;
        public float MaximumCrossingSegmentMeters => maximumCrossingSegmentMeters;
        private void OnValidate() => index = null;
        public Effect Find(GameAudioCue cue)
        {
            if (index == null)
            {
                index = new Dictionary<GameAudioCue, Effect>();
                foreach (var effect in effects) index.Add(effect.cue, effect);
            }
            return index.TryGetValue(cue, out var value) ? value : null;
        }
        public void Validate()
        {
            bool Positive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0;
            bool Volume(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
            if (!Positive(stepDistanceMeters) || !Positive(headBobReturnSeconds) || !Positive(ambientFadeSeconds) ||
                !Positive(doorwayMarginMeters) || !Positive(maximumCrossingSegmentMeters) || effectVoices < 1 || effectVoices > 32)
                throw new InvalidOperationException("GameFeedback의 양수 거리·시간 및 효과음 채널(1~32)을 확인하세요.");
            var cues = new HashSet<GameAudioCue>();
            foreach (var effect in effects)
            {
                if (effect == null || !Enum.IsDefined(typeof(GameAudioCue), effect.cue) || !cues.Add(effect.cue) || !Volume(effect.volume) ||
                    effect.cooldownMs < 0 || effect.clips == null || effect.clips.Length == 0 ||
                    (effect.spatial && (!Positive(effect.minDistance) || !Positive(effect.maxDistance) || effect.maxDistance < effect.minDistance)))
                    throw new InvalidOperationException("효과음 키·볼륨·클립 목록·거리·중복을 확인하세요.");
                foreach (var clip in effect.clips) if (clip == null) throw new InvalidOperationException("효과음 클립 참조가 비어 있습니다: " + effect.cue);
            }
            var rooms = new HashSet<int>();
            foreach (var room in ambient)
                if (room == null || room.roomNo <= 0 || !rooms.Add(room.roomNo) || room.clip == null || !Volume(room.volume))
                    throw new InvalidOperationException("방 배경음 번호·클립·볼륨·중복을 확인하세요.");
            index = null;
        }
    }
}
