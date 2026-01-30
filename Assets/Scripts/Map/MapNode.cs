using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapNode
{
    // 在网格中的坐标：x是层数(0-14), y是这一层的第几个(0-4)
    public Vector2Int gridPosition; 
    
    public Enum.RoomType roomType;
    public Enum.NodeStatus status;
    
    // 存储具体的房间数据引用 (目前先存 RoomDataSO，以后如果要存盘再改 ID)
    // [System.NonSerialized] 避免 Unity Inspector 递归卡死，但在 Debug 模式可以看到
    public RoomDataSO roomDataRef; 

    // 连线关系：存的是对方的 gridPosition
    public List<Vector2Int> incoming = new List<Vector2Int>();
    public List<Vector2Int> outgoing = new List<Vector2Int>();

    public MapNode(int layerIndex, int nodeIndex)
    {
        gridPosition = new Vector2Int(layerIndex, nodeIndex);
        roomType = Enum.RoomType.Unknown;
        status = Enum.NodeStatus.Locked;
    }
}