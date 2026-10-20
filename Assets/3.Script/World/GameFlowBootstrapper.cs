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
        private EscapeGame.Player.PlayerSpawnDriver playerSpawn;
        public bool PlayerReady => playerSpawn != null && playerSpawn.Player != null &&
            playerSpawn.Player.gameObject.activeInHierarchy && playerSpawn.Player.GetComponent<EscapeGame.Player.PlayerMovementDriver>()?.IsReady == true &&
            playerSpawn.Player.GetComponent<EscapeGame.Player.PlayerLookDriver>()?.IsReady == true &&
            playerSpawn.Player.GetComponent<EscapeGame.Interaction.PlayerInteractionDriver>()?.IsReady == true;
        public bool IsGameplayReady => WorldReady && PlayerReady && SceneManager.GetActiveScene().isLoaded &&
            SceneManager.GetActiveScene().name == ingameScene;
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
            Service = new GameEntryService(session, (ISessionContentService)session, assets.ReadLocalData,
                assets.CreateValidator(), EnterSceneAsync, destroyCancellationToken);
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
            token.ThrowIfCancellationRequested();
            // 실제 씬 로더를 명시적으로 조립합니다. Start 실행 순서에 기대지 않습니다.
            var scene = SceneManager.GetActiveScene();
            var worlds = System.Array.FindAll(FindObjectsByType<IngameWorldDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None),
                world => world.gameObject.scene == scene);
            if (worlds.Length != 1) throw new InvalidOperationException("Ingame 씬에 활성 IngameWorldDriver가 정확히 하나 필요합니다.");
            var spawnHost = new GameObject("LocalPlayerSpawn");
            SceneManager.MoveGameObjectToScene(spawnHost, scene);
            try
            {
                worlds[0].Initialize(this);
                var firstRoom = worlds[0].RequireFirstRoom();
                playerSpawn = spawnHost.AddComponent<EscapeGame.Player.PlayerSpawnDriver>();
                playerSpawn.Initialize(Service.Data, Service.Role, firstRoom, Service);
                if (!PlayerReady) throw new InvalidOperationException("플레이어 입력·충돌 준비가 완료되지 않았습니다.");
            }
            catch (Exception error)
            {
                WorldReady = false;
                Destroy(spawnHost);
                Debug.LogError("[Game Entry] " + error.Message);
                throw; // 입장 서비스가 실패 상태·참가 자리 정리를 수행합니다.
            }
        }
        private void OnDestroy()
        {
            if (playerSpawn != null) Destroy(playerSpawn.gameObject);
            if (Service == null) return;
            Service.Changed -= OnEntryChanged;
            Service.Dispose();
        }
    }
}
