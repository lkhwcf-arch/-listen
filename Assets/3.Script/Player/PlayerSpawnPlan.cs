using System;
using System.Linq;
using EscapeGame.Data.Tables;
using EscapeGame.World;

namespace EscapeGame.Player
{
    // 시트의 역할·활성·방·시스템 프리팹 ID로 스폰 행을 선택합니다. 좌표를 추측하지 않습니다.
    public sealed class PlayerSpawnPlan
    {
        public string Role
        {
            get;
        }
        public AnchorData Anchor
        {
            get;
        }
        public RoomData Room
        {
            get;
        }
        public PlayerCharacterSettings Character
        {
            get;
        }
        public int EyeHeightMm => Character.EyeHeightMm;
        public int CapsuleHeightMm => Character.CapsuleHeightMm;
        public int CapsuleRadiusMm => Character.CapsuleRadiusMm;
        public int StepOffsetMm => Character.StepOffsetMm;
        public int FovMilliDegrees => Character.FovMilliDegrees;
        public bool UsesCylinder => Role == "B";

        public PlayerSpawnPlan(CsvGameData data, string role, int spawnPrefabId, int characterConfigId)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (spawnPrefabId <= 0 || characterConfigId <= 0)
                throw new ArgumentOutOfRangeException("설정 ID는 양수여야 합니다.");
            var rooms = new RoomLoadPlan(data, role);
            Role = role;
            Room = rooms.First;
            var rows = data.NonInteractAnchors.Where(row => row.Active && row.RoomNo == Room.RoomNo &&
                row.PrefabId == spawnPrefabId && (row.PlayerType == "ALL" || row.PlayerType == role)).ToArray();
            if (rows.Length != 1)
                throw new InvalidOperationException($"플레이어 스폰 행은 역할 {role}, 방 {Room.RoomNo}에 정확히 하나여야 합니다. 현재 {rows.Length}개입니다.");
            Anchor = rows[0];
            Character = PlayerCharacterSettings.Select(data, characterConfigId);
        }
    }
}
