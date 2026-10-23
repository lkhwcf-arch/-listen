using EscapeGame.Interaction;
using EscapeGame.World;
using UnityEngine;

namespace EscapeGame.Audio
{
    public sealed class RoomDoorwayAudio
    {
        private readonly WorldDoorView door;
        private readonly DoorwayCrossingFrame crossing;
        public int FromId { get; }
        public int ToId { get; }
        public RoomDoorwayAudio(WorldDoorView door, int fromId, int toId, float margin)
        {
            this.door = door; FromId = fromId; ToId = toId;
            crossing = new DoorwayCrossingFrame(door.DoorAnchor.localToWorldMatrix, door.ClosedBoundsInAnchor, margin);
        }
        public void Reset() => crossing.Reset();
        public bool Crossed(Vector3 previous, Vector3 current)
        {
            if (door == null || door.DoorAnchor == null) return false;
            return crossing.Crossed(previous, current, door.IsOpen);
        }
    }
}
