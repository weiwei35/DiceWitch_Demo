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
        Vector3? lastPos = null;

        foreach (var room in orderedRooms)
        {
            if (room == null) continue;
            foreach (var node in room.roomNodes)
            {
                if (node == null) continue;
                if (lastPos.HasValue) Gizmos.DrawLine(lastPos.Value, node.transform.position);
                lastPos = node.transform.position;
            }
        }
    }
}