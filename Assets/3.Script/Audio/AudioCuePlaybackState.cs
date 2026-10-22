using System;
using System.Collections.Generic;

namespace EscapeGame.Audio
{
    // 클립 순서와 쿨다운의 상태 소유자입니다. AudioSource·CSV·Unity 시간에 의존하지 않습니다.
    public sealed class AudioCuePlaybackState
    {
        private readonly Dictionary<GameAudioCue, int> cursors = new Dictionary<GameAudioCue, int>();
        private readonly Dictionary<GameAudioCue, long> availableAt = new Dictionary<GameAudioCue, long>();
        public static bool IsUi(GameAudioCue cue) => cue == GameAudioCue.Click || cue == GameAudioCue.ClickClose ||
            cue == GameAudioCue.CloseupOpen || cue == GameAudioCue.CloseupClose || cue == GameAudioCue.DialTurn ||
            cue == GameAudioCue.Confirm || cue == GameAudioCue.Button || cue == GameAudioCue.Reset;
        public bool TryTake(GameAudioCue cue, int clipCount, int cooldownMs, bool closeupOnly, bool closeupVisible,
            bool closingTransition, long nowMs, out int clipIndex)
        {
            clipIndex = -1;
            if (nowMs < 0 || cooldownMs < 0) throw new ArgumentOutOfRangeException();
            bool closing = closingTransition && (cue == GameAudioCue.ClickClose || cue == GameAudioCue.CloseupClose);
            if (clipCount <= 0 || ((closeupOnly || IsUi(cue)) && !closeupVisible && !closing) ||
                (availableAt.TryGetValue(cue, out var next) && nowMs < next)) return false;
            int cursor = cursors.TryGetValue(cue, out var current) ? current : 0;
            clipIndex = cursor % clipCount; cursors[cue] = (clipIndex + 1) % clipCount;
            availableAt[cue] = checked(nowMs + cooldownMs); return true;
        }
    }
}
