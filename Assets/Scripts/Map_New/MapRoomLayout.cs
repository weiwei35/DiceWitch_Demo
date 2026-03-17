using UnityEngine;
using System.Collections.Generic;

public class MapRoomLayout : MonoBehaviour
{
    [Header("房间配置")]
    [Tooltip("这个房间的大事件 (战斗、商店等)")]
    public RoomDataSO roomData;

    [Header("节点列表")][Tooltip("按顺序把属于这个房间的节点拖进来")]
    public List<MapNodeAnchor> roomNodes = new List<MapNodeAnchor>();
}