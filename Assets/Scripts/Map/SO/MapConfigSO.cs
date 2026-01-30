using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public struct RoomTypeWeight
{
    public Enum.RoomType type;
    [Range(0, 100)] public int weight; // 权重值，越大越容易随到
}

[System.Serializable]
public class LayerRule
{
    public string note = "第 N 层配置"; // 方便在 Inspector 里看
    
    [Header("Grid Settings")]
    [Range(1, 6)] public int minNodes = 3;
    [Range(1, 6)] public int maxNodes = 4;

    [Header("Room Type Rules")]
    // 如果这个勾上，这层所有房间强制为某类型（比如 Boss 层）
    public bool overrideType = false; 
    public Enum.RoomType fixedType;

    // 如果没勾 override，就按权重随机
    public List<RoomTypeWeight> weights = new List<RoomTypeWeight>();
}

[CreateAssetMenu(menuName = "Map/Advanced Map Config")]
public class MapConfigSO : ScriptableObject
{
    [Header("Global Settings")]
    public int totalLayers = 15;
    [Header("Room Pools (房间池)")]
    // 普通战斗房池子
    public List<BattleRoomSO> battleRoomPool;
    
    // 精英战斗房池子 (如果你还没有 EliteRoomSO，暂时可以用 BattleRoomSO 代替)
    public List<BattleRoomSO> eliteRoomPool; 
    
    // 事件房池子 (需要你之前定义的 EventRoomSO，如果没有就先用 RoomDataSO)
    public List<RoomDataSO> eventRoomPool;
    [Header("Fixed Rooms (固定房间)")]
    public BattleRoomSO bossRoom;     // 最终 Boss
    public RoomDataSO treasureRoom;   // 宝箱房配置 (通用)
    public RoomDataSO shopRoom;       // 商店房配置 (通用)
    public RoomDataSO restRoom;       // 休息房配置 (通用)
    
    [Header("Default Rule (默认规则)")]
    // 如果某一层没有特殊配置，就用这个默认的
    public LayerRule defaultRule;

    [Header("Specific Rules (特殊层规则)")]
    // 索引 0 代表第 1 层，索引 14 代表第 15 层...
    // 我们用 List 来存，Inspector 里可以灵活加减
    public List<LayerRuleEntry> specificLayers = new List<LayerRuleEntry>();
}

[System.Serializable]
public class LayerRuleEntry
{
    public int layerIndex; // 指定针对第几层 (0-based)
    public LayerRule rule;
}