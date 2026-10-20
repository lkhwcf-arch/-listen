#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using EscapeGame.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class RoomCollisionChecks
    {
        [MenuItem("Tools/Game/Verify R01 Collision")]
        public static void Run()
        {
            if (Application.isPlaying) { Debug.LogWarning("Play를 종료한 뒤 R01 충돌 검사를 실행하세요."); return; }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            int checks = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException(label); checks++; }
            bool originalBackfaces = Physics.queriesHitBackfaces;
            try
            {
                var offset = new Vector3(1000, 1000, 1000);
                // 동일 평면의 양쪽 면에서 모델이 관통하지 않아야 합니다. 전역 backface 설정은 변경하지 않습니다.
                for (int winding = 0; winding < 2; winding++)
                {
                    var mesh = new Mesh { vertices = new[] { new Vector3(-5, -5, 0), new Vector3(5, -5, 0), new Vector3(5, 5, 0), new Vector3(-5, 5, 0) },
                        triangles = winding == 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 2, 1, 0, 3, 2, 0 } };
                    var wall = new GameObject("[Temporary] Single Sided Wall"); wall.transform.position = offset + Vector3.forward * .3f;
                    wall.AddComponent<MeshCollider>().sharedMesh = mesh;
                    var owner = new GameObject("[Temporary] View Model"); owner.transform.position = offset;
                    try
                    {
                        var camera = owner.AddComponent<Camera>(); camera.enabled = false; camera.nearClipPlane = .03f;
                        var visual = new GameObject("Visual").transform; visual.SetParent(owner.transform, false); visual.localPosition = new Vector3(.18f, -.2f, .5f);
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(visual, false); cube.transform.localScale = Vector3.one * .18f;
                        Object.DestroyImmediate(cube.GetComponent<Collider>());
                        var renderer = cube.GetComponent<Renderer>();
                        var driver = owner.AddComponent<ViewModelObstructionDriver>(); driver.Initialize(camera, visual, 20);
                        Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                        Check(driver.IsObstructed && !renderer.enabled, "near single-sided surface blocks view model, winding=" + winding);
                        wall.transform.position = offset + Vector3.forward * 2; Physics.SyncTransforms(); driver.SendMessage("LateUpdate");
                        Check(!driver.IsObstructed && renderer.enabled && Mathf.Abs(visual.localPosition.z - .5f) < .00001f, "far wall restores pose, winding=" + winding);
                    }
                    finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(wall); Object.DestroyImmediate(mesh); }
                }
                Check(Physics.queriesHitBackfaces == originalBackfaces, "global backface policy stays unchanged");

                const string basePath = "Assets/2.Model/Prefabs/R01/SILENT_Room_01_Final/Prefabs/Room_01_Final.prefab";
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
                foreach (var selection in new[] {
                    (path: basePath, name: "Door"),
                    (path: "Assets/2.Model/Prefabs/Door.prefab", name: "Door"),
                    (path: "Assets/2.Model/Prefabs/R01/SILENT_Room_01_Final/Prefabs/Room_01_Final 1.prefab", name: "Hemnes.Drawer") })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(selection.path);
                    var filter = prefab.GetComponentsInChildren<MeshFilter>(true).Single(item => item.name == selection.name);
                    var collider = filter.GetComponent<MeshCollider>();
                    Check(collider != null && collider.enabled && !collider.isTrigger && !collider.convex && collider.sharedMesh == filter.sharedMesh,
                        "solid collision matches authored mesh: " + selection.path);
                    // 표시 메시의 가장 얇은 축 바깥에서 접근한 캡슐이 정적 가구/문을 통과하지 않아야 합니다.
                    var solid = new GameObject("[Temporary] Solid"); solid.transform.position = offset;
                    solid.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    var bounds = filter.sharedMesh.bounds;
                    int axis = bounds.size.x <= bounds.size.y && bounds.size.x <= bounds.size.z ? 0 : bounds.size.y <= bounds.size.z ? 1 : 2;
                    var direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
                    var actor = new GameObject("[Temporary] Capsule");
                    try
                    {
                        var center = offset + bounds.center;
                        float half = axis == 0 ? bounds.extents.x : axis == 1 ? bounds.extents.y : bounds.extents.z;
                        actor.transform.position = center - direction * (half + .5f);
                        var cc = actor.AddComponent<CharacterController>(); cc.height = .2f; cc.radius = .1f; cc.center = Vector3.zero; cc.skinWidth = .01f; cc.stepOffset = 0;
                        Physics.SyncTransforms();
                        bool sideHit = false;
                        for (int step = 0; step < 200; step++) sideHit |= (cc.Move(direction * .01f) & CollisionFlags.CollidedSides) != 0;
                        Check(Vector3.Dot(actor.transform.position - center, direction) < half, "capsule cannot cross solid center: " + selection.name);
                        Check(sideHit || axis == 1, "controller reports blocking contact: " + selection.name);
                    }
                    finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(solid); }
                }
                // 실제 R01의 출입구 중심을 피해 양쪽 벽과 앞/뒤 벽을 검사합니다.
                var room = Object.Instantiate(basePrefab); room.transform.position = offset;
                try
                {
                    var spawn = room.GetComponentsInChildren<Transform>(true).Single(item => item.name == "R01_Player");
                    foreach (var direction in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
                    {
                        var actor = new GameObject("[Temporary] R01 Capsule");
                        try
                        {
                            actor.transform.position = new Vector3(spawn.position.x, spawn.position.y, offset.z + 4.5f);
                            var cc = actor.AddComponent<CharacterController>(); cc.height = 1.8f; cc.radius = .3f; cc.center = Vector3.up * .9f; cc.skinWidth = .03f; cc.stepOffset = .3f;
                            Physics.SyncTransforms();
                            for (int step = 0; step < 1200; step++) cc.Move(direction * .01f + Vector3.down * .002f);
                            var local = actor.transform.position - offset;
                            Check(local.x > -8.1f && local.x < .15f && local.z > 2.4f && local.z < 10.6f && local.y > 13,
                                "authored R01 wall retains body, direction=" + direction + " at " + local);
                        }
                        finally { Object.DestroyImmediate(actor); }
                    }
                }
                finally { Object.DestroyImmediate(room); }
                Debug.Log($"[R01 Collision Checks] {checks} native Unity physics checks passed. 플레이 화면 확인은 별도입니다.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
#endif
