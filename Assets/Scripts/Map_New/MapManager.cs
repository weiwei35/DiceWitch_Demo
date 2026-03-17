using System;
using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;[Header("Config")]
    public BoardMapConfigSO boardConfig;
    
    [Header("Runtime Data")]
    public List<BoardNode> boardNodes = new List<BoardNode>();
    public int currentPlayerNodeIndex = 0; 
    
    // 记录已经通关的房间全局ID
    public HashSet<int> clearedRoomIds = new HashSet<int>();

    public event Action OnMapGenerated; 

    void Awake()
    {
        // 探针日志：只要脚本挂在场景里并激活，这句绝对会打印
        Debug.Log("<color=yellow>【系统】MapManager Awake 执行！</color>");

        if (Instance == null) 
        { 
            Instance = this; 
            DontDestroyOnLoad(gameObject); 
        }
        else 
        { 
            Debug.LogWarning("场景中存在重复的 MapManager，正在销毁...");
            Destroy(gameObject); 
        }
    }

    void Start()
    {
        Debug.Log("<color=yellow>【系统】MapManager Start 执行！准备检查配置...</color>");

        if (boardConfig != null)
        {
            GenerateBoard();
        }
        else
        {
            Debug.LogError("【严重错误】MapManager 的 boardConfig 是空的！请去 Inspector 拖入 BoardMapConfigSO 配置。");
        }
    }

    public void GenerateBoard()
    {
        boardNodes.Clear();
        clearedRoomIds.Clear(); // 重置通关记录
        currentPlayerNodeIndex = 0;
        
        int globalNodeIndex = 0;
        int globalRoomId = 0; // 全局房间ID，用来标记房间是否通关

        // 防空检查
        if (boardConfig.regions == null || boardConfig.regions.Count == 0)
        {
            Debug.LogWarning("BoardConfig 中没有任何 Region 配置！");
            return;
        }

        foreach (var region in boardConfig.regions)
        {
            if (region.regionPrefab == null) continue;
            
            var layout = region.regionPrefab.GetComponent<MapRegionLayout>();
            if (layout == null) 
            {
                Debug.LogError($"预制体 {region.regionPrefab.name} 上没有挂载 MapRegionLayout 脚本！");
                continue;
            }

            // 遍历区域里的所有房间
            foreach (var roomLayout in layout.orderedRooms)
            {
                if (roomLayout == null) continue; // 防空检查

                // 遍历房间里的所有节点
                foreach (var anchor in roomLayout.roomNodes)
                {
                    if (anchor == null) continue; // 防空检查

                    // 获取当前区域的真实索引
                    int regionIndex = boardConfig.regions.IndexOf(region);
                    BoardNode node = new BoardNode(globalNodeIndex, regionIndex); 
                    
                    node.type = anchor.nodeType;
                    node.effectValue = anchor.effectValue;
                    
                    // 记录房间归属
                    node.roomDataRef = roomLayout.roomData;
                    node.roomId = globalRoomId; 

                    boardNodes.Add(node);
                    globalNodeIndex++;
                }
                globalRoomId++; // 切换到下一个房间
            }
        }
        
        Debug.Log($"<color=green>地图数据生成完毕！共 {boardNodes.Count} 个节点，{globalRoomId} 个房间。</color>");
        OnMapGenerated?.Invoke();
    }

    // =================================================================
    // 【核心机制】当玩家的棋子停在一个节点上时调用
    // =================================================================
    public void OnPlayerLanded(BoardNode landedNode)
    {
        Debug.Log($"玩家落在第 {landedNode.index} 格，类型：{landedNode.type}");
        
        // 1. 先生效格子自身的效果
        ProcessNodeEffect(landedNode);

        // 2. 判断这个格子的房间有没有被打过
        if (landedNode.roomDataRef != null && !clearedRoomIds.Contains(landedNode.roomId))
        {
            Debug.Log($"<color=orange>遭遇房间事件！准备进入：{landedNode.roomDataRef.roomName}</color>");
            
            // 标记为已通关 (这样即使这回合打赢了，下回合往前走还在这个房间，也不会再触发了)
            clearedRoomIds.Add(landedNode.roomId);
            
            // 路由：切入战斗/商店
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.EnterRoom(landedNode.roomDataRef);
            }
        }
        else
        {
            Debug.Log("该房间已被清理，或者没有事件，安全停留！");
            // 这里可以通知 UI，允许玩家掷下一次骰子
            // MapDiceThrower.Instance.EnableThrow(); 
        }
    }

    // 处理局部格子效果
    private void ProcessNodeEffect(BoardNode node)
    {
        switch (node.type)
        {
            case Enum.BoardNodeType.Heal:
                if (PlayerManager.Instance != null) PlayerManager.Instance.Heal(node.effectValue);
                Debug.Log($"踩到回血格，恢复 {node.effectValue} HP");
                break;
            case Enum.BoardNodeType.Trap:
                if (PlayerManager.Instance != null) PlayerManager.Instance.TakeDamage(node.effectValue);
                Debug.Log($"踩到陷阱，受到 {node.effectValue} 伤害");
                break;
            case Enum.BoardNodeType.Treasure:
                if (PlayerProgressionManager.Instance != null) PlayerProgressionManager.Instance.AddManaDust(node.effectValue);
                Debug.Log($"捡到宝箱，获得 {node.effectValue} 资源");
                break;
            case Enum.BoardNodeType.Empty:
            case Enum.BoardNodeType.RoomEvent:
                // 无事发生
                break;
        }
    }
}