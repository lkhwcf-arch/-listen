using System;
using EscapeGame.Data;
using EscapeGame.Data.Tables;

namespace EscapeGame.World
{
    public enum RoomPlacementMode { Authored = 0, ConnectionPoints = 1 }

    // 실제 게임·로컬·에디터가 같은 배치 정책과 오브젝트 조립 경로를 사용합니다.
    public static class RoomSceneAssembler
    {
        public static void Prepare(RoomSceneView view, RoomData row, RoomLoadPlan plan, CsvGameData data,
            GameContentAssets assets, System.Collections.Generic.IReadOnlyCollection<int> external, RoomSceneView previous)
        {
            if (assets.PlacementMode == RoomPlacementMode.ConnectionPoints && row.Id != plan.First.Id)
            {
                if (previous == null) throw new InvalidOperationException("연결점 배치에는 이전 방이 먼저 준비되어야 합니다.");
                RoomConnectionAlignment.Align(view.transform, RoomConnectionAlignment.FindEntry(view.transform, row.RoomNo),
                    RoomConnectionAlignment.FindExit(previous.transform, previous.RoomNo));
            }
            // Authored는 제작자가 저장한 최종 좌표를 유지합니다. 베이크한 배치가 바뀌지 않습니다.
            view.Load(row, plan, data, assets, external);
        }
    }
}
