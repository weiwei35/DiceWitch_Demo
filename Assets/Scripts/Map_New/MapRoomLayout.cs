using UnityEngine;
using System.Collections.Generic;

public class MapRoomLayout : MonoBehaviour
{
    [Header("房间配置")]
    [Tooltip("这个房间的大事件 (战斗、商店等)")]
    public RoomDataSO roomData;

    [Header("节点列表")][Tooltip("按顺序把属于这个房间的节点拖进来")]
    public List<MapNodeAnchor> roomNodes = new List<MapNodeAnchor>();

    [Header("房间出口")]
    [Tooltip("这个房间走完后可以进入的后继房间。留空时默认连接 orderedRooms 中的下一个房间。")]
    public List<MapRoomLayout> nextRooms = new List<MapRoomLayout>();
}
