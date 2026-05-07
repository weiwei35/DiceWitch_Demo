using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameEnums
{
    //游戏状态类型
    public enum GameState
    {
        Idle,               // 正常状态
        Drafting,           // 抽卡页开启
        TargetSelection,     // 选人附魔模式
        Map, //地图模式
    }
    
    //房间类型
    public enum RoomType
    {
        Battle,     // 普通战斗
        Elite,      // 精英战斗
        Boss,       // Boss
        Rest,       // 休息点
        Shop,       // 商店
        Treasure,   // 宝箱
        Unknown,     // 用于初始化
        Event
    }
    //角色对象类型
    public enum TargetTeam { Player, Enemy }
    
    //魔法阵槽位属性类型
    public enum SlotAttributeType
    {
        BaseValueAdd,     // 基础点数增加
        // 以后可以加更多：倍率、状态层数等
    }
    
    //骰子属性类型
    public enum DiceActionType { Attack, Defend, Magic, Empty }
    // 棋盘节点效果类型
    public enum BoardNodeType
    {
        Empty,              // 空地（无事发生）
        HpChange,           // ±生命值 (合并了原 Heal 和 Trap)
        ResourceChange,     // ±资源 (替代了原 Treasure)
        RoomEvent,          // 房间主事件
        NextBattleArmor,    // 下次战斗给护甲
        NextBattleFixedDice,// 下次战斗有一枚固定骰子点数
        BlockNextDamage,    // 抵消下一次伤害 (全图+战斗通用)
        NextBattleDamageUp, // 下次战斗伤害增加
        Relic,              // 获得遗物
        Forge               // 骰子锻造
    }
    public enum EnemyTier
    {
        Normal,     // 普通怪物
        Elite,      // 精英怪物
        Boss        // 首领
    }
}
