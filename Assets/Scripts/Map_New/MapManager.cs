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
                    node.forgeBonusType = anchor.forgeBonusType;
                    
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
    public void OnPlayerLanded(BoardNode landedNode, Vector3 pawnPos)
    {
        Debug.Log($"玩家落在第 {landedNode.index} 格，类型：{landedNode.type}");

        // 1. 先生效格子自身的效果，并在棋子头上飘字
        ProcessNodeEffect(landedNode, pawnPos);

        // 2. 如果是锻造节点，先进入锻造流程，锻造结束后再进房间
        if (landedNode.type == GameEnums.BoardNodeType.Forge)
        {
            GameFlowController.Instance.StartForgeProcess(() =>
            {
                StartCoroutine(DelayEnterRoom(landedNode));
            });
        }
        else
        {
            StartCoroutine(DelayEnterRoom(landedNode));
        }
    }

    // 【新增】延迟进入房间的协程
    private System.Collections.IEnumerator DelayEnterRoom(BoardNode landedNode)
    {
        // 如果有实际效果，多等一会 (1秒)；如果是空地或者直接进事件的格子，少等一会 (0.3秒缓冲)
        float delayTime = (landedNode.type == GameEnums.BoardNodeType.Empty || landedNode.type == GameEnums.BoardNodeType.RoomEvent) ? 0.3f : 2.0f;
        
        yield return new WaitForSeconds(delayTime);

        // --- 以下是你原来的进入房间逻辑 ---
        if (landedNode.roomDataRef != null && !clearedRoomIds.Contains(landedNode.roomId))
        {
            clearedRoomIds.Add(landedNode.roomId);

            if (landedNode.roomDataRef.skipRemainingNodesOnClear)
            {
                for (int i = landedNode.index + 1; i < boardNodes.Count; i++)
                {
                    if (boardNodes[i].roomId == landedNode.roomId) boardNodes[i].isInvalidated = true; 
                    else break; 
                }
                MapViewController.Instance?.UpdateNodeStates(landedNode.index);
            }
            
            if (GameFlowController.Instance != null)
            {
                GameFlowController.Instance.EnterRoom(landedNode.roomDataRef);
            }
        }
    }

    // 【修改】处理效果并呼叫飘字
    private void ProcessNodeEffect(BoardNode node, Vector3 pawnPos)
    {
        string floatText = "";
        Color floatColor = Color.white;

        switch (node.type)
        {
            case GameEnums.BoardNodeType.HpChange:
                if (node.effectValue > 0) 
                {
                    PlayerManager.Instance.Heal(node.effectValue);
                    floatText = $"+{node.effectValue} HP";
                    floatColor = Color.green;
                }
                else if (node.effectValue < 0) 
                {
                    PlayerManager.Instance.TakeDamage(Mathf.Abs(node.effectValue));
                    floatText = $"{node.effectValue} HP"; // 负数自带减号
                    floatColor = Color.red;
                }
                break;
                
            case GameEnums.BoardNodeType.ResourceChange:
                if (node.effectValue > 0) 
                {
                    ResourceManager.Instance.AddManaDust(node.effectValue);
                    floatText = $"+{node.effectValue} 粉尘";
                    floatColor = new Color(1f, 0.8f, 0f); // 黄色
                }
                else if (node.effectValue < 0) 
                {
                    ResourceManager.Instance.TrySpendManaDust(Mathf.Abs(node.effectValue));
                    floatText = $"{node.effectValue} 粉尘";
                    floatColor = Color.red;
                }
                break;
                
            case GameEnums.BoardNodeType.NextBattleArmor:
                PlayerManager.Instance.nextBattleArmorBonus += node.effectValue;
                floatText = $"开局护甲 +{node.effectValue}";
                floatColor = new Color(0.2f, 0.6f, 1f); // 蓝色
                break;
                
            case GameEnums.BoardNodeType.NextBattleFixedDice:
                PlayerManager.Instance.nextBattleFixedDiceValue = node.effectValue;
                floatText = $"必定掷出 {node.effectValue}";
                floatColor = new Color(0.8f, 0.2f, 1f); // 紫色
                break;
                
            case GameEnums.BoardNodeType.BlockNextDamage:
                PlayerManager.Instance.hasBlockNextDamageShield = true;
                PlayerManager.Instance.UpdateUI();
                floatText = "获得圣盾";
                floatColor = Color.cyan;
                break;
                
            case GameEnums.BoardNodeType.NextBattleDamageUp:
                PlayerManager.Instance.nextBattleDamageBonus += node.effectValue;
                floatText = $"伤害 +{node.effectValue}";
                floatColor = new Color(1f, 0.5f, 0f); // 橙色
                break;
                
            case GameEnums.BoardNodeType.Relic:
                floatText = "获得遗物";
                floatColor = Color.yellow;
                break;

            case GameEnums.BoardNodeType.Forge:
                // 锻造节点的额外加成：根据 forgeBonusType 判定
                switch (node.forgeBonusType)
                {
                    case GameEnums.BoardNodeType.HpChange:
                        if (node.effectValue > 0)
                        {
                            PlayerManager.Instance.Heal(node.effectValue);
                            floatText = $"+{node.effectValue} HP";
                            floatColor = Color.green;
                        }
                        else if (node.effectValue < 0)
                        {
                            PlayerManager.Instance.TakeDamage(Mathf.Abs(node.effectValue));
                            floatText = $"{node.effectValue} HP";
                            floatColor = Color.red;
                        }
                        break;
                    case GameEnums.BoardNodeType.ResourceChange:
                        if (node.effectValue > 0)
                        {
                            ResourceManager.Instance.AddManaDust(node.effectValue);
                            floatText = $"+{node.effectValue} 粉尘";
                            floatColor = new Color(1f, 0.8f, 0f);
                        }
                        else if (node.effectValue < 0)
                        {
                            ResourceManager.Instance.TrySpendManaDust(Mathf.Abs(node.effectValue));
                            floatText = $"{node.effectValue} 粉尘";
                            floatColor = Color.red;
                        }
                        break;
                    case GameEnums.BoardNodeType.NextBattleArmor:
                        PlayerManager.Instance.nextBattleArmorBonus += node.effectValue;
                        floatText = $"开局护甲 +{node.effectValue}";
                        floatColor = new Color(0.2f, 0.6f, 1f);
                        break;
                    case GameEnums.BoardNodeType.NextBattleFixedDice:
                        PlayerManager.Instance.nextBattleFixedDiceValue = node.effectValue;
                        floatText = $"必定掷出 {node.effectValue}";
                        floatColor = new Color(0.8f, 0.2f, 1f);
                        break;
                    case GameEnums.BoardNodeType.BlockNextDamage:
                        PlayerManager.Instance.hasBlockNextDamageShield = true;
                        PlayerManager.Instance.UpdateUI();
                        floatText = "获得圣盾";
                        floatColor = Color.cyan;
                        break;
                    case GameEnums.BoardNodeType.NextBattleDamageUp:
                        PlayerManager.Instance.nextBattleDamageBonus += node.effectValue;
                        floatText = $"伤害 +{node.effectValue}";
                        floatColor = new Color(1f, 0.5f, 0f);
                        break;
                }
                break;
        }

        // 呼叫飘字管理器
        if (!string.IsNullOrEmpty(floatText) && FloatingTextManager.Instance != null)
        {
            FloatingTextManager.Instance.ShowText(pawnPos, floatText, floatColor);
        }
    }
    //找下一个房间的起点
    public int GetNextRoomStartIndex(int currentRoomId)
    {
        // 遍历整个地图节点列表
        for (int i = 0; i < boardNodes.Count; i++)
        {
            // 找到第一个归属于不同/更大 Room ID 的节点
            if (boardNodes[i].roomId > currentRoomId)
            {
                return i; // 返回这个节点的绝对索引
            }
        }
        
        // 如果返回 -1，说明这已经是整个大地图的最后一个房间了，没地方可跳了
        return -1; 
    }
}