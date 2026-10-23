#if UNITY_EDITOR
using System;
using EscapeGame.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EscapeGame.Editor
{
    public static class RoomConnectionAlignmentChecks
    {
        [MenuItem("Tools/Game/Verify Room Connections")]
        public static void Run()
        {
            var owner = new GameObject("[Temporary] Room Alignment Checks") { hideFlags = HideFlags.HideAndDontSave };
            int count = 0;
            void Check(bool condition, string message)
            {
                if (!condition)
                    throw new InvalidOperationException(message);
                count++;
            }
            try
            {
                var current = New("Current", owner.transform);
                var exit = New("R01_DoorPoint", current);
                exit.localPosition = new Vector3(3, 0, 7);
                foreach (int yaw in new[] { 0, 90, 180, 270 })
                {
                    current.SetPositionAndRotation(new Vector3(11, 2, -9), Quaternion.Euler(0, yaw, 0));
                    var next = New("Next", owner.transform);
                    next.SetPositionAndRotation(new Vector3(-15, 3, 4), Quaternion.Euler(0, 37, 0));
                    var group = New("Nested", next);
                    group.localPosition = new Vector3(1, 0, 2);
                    group.localRotation = Quaternion.Euler(0, 24, 0);
                    var entry = New("R02_EntryPoint", group);
                    entry.localPosition = new Vector3(2, 0, -5);
                    entry.localRotation = Quaternion.Euler(0, 17, 0);
                    Vector3 previousPosition = current.position;
                    RoomConnectionAlignment.Align(next, RoomConnectionAlignment.FindEntry(next, 2), RoomConnectionAlignment.FindExit(current, 1));
                    Check(Vector3.Distance(entry.position, exit.position) < 0.0001f, "entry/exit positions must coincide");
                    Check(Vector3.Dot(entry.forward, exit.forward) < -0.9999f, "outward directions must oppose");
                    Check(Vector3.Dot(entry.up, exit.up) > 0.9999f, "floor up directions must match");
                    Check(next.localScale == Vector3.one && current.position == previousPosition, "keep previous room and scale");
                    var first = new Pose(next.position, next.rotation);
                    RoomConnectionAlignment.Align(next, entry, exit);
                    Check(Vector3.Distance(first.position, next.position) < 0.0001f && Quaternion.Angle(first.rotation, next.rotation) < 0.01f, "alignment must be repeatable");
                    Object.DestroyImmediate(next.gameObject);
                }
                bool missing = false;
                try
                {
                    RoomConnectionAlignment.FindEntry(current, 2);
                }
                catch (InvalidOperationException)
                {
                    missing = true;
                }
                Check(missing, "missing entry must be rejected");
                New("R01_DoorPoint", current);
                bool duplicate = false;
                try
                {
                    RoomConnectionAlignment.FindExit(current, 1);
                }
                catch (InvalidOperationException)
                {
                    duplicate = true;
                }
                Check(duplicate, "ambiguous exit must be rejected");
                var invalid = New("Invalid next", owner.transform);
                invalid.localScale = new Vector3(1, 2, 1);
                var invalidEntry = New("R02_EntryPoint", invalid);
                var before = new Pose(invalid.position, invalid.rotation);
                bool invalidScale = false;
                try
                {
                    RoomConnectionAlignment.Align(invalid, invalidEntry, exit);
                }
                catch (InvalidOperationException)
                {
                    invalidScale = true;
                }
                Check(invalidScale && invalid.position == before.position && invalid.rotation == before.rotation, "invalid scale rejected before placement");
                Debug.Log($"[Room Connection Checks] {count} checks passed.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static Transform New(string name, Transform parent)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }
    }
}
#endif
