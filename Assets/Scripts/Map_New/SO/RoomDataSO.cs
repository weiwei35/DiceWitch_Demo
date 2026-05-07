using UnityEngine;

// 这是一个抽象基类，所有具体房间都继承它
public abstract class RoomDataSO : ScriptableObject
{
    public string roomName;
    public GameEnums.RoomType roomType;
    public Sprite roomIcon; // 地图上显示的图标
    [TextArea] public string description; // 鼠标悬停时的描述

    // ==========================================
    // 【新增】跨区跳跃配置
    // ==========================================
    [Header("地图规则")][Tooltip("如果为 true，打通此房间后，下一次掷骰子的第 1 步将直接飞跃到下一个房间的起点。")]
    public bool skipRemainingNodesOnClear = false; 
}