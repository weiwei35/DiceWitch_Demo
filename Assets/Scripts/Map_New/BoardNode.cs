using UnityEngine;

[System.Serializable]
public class BoardNode
{
    public int index;               // 在棋盘上的绝对索引 (第几格)
    public Enum.BoardNodeType type; // 这一格是什么效果
    public int effectValue;         // 效果数值 (比如回血5点，扣血3点)
    
    public int roomId;

    // 如果这个节点是 RoomEvent (房间终点)，这里存着要打什么怪/进什么商店
    public RoomDataSO roomDataRef;  

    // 所属区域 (方便 UI 换背景或者变颜色)
    public int regionIndex;         

    public BoardNode(int index, int regionIndex)
    {
        this.index = index;
        this.regionIndex = regionIndex;
        this.type = Enum.BoardNodeType.Empty;
    }
}