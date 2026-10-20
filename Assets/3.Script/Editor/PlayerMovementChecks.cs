#if UNITY_EDITOR
using System;
using System.Linq;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using EscapeGame.Player;
using EscapeGame.Timing;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class PlayerMovementChecks
    {
        private sealed class Clock : IMillisecondClock { public long ElapsedMilliseconds { get; set; } }
        private sealed class Input : IPlayerMoveInput
        {
            public bool Enabled, Disposed;
            public PlayerMoveCommand Read() => Enabled ? new PlayerMoveCommand(0, 1000) : default;
            public void SetEnabled(bool value) => Enabled = value;
            public void Dispose() { Enabled = false; Disposed = true; }
        }

        [MenuItem("Tools/Game/Verify Player Movement")]
        public static void Run()
        {
            if (!Application.isPlaying || !Application.isFocused || Time.timeScale == 0)
            { Debug.LogWarning("Play 중 Unity 창에 포커스가 있고 Time Scale이 0이 아닐 때 검사하세요."); return; }
            int checks = 0;
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); checks++; }
            var options = Resources.Load<PlayerPresentationSettings>("PlayerPresentation"); options.Validate();
            Check(options.InputActions == AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/10.Input/InputSystem_Actions.inputactions"), "serialized input reference resolves");
            var move = options.InputActions.FindAction("Player/Move", true);
            foreach (string key in new[] { "w", "a", "s", "d" })
                Check(move.bindings.Any(binding => binding.path == "<Keyboard>/" + key), "keyboard binding " + key);
            bool originalEnabled = move.enabled;
            using (var input = new InputSystemMoveInput(options.InputActions))
            {
                input.SetEnabled(true);
                Check(move.enabled == originalEnabled, "clone does not enable source action");
                input.SetEnabled(false); Check(input.Read().IsIdle, "disabled input emits idle command");
            }
            Check(move.enabled == originalEnabled, "clone disposal preserves source action");
            var assets = Resources.Load<GameContentAssets>("GameContent"); var store = new CsvDataStore();
            if (!store.TryRegister(assets.ReadLocalData(), assets.CreateValidator(), out var report)) throw new InvalidOperationException("CSV 검증 실패");
            var settings = PlayerCharacterSettings.Select(new CsvGameData(store), options.CharacterConfigId);
            var scene = SceneManager.CreateScene("[Temporary] Player Movement Checks");
            var root = new GameObject("MovementChecks"); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(1000, 10, 1000);
            var fake = new Input();
            try
            {
                void Box(string name, Vector3 position, Vector3 scale)
                {
                    var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
                    item.transform.SetParent(root.transform, false); item.transform.localPosition = position; item.transform.localScale = scale;
                }
                Box("Floor", new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20));
                Box("Wall", new Vector3(0, 1.5f, 2.5f), new Vector3(20, 3, 0.5f));
                var player = new GameObject("TemporaryPlayer"); player.SetActive(false); player.transform.SetParent(root.transform, false);
                var body = player.AddComponent<CharacterController>();
                body.height = settings.CapsuleHeightMm / 1000f; body.radius = settings.CapsuleRadiusMm / 1000f;
                body.center = Vector3.up * body.height / 2; body.stepOffset = settings.StepOffsetMm / 1000f; body.minMoveDistance = 0;
                var driver = player.AddComponent<PlayerMovementDriver>(); var clock = new Clock();
                driver.Initialize(settings, fake, clock, 100, options.MaximumFallSpeedMmPerSecond, options.GroundContactMm);
                player.SetActive(true); Physics.SyncTransforms();
                for (int frame = 0; frame < 120; frame++) { clock.ElapsedMilliseconds += 16; driver.SendMessage("Update"); }
                var position = player.transform.localPosition;
                Check(position.z > 0.5f && position.z < 2.1f, "controller reaches wall without passing through");
                Check(Mathf.Abs(position.y) < 0.15f, "controller stays on floor");
                driver.SetMovementEnabled(false);
                Check(!fake.Enabled && driver.Motor.ForwardVelocityUmPerSecond == 0, "gate clears input and velocity");
                clock.ElapsedMilliseconds += 5000; driver.SendMessage("Update");
                Check(player.transform.localPosition == position, "disabled movement ignores elapsed time");
                driver.SetMovementEnabled(true); Check(fake.Enabled, "gate re-enables input");
                player.SetActive(false); Check(!fake.Enabled, "disable releases input");
                Object.DestroyImmediate(player); Check(fake.Disposed, "destroy disposes input");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/2.Model/Prefabs/R01/SILENT_Room_01_Final/Prefabs/Room_01_Final.prefab");
                Check(prefab.GetComponentsInChildren<MeshCollider>(true).Any(c => c.name == "Room_01_A_Structure" && c.sharedMesh != null && !c.isTrigger), "R01 structure owns mesh collider");
                Debug.Log($"[Player Movement Checks] {checks} Unity checks passed.");
            }
            finally { Object.DestroyImmediate(root); SceneManager.UnloadSceneAsync(scene); }
        }
    }
}
#endif
