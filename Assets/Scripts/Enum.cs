using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Enum
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
    public enum NodeStatus
    {
        Locked,     // 不可达
        Available,  // 可点击
        Visited,    // 已访问（未完成）
        Completed   // 已通过
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
}
