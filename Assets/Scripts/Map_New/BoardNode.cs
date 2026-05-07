using UnityEngine;

[System.Serializable]
public class BoardNode
{
    public int index;               
    public GameEnums.BoardNodeType type;
    public int effectValue;
    public GameEnums.BoardNodeType forgeBonusType = GameEnums.BoardNodeType.Empty; // 仅当 type=Forge 时生效，指定 effectValue 的效果类型         
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
        this.type = GameEnums.BoardNodeType.Empty;
        this.isInvalidated = false; // 默认不失效
    }
}