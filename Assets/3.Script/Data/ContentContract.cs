// protocol/data/content-contract.json에서 생성. 직접 수정하지 않습니다.
namespace EscapeGame.Data
{
    public static class ContentContract
    {
        public static System.Collections.Generic.IReadOnlyList<string> Features { get; } = System.Array.AsReadOnly(new[] { "Interactable", "CloseUpUI", "DialLock", "ButtonConsole", "Door", "LetterSign", "ScreenText", "Spawn" });
        public static System.Collections.Generic.IReadOnlyList<string> PuzzleTypes { get; } = System.Array.AsReadOnly(new[] { "CardLetter", "FrameCount", "ArrowSequence", "WireRank", "TwistedFormula", "TVLetter", "ColourWord" });
        public static System.Collections.Generic.IReadOnlyList<string> Colors { get; } = System.Array.AsReadOnly(new[] { "red", "cyan", "blue", "darkblue", "lightblue", "purple", "yellow", "lime", "fuchsia", "white", "silver", "grey", "gray", "black", "orange", "brown", "maroon", "green", "olive", "navy", "teal", "aqua", "magenta" });
        public static string RequiredParameter(string feature) => feature switch { "Interactable" => "HighlightColor", "CloseUpUI" => "CamOffset", _ => null };
    }
}
