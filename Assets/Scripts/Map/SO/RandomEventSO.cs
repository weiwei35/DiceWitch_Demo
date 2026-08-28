using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Random Event", menuName = "Map/Random Event")]
public class RandomEventSO : ScriptableObject
{
    public string eventName;[Tooltip("事件的页面节点，List 索引 0 默认是起始页")]
    public List<EventPage> pages = new List<EventPage>();
}

[System.Serializable]
public class EventPage
{[Tooltip("当前页面的背景图")]
    public Sprite backgroundImage;
    
    [TextArea(3, 5)][Tooltip("当前页面的剧情描述文字")]
    public string description;[Tooltip("玩家在当前页面的可用选项")]
    public List<EventChoice> choices = new List<EventChoice>();
}[System.Serializable]
public class EventChoice
{
    public string choiceText;[Header("结果结算 (正加负扣)")]
    public int hpChange;      // 正数回血，负数扣血
    public int goldChange;    // 正数加钱，负数扣钱
    // 这里可以随时扩展你的资源，比如 manaDustChange 等

    [Header("下一步路由")][Tooltip("点击后跳转到的 Page 索引（即 pages 列表的下标）。填 -1 表示结束事件并返回大地图。")]
    public int targetPageId = -1;[Tooltip("如果这个选项会直接触发战斗，将战斗房的配置拖入此处。如果不为空，则无视 targetPageId 切入战斗！")]
    public RoomDataSO battleToTrigger; // 依赖你现有的房间基类
}