using UnityEngine;
using System.Collections.Generic;

public class MapRegionLayout : MonoBehaviour
{
    [Header("地图底层")]
    [Tooltip("常驻显示的基础格纹根节点。运行时会被放在路线和房间节点下方。")]
    public GameObject baseGridRoot;
    [Tooltip("只在旧节点和失效分支周围显示的格纹覆盖层。")]
    public MapGridRevealLayer passedGridRevealLayer;

    [Header("路线设置")]
    [Tooltip("按前进顺序，把房间拖进来")]
    public List<MapRoomLayout> orderedRooms = new List<MapRoomLayout>();
    
    // 画绿线辅助你拼图
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        for (int i = 0; i < orderedRooms.Count; i++)
        {
            MapRoomLayout room = orderedRooms[i];
            if (room == null) continue;

            MapNodeAnchor roomEndNode = GetLastNode(room);
            if (roomEndNode == null) continue;

            if (room.nextRooms != null && room.nextRooms.Count > 0)
            {
                foreach (MapRoomLayout nextRoom in room.nextRooms)
                    DrawRoomExitLine(roomEndNode, nextRoom);
            }
            else if (i + 1 < orderedRooms.Count)
            {
                DrawRoomExitLine(roomEndNode, orderedRooms[i + 1]);
            }
        }
    }

    private void DrawRoomExitLine(MapNodeAnchor roomEndNode, MapRoomLayout nextRoom)
    {
        MapNodeAnchor nextStartNode = GetFirstNode(nextRoom);
        if (nextStartNode == null) return;

        Gizmos.DrawLine(roomEndNode.transform.position, nextStartNode.transform.position);
    }

    private MapNodeAnchor GetFirstNode(MapRoomLayout room)
    {
        if (room == null || room.roomNodes == null) return null;

        foreach (MapNodeAnchor node in room.roomNodes)
        {
            if (node != null)
                return node;
        }

        return null;
    }

    private MapNodeAnchor GetLastNode(MapRoomLayout room)
    {
        if (room == null || room.roomNodes == null) return null;

        for (int i = room.roomNodes.Count - 1; i >= 0; i--)
        {
            if (room.roomNodes[i] != null)
                return room.roomNodes[i];
        }

        return null;
    }
}
