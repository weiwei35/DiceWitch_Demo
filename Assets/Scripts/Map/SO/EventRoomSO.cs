using UnityEngine;
using System.Collections.Generic;[CreateAssetMenu(fileName = "New Event Room", menuName = "Map/Room/Event Room")]
public class EventRoomSO : RoomDataSO
{
    protected override GameEnums.RoomType FixedRoomType => GameEnums.RoomType.Event;

    [Header("Event Config")]
    public int bonusManaDust; 
    public DiceAbilitySO rewardAbility;

    [Header("事件池配置")][Tooltip("玩家进入此房间时，系统会从以下列表中随机抽取一个事件执行")]
    public List<RandomEventSO> possibleEvents = new List<RandomEventSO>();
    
}
