using System;
using System.Collections.Generic;
using EscapeGame.Data;
using EscapeGame.Data.Tables;
using UnityEngine;
using UnityEngine.Rendering;

namespace EscapeGame.World
{
    // 역할별 베이크 데이터와 씬 전역 프로브의 소유권을 연결하는 Unity 어댑터입니다.
    public sealed class RoomProbeLightingDriver : MonoBehaviour
    {
        private LightProbes previous, owned;
        private IngameWorldDriver world;
        private readonly List<(Light light, bool enabled)> directional = new List<(Light, bool)>();
        public bool IsApplied => owned != null && ReferenceEquals(LightmapSettings.lightProbes, owned);
        public void Initialize(string role, RoomProbeLightingSettings settings)
        {
            if (owned != null)
                throw new InvalidOperationException("방 프로브는 한 번만 연결합니다.");
            var source = settings.ForRole(role);
            if (source == null || source.count == 0)
            {
                Debug.LogWarning("[방 조명] 현재 역할의 베이크 데이터가 없습니다. 조명 프로브 메뉴에서 베이크·저장하세요.");
                return;
            }
            previous = LightmapSettings.lightProbes;
            owned = Instantiate(source);
            owned.name = "실행용 방 조명 프로브 " + role;
            LightmapSettings.lightProbes = owned;
            LightProbes.Tetrahedralize();
            if (settings.DisableSceneDirectionalLights)
                foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (light.gameObject.scene == gameObject.scene && light.type == LightType.Directional)
                    {
                        directional.Add((light, light.enabled));
                        light.enabled = false;
                    }
            Debug.Log($"[방 조명] {role} 역할 프로브 {owned.count}개 적용");
        }
        public void RegisterRoom(RoomSceneView view)
        {
            if (IsApplied && view != null)
                ApplyReceivers(view.gameObject);
        }
        public void RegisterPlayer(GameObject player)
        {
            if (IsApplied && player != null)
                ApplyReceivers(player);
        }
        private static void ApplyReceivers(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                if (renderer is MeshRenderer mesh)
                    mesh.receiveGI = ReceiveGI.LightProbes;
            }
        }
        public void BindWorld(IngameWorldDriver value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (ReferenceEquals(world, value))
                return;
            if (!ReferenceEquals(world, null))
                throw new InvalidOperationException("프로브의 월드 소유자는 변경할 수 없습니다.");
            world = value;
            world.RoomLoaded += OnRoomLoaded;
            foreach (var room in world.LoadedRooms.Values)
                RegisterRoom(room);
        }
        private void OnRoomLoaded(RoomData row, RoomSceneView view) => RegisterRoom(view);
        private void OnDestroy()
        {
            if (world != null)
                world.RoomLoaded -= OnRoomLoaded;
            bool ownsGlobal = owned != null && ReferenceEquals(LightmapSettings.lightProbes, owned);
            if (ownsGlobal)
            {
                LightmapSettings.lightProbes = previous;
                foreach (var item in directional)
                    if (item.light != null)
                        item.light.enabled = item.enabled;
            }
            if (owned != null)
                Destroy(owned);
            directional.Clear();
        }
    }
}
