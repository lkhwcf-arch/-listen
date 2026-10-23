using EscapeGame.Audio;
using EscapeGame.Player;
using EscapeGame.World;
int count = 0;
void Check(bool ok, string why)
{
    if (!ok)
        throw new Exception(why);
    count++;
}
bool Near(double a, double b) => Math.Abs(a - b) < .00001;
var gait = new PlayerGait(.75, .02, .12);
int steps = 0;
gait.Stepped += () => steps++;
for (int i = 0; i < 100; i++) gait.Tick(0, true, true, .016);
Check(steps == 0 && gait.OffsetMeters == 0, "벽에 막힌 입력은 보행하지 않음");
gait.Tick(.2, true, true, .1);
Check(Math.Abs(gait.OffsetMeters) <= .02 && gait.OffsetMeters != 0, "시트 진폭 안에서 보행");
gait.Tick(.55, true, true, .1);
Check(steps == 1, "실제 거리 누적으로 발소리 한 번");
gait.Tick(2.25, true, true, .1);
Check(steps == 4, "여러 보폭을 순서대로 진행");
gait.Tick(.1, true, true, .1);
double moving = gait.OffsetMeters;
gait.Tick(0, true, false, .1);
Check(Math.Abs(gait.OffsetMeters) < Math.Abs(moving), "조작 잠금 때 눈높이 복귀");
gait.Tick(1, false, true, .1);
Check(steps == 4, "공중에서는 발소리 없음");
gait.Reset();
Check(gait.OffsetMeters == 0, "재활성화 기준점 초기화");
var bank = new AudioCuePlaybackState();
for (int i = 0; i < 7; i++) Check(bank.TryTake(GameAudioCue.Footstep, 3, 0, false, false, false, i * 300, out int clip) && clip == i % 3, "발소리 1-2-3 순환");
Check(!bank.TryTake(GameAudioCue.Click, 1, 0, false, false, false, 3000, out _), "UI 큐는 Closeup 밖에서 차단");
Check(bank.TryTake(GameAudioCue.Click, 2, 100, true, true, false, 3000, out int first) && first == 0, "Closeup 클릭 첫 클립");
Check(!bank.TryTake(GameAudioCue.Click, 2, 100, true, true, false, 3050, out _), "쿨다운 중 재생 억제");
Check(bank.TryTake(GameAudioCue.Click, 2, 100, true, true, false, 3100, out int second) && second == 1, "억제한 요청은 클립 순서를 소비하지 않음");
Check(bank.TryTake(GameAudioCue.CloseupClose, 1, 0, true, false, true, 3200, out _), "닫기 전환의 마지막 효과음 허용");
Check(!bank.TryTake(GameAudioCue.Click, 1, 0, true, false, true, 3300, out _), "닫기 플래그가 일반 클릭의 제한을 풀지 않음");
Check(bank.TryTake(GameAudioCue.DoorOpen, 1, 0, false, false, false, 3300, out _), "문 SFX는 UI와 독립");
var fade = new AmbientCrossfade(3, 3);
fade.Select(0, true);
fade.Tick(.016);
Check(Near(fade.Gain(0), 1), "첫 방 즉시 선택이 다음 프레임에도 유지");
fade.Select(1);
fade.Tick(1.5);
Check(Near(fade.Gain(0), .5) && Near(fade.Gain(1), .5), "3초 전환 중간 볼륨");
fade.Select(0);
Check(Near(fade.Gain(0), .5), "역주행 순간 볼륨 점프 없음");
fade.Tick(1.5);
Check(Near(fade.Gain(0), .75) && Near(fade.Gain(1), .25), "역주행 보간");
fade.Select(2);
fade.Tick(3);
Check(Near(fade.Gain(0), 0) && Near(fade.Gain(1), 0) && Near(fade.Gain(2), 1), "세 번째 방까지 빠르게 이동해도 이전 음 완전히 소거");
fade.Select(2);
fade.Tick(.5);
Check(Near(fade.Gain(2), 1), "같은 방은 재시작하지 않음");
fade.Select(-1);
fade.Tick(3);
Check(Near(fade.Gain(2), 0), "대상 없는 방은 무음으로 전환");
var portal = new DoorwayCrossing(.05);
Check(!portal.Tick(0, 0, 1, 0, 0, .2, 1, 2, true), "처음 샘플은 기준만 설정");
Check(!portal.Tick(0, 0, .2, 0, 0, .01, 1, 2, true), "문턱 중립 구간은 전환 안 함");
Check(portal.Tick(0, 0, .01, 0, 0, -.1, 1, 2, true), "반대편으로 안정적으로 넘어가면 입장");
Check(!portal.Tick(0, 0, -.1, 0, 0, -.2, 1, 2, true), "같은 쪽에서 중복 입장 방지");
Check(portal.Tick(0, 0, -.2, 0, 0, .2, 1, 2, true), "이전 방으로 복귀");
Check(!portal.Tick(3, 0, .2, 3, 0, -.2, 1, 2, true), "문 폭 바깥의 벽 통과는 방 전환 아님");
Check(!portal.Tick(0, 0, -.2, 0, 0, .2, 1, 2, false), "닫힌 문은 전환 안 함");
var clock = new EscapeRunClock(5000);
Check(!clock.ElapsedMs.HasValue, "영상 시작 전 측정 계속");
clock.FreezeAtVideoStart(127345);
Check(clock.ElapsedMs == 122345, "씬 진입부터 실제 영상 재생 시작까지");
clock.FreezeAtVideoStart(999999);
Check(clock.ElapsedMs == 122345, "영상 재시작에도 첫 시작 기록 유지");
Check(EscapeRunClock.Format(clock.ElapsedMs.Value) == "00:02:02", "시분초 표시");
Check(EscapeRunClock.Format(27L * 3600000) == "27:00:00", "하루가 넘어도 시간 유지");
var idle = new AmbientCrossfade(7, 3);
Check(!idle.IsTransitioning && !idle.Tick(.016), "선택 전 불필요한 전환 없음");
idle.Select(0, true);
Check(!idle.IsTransitioning && !idle.Tick(.016), "즉시 선택 뒤 반복 볼륨 갱신 없음");
idle.Select(1);
Check(idle.IsTransitioning && idle.Tick(1.5), "전환 중에는 변경을 알려 어댑터 갱신");
Check(idle.Tick(1.5) && !idle.IsTransitioning, "마지막 프레임은 정확한 최종 볼륨 적용");
Check(!idle.Tick(.016) && Near(idle.Gain(1), 1), "전환 완료 후 변경 없음");
idle.Select(0);
Check(!idle.Tick(0) && idle.IsTransitioning, "0초 프레임은 전환을 소모하지 않음");
bool invalidTime = false;
try
{
    idle.Tick(double.NaN);
}
catch (ArgumentOutOfRangeException)
{
    invalidTime = true;
}
Check(invalidTime, "대기 중에도 잘못된 시간 거부");
var expected = new[] { GameAudioCue.Footstep, GameAudioCue.Click, GameAudioCue.ClickClose, GameAudioCue.CloseupOpen, GameAudioCue.CloseupClose,
    GameAudioCue.DialTurn, GameAudioCue.Confirm, GameAudioCue.Button, GameAudioCue.Reset, GameAudioCue.DoorOpen, GameAudioCue.DoorClose,
    GameAudioCue.DoorLocked, GameAudioCue.Correct, GameAudioCue.Wrong };
Check(expected.Select(cue => (int)cue).SequenceEqual(Enumerable.Range(0, 14)), "기존 에셋 사운드 ID 0~13 보존");
Console.WriteLine($"보행·사운드·크로스페이드·문 경계·탈출 시간 검사 {count}개 통과");
