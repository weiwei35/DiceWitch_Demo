using UnityEngine;

[CreateAssetMenu(menuName = "Map/Battle Room")]
public class BattleRoomSO : RoomDataSO
{
    [Header("战斗房类型")]
    [Tooltip("普通怪、精英怪、Boss 都使用 BattleRoomSO；地图表现和房间流程会根据这里区分。")]
    public GameEnums.EnemyTier battleTier = GameEnums.EnemyTier.Normal;

    protected override GameEnums.RoomType FixedRoomType
    {
        get
        {
            switch (battleTier)
            {
                case GameEnums.EnemyTier.Elite:
                    return GameEnums.RoomType.Elite;
                case GameEnums.EnemyTier.Boss:
                    return GameEnums.RoomType.Boss;
                case GameEnums.EnemyTier.Normal:
                default:
                    return GameEnums.RoomType.Battle;
            }
        }
    }

    [Header("Battle Config")]
    // 这个房间里会有什么怪？
    // 这里可以直接引用我们之前做好的 WaveDataSO
    public WaveDataSO enemyWave; 
    
    [Header("奖励配置")]
    [Tooltip("如果勾选，打通此房间后会弹出骰子附魔三选一奖励")]
    public bool rewardAbilityDraft = false; 
    
    // 以后可以加：背景图 prefab、特殊环境效果等
    // public GameObject environmentPrefab; 

}
