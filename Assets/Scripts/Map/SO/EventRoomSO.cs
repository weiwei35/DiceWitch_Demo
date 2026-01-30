using UnityEngine;

[CreateAssetMenu(menuName = "Map/Event Room")]
public class EventRoomSO : RoomDataSO
{
    [Header("Event Config")]
    // 这里可以放 LootTable, 或者对话脚本 ScriptableObject
    public int bonusManaDust; 
    public DiceAbilitySO rewardAbility;
    
    public void OnEnable() => roomType = Enum.RoomType.Treasure; // 或 Event
}
