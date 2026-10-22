using EscapeGame.Interaction;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Audio
{
    public sealed class RoomDoorwayAudio
    {
        private readonly WorldDoorView door;
        private readonly DoorwayCrossing crossing;
        public int FromId { get; }
        public int ToId { get; }
        public RoomDoorwayAudio(WorldDoorView door, int fromId, int toId, float margin)
        { this.door = door; FromId = fromId; ToId = toId; var axis = door.ClosedBoundsInAnchor.size.x < door.ClosedBoundsInAnchor.size.z ? Vector3.right : Vector3.forward;
            float scale = door.DoorAnchor.TransformVector(axis).magnitude;
            crossing = new DoorwayCrossing(margin / Mathf.Max(.0001f, scale)); }
        public void Reset() => crossing.Reset();
        public bool Crossed(Vector3 previous, Vector3 current)
        {
            if (door == null || door.DoorAnchor == null) return false;
            var bounds = door.ClosedBoundsInAnchor;
            var a = door.DoorAnchor.InverseTransformPoint(previous) - bounds.center;
            var b = door.DoorAnchor.InverseTransformPoint(current) - bounds.center;
            bool normalX = bounds.size.x < bounds.size.z;
            return crossing.Tick(normalX ? a.z : a.x, a.y, normalX ? a.x : a.z,
                normalX ? b.z : b.x, b.y, normalX ? b.x : b.z,
                (normalX ? bounds.size.z : bounds.size.x) * .5, bounds.size.y * .5, door.IsOpen);
        }
    }
}
