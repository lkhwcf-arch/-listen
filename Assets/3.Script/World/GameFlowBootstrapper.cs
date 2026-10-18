using System;
using System.Threading;
using System.Threading.Tasks;
using EscapeGame.Data;
using EscapeGame.Session;
using EscapeGame.Session.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EscapeGame.World
{
    [DefaultExecutionOrder(-500), RequireComponent(typeof(SessionBootstrapper))]
    public sealed class GameFlowBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameContentAssets assets;
        [SerializeField] private string ingameScene = "Ingame";
        public GameEntryService Service { get; private set; }
        public GameContentAssets Assets => assets;
        public bool WorldReady { get; set; }
        private void Awake()
        {
            var owners = FindObjectsByType<GameFlowBootstrapper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (owners.Length > 1)
            {
                foreach (var owner in owners)
                    if (owner != this && owner.Service != null) { Destroy(gameObject); return; }
            }
            if (transform.parent != null) throw new InvalidOperationException("게임 수명 객체는 씬 루트에 배치하세요.");
            if (assets == null) assets = Resources.Load<GameContentAssets>("GameContent");
            if (assets == null) throw new InvalidOperationException("GameContent 에셋이 필요합니다.");
            var session = GetComponent<SessionBootstrapper>().Service;
            Service = new GameEntryService(session, (ISessionContentService)session, assets.ReadLocalData, assets.CreateValidator(), EnterSceneAsync);
            Service.Changed += OnEntryChanged;
            DontDestroyOnLoad(gameObject);
        }
        private void OnEntryChanged() { if (Service.IsBusy) WorldReady = false; }
        private async Task EnterSceneAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorldReady = false;
            var operation = SceneManager.LoadSceneAsync(ingameScene);
            if (operation == null) throw new InvalidOperationException("Ingame 씬이 빌드 목록에 없습니다.");
            // Unity 씬 로드는 시작 후 취소할 수 없습니다. 서비스 수명은 씬 전환 뒤에도 유지됩니다.
            while (!operation.isDone) await Task.Yield();
        }
        private void OnDestroy()
        {
            if (Service == null) return;
            Service.Changed -= OnEntryChanged;
            Service.Dispose();
        }
    }
}
