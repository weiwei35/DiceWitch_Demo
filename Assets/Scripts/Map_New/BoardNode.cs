using UnityEngine;

[System.Serializable]
public class BoardNode
{
    public int index;               
    public Enum.BoardNodeType type; 
    public int effectValue;         
    public int roomId;
    public RoomDataSO roomDataRef;  
    public int regionIndex;         

    // ==========================================
    // 【新增】标记该节点是否因为跨区跳跃而失效
    // ==========================================
    public bool isInvalidated = false; 

    public BoardNode(int index, int regionIndex)
    {
        this.index = index;
        this.regionIndex = regionIndex;
        this.type = Enum.BoardNodeType.Empty;
        this.isInvalidated = false; // 默认不失效
    }
}