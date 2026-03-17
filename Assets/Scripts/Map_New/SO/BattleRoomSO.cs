using UnityEngine;

[CreateAssetMenu(menuName = "Map/Battle Room")]
public class BattleRoomSO : RoomDataSO
{
    [Header("Battle Config")]
    // 这个房间里会有什么怪？
    // 这里可以直接引用我们之前做好的 WaveDataSO
    public WaveDataSO enemyWave; 
    
    // 以后可以加：背景图 prefab、特殊环境效果等
    // public GameObject environmentPrefab; 

    public void OnEnable()
    {
        roomType = Enum.RoomType.Battle;
    }
}