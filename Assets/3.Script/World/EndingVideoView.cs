using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace EscapeGame.World
{
    // VideoPlayer 이벤트와 전체 화면 표현을 재생 포트에 연결하는 Adapter입니다.
    public sealed class EndingVideoView : IEndingVideoPlayback
    {
        private GameObject root;
        private VideoPlayer player;
        private RenderTexture texture;
        public event Action Prepared;
        public event Action PlaybackStarted;
        public event Action Finished;
        public event Action<string> Failed;
        public EndingVideoView(VideoClip clip, Scene scene)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            try
            {
                int width = clip.width > 0 ? checked((int)clip.width) : 1920;
                int height = clip.height > 0 ? checked((int)clip.height) : 1080;
                texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "EndingVideo" };
                if (!texture.Create()) throw new InvalidOperationException("엔딩 영상 화면을 만들지 못했습니다.");
                var previous = RenderTexture.active;
                try { RenderTexture.active = texture; GL.Clear(true, true, Color.black); }
                finally { RenderTexture.active = previous; }
                root = new GameObject("EndingVideoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                root.GetComponent<Image>().color = Color.black;
                var screen = new GameObject("EndingVideo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
                screen.transform.SetParent(root.transform, false); screen.GetComponent<RawImage>().texture = texture;
                var fitter = screen.GetComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)width / height;
                player = root.AddComponent<VideoPlayer>(); player.playOnAwake = false; player.isLooping = false;
                player.source = VideoSource.VideoClip; player.clip = clip;
                player.renderMode = VideoRenderMode.RenderTexture; player.targetTexture = texture;
                player.audioOutputMode = VideoAudioOutputMode.Direct; player.waitForFirstFrame = true;
                if (clip.audioTrackCount > 0)
                {
                    player.controlledAudioTrackCount = 1; player.EnableAudioTrack(0, true);
                    player.SetDirectAudioMute(0, false); player.SetDirectAudioVolume(0, 1);
                }
                player.started += OnStarted; player.prepareCompleted += OnPrepared; player.loopPointReached += OnFinished; player.errorReceived += OnError;
            }
            catch { Dispose(); throw; }
        }
        public void SetVisible(bool visible)
        {
            if (root == null) return;
            root.SetActive(visible);
            if (!visible && player != null && player.isPlaying) player.Pause();
            else if (visible && player != null && player.isPaused && player.frame < (long)player.frameCount - 1) player.Play();
        }
        public void Prepare() => player.Prepare();
        public void Play() => player.Play();
        private void OnStarted(VideoPlayer source) => PlaybackStarted?.Invoke();
        private void OnPrepared(VideoPlayer source) => Prepared?.Invoke();
        private void OnFinished(VideoPlayer source) { source.Pause(); Finished?.Invoke(); }
        private void OnError(VideoPlayer source, string message) => Failed?.Invoke(message);
        public void Dispose()
        {
            if (player != null)
            {
                player.started -= OnStarted; player.prepareCompleted -= OnPrepared; player.loopPointReached -= OnFinished; player.errorReceived -= OnError;
                player.Stop(); player.targetTexture = null;
            }
            if (root != null) { root.SetActive(false); Object.Destroy(root); }
            if (texture != null) { texture.Release(); Object.Destroy(texture); }
            root = null; player = null; texture = null; Prepared = null; Finished = null; Failed = null; PlaybackStarted = null;
        }
    }
}
