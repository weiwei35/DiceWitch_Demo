using System.Collections.Generic;

[System.Serializable]
public class BoardRoom
{
    public int roomId;
    public int regionIndex;
    public RoomDataSO roomDataRef;
    public int startNodeIndex;
    public int endNodeIndex;
    public List<int> nextRoomIds = new List<int>();
}
