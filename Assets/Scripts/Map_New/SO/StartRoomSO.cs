using UnityEngine;

[CreateAssetMenu(fileName = "New Start Room", menuName = "Map/Room/Start Room")]
public class StartRoomSO : RoomDataSO
{
    protected override GameEnums.RoomType FixedRoomType => GameEnums.RoomType.Start;
}
