using UnityEngine;
using System.Collections.Generic;

public class MapRegionLayout : MonoBehaviour
{
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

            DrawRoomInternalLines(room);

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

    private void DrawRoomInternalLines(MapRoomLayout room)
    {
        MapNodeAnchor lastNode = null;

        foreach (MapNodeAnchor node in room.roomNodes)
        {
            if (node == null) continue;

            if (lastNode != null)
                Gizmos.DrawLine(lastNode.transform.position, node.transform.position);

            lastNode = node;
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
