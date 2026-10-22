using System;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Interaction;
using EscapeGame.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace EscapeGame.World
{
    // 마지막 방(ROOM.Next=0)의 문이 열리면 엔딩 영상을 전체 화면으로 한 번 재생합니다.
    // 문 허가는 기존 문 판정(ExitRule)을 그대로 사용하고, 이 스크립트는 열린 결과만 관찰합니다.
    public sealed class EndingVideoDirector : MonoBehaviour
    {
        private const float ScanSeconds = .25f;
        private float nextScan;
        private WorldDoorView watched, played;
        private GameObject root;
        private VideoPlayer player;
        private RenderTexture texture;
        private IDisposable block;
        public bool IsPlaying => root != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<EndingVideoDirector>() != null) return;
            var host = new GameObject(nameof(EndingVideoDirector));
            DontDestroyOnLoad(host);
            host.AddComponent<EndingVideoDirector>();
        }
        private void Update()
        {
            if (root == null && texture != null) Clear(); // 재생 중 씬이 바뀐 경우
            if (watched == null)
            {
                if (Time.unscaledTime < nextScan) return;
                nextScan = Time.unscaledTime + ScanSeconds;
                watched = FindObjectsByType<WorldDoorView>(FindObjectsSortMode.None).FirstOrDefault(IsFinalRoomDoor);
                return;
            }
            if (watched.IsOpen && watched != played && !IsPlaying) Play(watched);
        }
        private static bool IsFinalRoomDoor(WorldDoorView door)
        {
            var target = door.GetComponent<WorldInteractionTarget>();
            if (target == null || target.Anchor == null || target.Data == null) return false;
            return target.Data.RoomsA.Cast<RoomData>().Concat(target.Data.RoomsB)
                .Any(room => room.Active && room.Next == 0 && room.RoomNo == target.Anchor.RoomNo);
        }
        private void Play(WorldDoorView door)
        {
            played = door;
            var config = door.GetComponent<WorldInteractionTarget>().Data.GameConfigs.FirstOrDefault(row => row.Id == EndingVideoAssets.ConfigId);
            var assets = Resources.Load<EndingVideoAssets>("EndingVideo");
            VideoClip clip;
            try
            {
                if (config == null) throw new InvalidOperationException("GAME_CONFIG 16011 엔딩 영상 경로가 없습니다.");
                if (assets == null) throw new InvalidOperationException("Resources/EndingVideo 에셋이 없습니다.");
                clip = assets.Resolve(config.Value);
            }
            catch (Exception error) { Debug.LogWarning("[엔딩] " + error.Message); return; }

            var pointer = FindFirstObjectByType<PlayerPointerDriver>();
            if (pointer != null && pointer.Controls != null) block = pointer.Controls.BlockForUi();
            texture = new RenderTexture(clip.width > 0 ? (int)clip.width : 1920, clip.height > 0 ? (int)clip.height : 1080, 0);

            root = new GameObject("EndingVideoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            root.GetComponent<Image>().color = Color.black;
            var screen = new GameObject("EndingVideo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            screen.transform.SetParent(root.transform, false);
            screen.GetComponent<RawImage>().texture = texture;
            var fitter = screen.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = (float)texture.width / texture.height;

            player = root.AddComponent<VideoPlayer>();
            player.playOnAwake = false; player.isLooping = false; player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = texture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.loopPointReached += OnFinished;
            player.Play();
        }
        // 결과 화면 연결 전까지는 영상이 끝나면 조작을 돌려줍니다.
        private void OnFinished(VideoPlayer source) => Clear();
        private void Clear()
        {
            if (player != null) player.loopPointReached -= OnFinished;
            if (root != null) Destroy(root);
            if (texture != null) { texture.Release(); Destroy(texture); }
            block?.Dispose();
            root = null; player = null; texture = null; block = null;
        }
        private void OnDestroy() => Clear();
    }
}
