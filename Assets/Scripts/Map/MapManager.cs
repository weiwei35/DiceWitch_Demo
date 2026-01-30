using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random; // 必须引用这个，用于方便的列表查询

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [Header("Config")]
    public MapConfigSO mapConfig;

    [Header("Runtime Data")]
    // 我们用 List 存储所有节点，查询时遍历即可
    public List<MapNode> mapNodes = new List<MapNode>();
    public MapNode currentNode; // 玩家当前在哪

    public event Action OnMapGenerated; 
    public event Action OnRoomLoaded; 
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start()
    {
        // 测试用：如果配置存在，游戏一开始就生成一个地图看看
        if (mapConfig != null)
        {
            GenerateMap();
        }
    }
    private LayerRule GetRuleForLayer(int layerIndex)
    {
        // 1. 先看有没有针对这一层的特殊配置
        var entry = mapConfig.specificLayers.Find(x => x.layerIndex == layerIndex);
        if (entry != null) return entry.rule;
    
        // 2. 没有就用默认的
        return mapConfig.defaultRule;
    }
    public void GenerateMap()
    {
        // 1. 安全检查
        if (mapConfig == null) { Debug.LogError("MapConfig 未赋值！"); return; }
    
        // 2. 清理旧数据
        mapNodes.Clear();
        currentNode = null;
        List<List<MapNode>> tempLayeredMap = new List<List<MapNode>>();
    
        // ---------------------------------------------------------
        // 第一阶段：生成节点 (保险 1：Mathf.Max)
        // ---------------------------------------------------------
        for (int i = 0; i < mapConfig.totalLayers; i++)
        {
            LayerRule rule = GetRuleForLayer(i);
            List<MapNode> currentLayerNodes = new List<MapNode>();
            
            // 【核心修改】即使配置写错成0，这里也强制至少生成 1 个
            int rawCount = UnityEngine.Random.Range(rule.minNodes, rule.maxNodes + 1);
            int nodeCount = Mathf.Max(1, rawCount); 
    
            for (int j = 0; j < nodeCount; j++)
            {
                MapNode node = new MapNode(i, j);
                currentLayerNodes.Add(node);
                mapNodes.Add(node);
            }
            tempLayeredMap.Add(currentLayerNodes);
        }
    
        // ---------------------------------------------------------
        // 第二阶段：连线逻辑 (含 Boss 层汇聚特例)
        // ---------------------------------------------------------
        for (int i = 0; i < mapConfig.totalLayers - 1; i++)
        {
            var currentLayer = tempLayeredMap[i];
            var nextLayer = tempLayeredMap[i + 1];
    
            // =========================================================
            // 【新增规则】汇聚检查 (Boss层特例)
            // 如果下一层只有一个节点，或者下一层节点数少于当前层
            // 我们必须允许合并，否则就会出现断头路
            // =========================================================
            if (nextLayer.Count == 1)
            {
                // 简单粗暴：所有人一起连向那个唯一的节点
                foreach (var node in currentLayer)
                {
                    CreateConnection(node, nextLayer[0]);
                }
                // 这一层处理完了，直接进入下一层循环
                continue; 
            }
    
            // =========================================================
            // 标准规则：切蛋糕算法 (Strict Lanes)
            // 适用于 Next >= Current 的情况，保持平行不交叉
            // =========================================================
            for (int j = 0; j < currentLayer.Count; j++)
            {
                MapNode currentNode = currentLayer[j];
    
                // 计算分配区间
                int rangeStart = Mathf.FloorToInt((float)j * nextLayer.Count / currentLayer.Count);
                int rangeEnd = Mathf.FloorToInt((float)(j + 1) * nextLayer.Count / currentLayer.Count);
    
                // 【安全修正】防止因为精度问题导致区间为空
                // 如果算出空区间，强行借用 rangeStart 那个位置
                if (rangeStart == rangeEnd) 
                {
                    // 确保不越界
                    if (rangeStart >= nextLayer.Count) rangeStart = nextLayer.Count - 1;
                    rangeEnd = rangeStart + 1;
                }
    
                // 1. 必连逻辑 (连区间里的第一个)
                if (rangeStart < nextLayer.Count)
                {
                    CreateConnection(currentNode, nextLayer[rangeStart]);
                }
    
                // 2. 随机分支逻辑 (连区间里剩下的)
                for (int k = rangeStart + 1; k < rangeEnd; k++)
                {
                    if (UnityEngine.Random.value < 0.5f) // 50% 概率多连一个
                    {
                        CreateConnection(currentNode, nextLayer[k]);
                    }
                    // 补漏：如果这个节点没人连，强制连上
                    else if (nextLayer[k].incoming.Count == 0)
                    {
                        CreateConnection(currentNode, nextLayer[k]);
                    }
                }
            }
            
            // =========================================================
            // 【最后补漏】防止计算误差导致下一层有孤儿
            // =========================================================
            foreach (var nextNode in nextLayer)
            {
                if (nextNode.incoming.Count == 0)
                {
                    // 找上一层理应负责它的那个父亲（按比例找）
                    int bestParentIndex = Mathf.FloorToInt((float)nextNode.gridPosition.y * currentLayer.Count / nextLayer.Count);
                    bestParentIndex = Mathf.Clamp(bestParentIndex, 0, currentLayer.Count - 1);
                    CreateConnection(currentLayer[bestParentIndex], nextNode);
                }
            }
        }
    
        AssignRoomTypes();
        AssignRoomDataToNodes();
        
        // =========================================================
        // 【新增】解锁第一层 (Layer 0)
        // =========================================================
        foreach (var node in mapNodes)
        {
            if (node.gridPosition.x == 0)
            {
                node.status = Enum.NodeStatus.Available;
            }
            else
            {
                // 保险起见，其他层确保是锁定的
                node.status = Enum.NodeStatus.Locked;
            }
        }
        
        Debug.Log($"<color=green>地图生成完毕！共 {mapConfig.totalLayers} 层，{mapNodes.Count} 个节点。</color>");
        
        // 通知 UI
        OnMapGenerated?.Invoke();
    }

    private void CreateConnection(MapNode from, MapNode to)
    {
        // 避免重复连线
        if (!from.outgoing.Contains(to.gridPosition))
        {
            from.outgoing.Add(to.gridPosition);
            to.incoming.Add(from.gridPosition);
        }
    }

    private void AssignRoomTypes()
    {
        foreach (var node in mapNodes)
        {
            int layerIndex = node.gridPosition.x;
            LayerRule rule = GetRuleForLayer(layerIndex);

            // A. 强制类型 (Override)
            if (rule.overrideType)
            {
                node.roomType = rule.fixedType;
            }
            // B. 权重随机
            else
            {
                node.roomType = GetRandomRoomType(rule.weights);
            }
        }
    }
    private void AssignRoomDataToNodes()
    {
        foreach (var node in mapNodes)
        {
            // 如果配置里用了 Override 强制指定了 RoomData (比如 Boss 层)，就跳过随机
            // (目前的 MapNode 结构里还没存 override 的 data，所以我们全量生成)
        
            switch (node.roomType)
            {
                case Enum.RoomType.Battle:
                    node.roomDataRef = GetRandomItem(mapConfig.battleRoomPool);
                    break;

                case Enum.RoomType.Elite:
                    node.roomDataRef = GetRandomItem(mapConfig.eliteRoomPool);
                    break;

                case Enum.RoomType.Boss:
                    node.roomDataRef = mapConfig.bossRoom;
                    break;

                case Enum.RoomType.Event:
                    node.roomDataRef = GetRandomItem(mapConfig.eventRoomPool);
                    break;

                case Enum.RoomType.Shop:
                    node.roomDataRef = mapConfig.shopRoom;
                    break;

                case Enum.RoomType.Treasure:
                    node.roomDataRef = mapConfig.treasureRoom;
                    break;
                
                case Enum.RoomType.Rest:
                    node.roomDataRef = mapConfig.restRoom;
                    break;
            }

            // 这里的 Log 可以帮你检查是不是每个节点都有了数据
            // if (node.roomDataRef == null) Debug.LogWarning($"节点 {node.gridPosition} 的 RoomData 为空！类型: {node.roomType}");
        }
    }
    public void EnterNode(MapNode targetNode)
    {
        // --- A. 验证合法性 ---
        // 如果节点被锁定，或者不是当前节点的邻居，就不能进
        if (targetNode.status != Enum.NodeStatus.Available)
        {
            Debug.LogWarning("该节点当前不可进入！");
            return;
        }

        // --- B. 更新地图状态 ---
        // 1. 把上一个节点标记为 Completed (如果是第一层就没有上一个)
        if (currentNode != null)
        {
            currentNode.status = Enum.NodeStatus.Completed;
        }

        // 2. 更新当前节点
        currentNode = targetNode;
        currentNode.status = Enum.NodeStatus.Visited; // 标记为"正在其中"

        // 3. 锁定其他同层节点 (Roguelike通常进了这一层的一个，其他的就废弃了)
        LockOtherNodesInLayer(targetNode.gridPosition.x);

        // 4. 通知 UI 刷新 (让旧节点变灰，新节点变亮)
        OnMapGenerated?.Invoke(); // 这里可以复用刷新事件，或者专门写一个 OnMapStateChanged

        // --- C. 执行跳转逻辑 ---
        ProcessRoomLogic(targetNode);
    }

    private void ProcessRoomLogic(MapNode node)
    {
        Debug.Log($"进入房间：{node.roomType} | 数据：{(node.roomDataRef ? node.roomDataRef.name : "null")}");

        switch (node.roomType)
        {
            case Enum.RoomType.Battle:
            case Enum.RoomType.Elite:
            case Enum.RoomType.Boss:
                // 以前是 LoadScene，现在直接切状态
                var battleData = node.roomDataRef as BattleRoomSO;
                GameFlowController.Instance.EnterBattleState(battleData);
                break;

            case Enum.RoomType.Shop:
            case Enum.RoomType.Rest:
            case Enum.RoomType.Treasure:
            case Enum.RoomType.Event:
                GameFlowController.Instance.EnterNonBattleState(node.roomDataRef, node.roomType);
                break;
        }
        OnRoomLoaded?.Invoke();
    }
    private void LockOtherNodesInLayer(int layerIndex)
    {
        // 简单的逻辑：把同一层除了自己以外的 Available 节点都设为 Locked
        foreach (var node in mapNodes)
        {
            if (node.gridPosition.x == layerIndex && node != currentNode)
            {
                if (node.status == Enum.NodeStatus.Available)
                {
                    node.status = Enum.NodeStatus.Locked;
                }
            }
        }
    }
    // 当战斗胜利或事件结束时调用此方法
    public void CompleteCurrentRoom()
    {
        if (currentNode == null) return;

        // 1. 标记当前为已完成
        currentNode.status = Enum.NodeStatus.Completed;

        // 2. 解锁下一层的子节点 (Outgoing)
        foreach (var nextPos in currentNode.outgoing)
        {
            // 在所有节点里找到对应的 MapNode
            // (这里可以用字典优化性能，但 List 也没事)
            var nextNode = mapNodes.Find(n => n.gridPosition == nextPos);
            if (nextNode != null)
            {
                nextNode.status = Enum.NodeStatus.Available;
            }
        }

        // 3. 刷新 UI
        OnMapGenerated?.Invoke();
    }
    // 通用辅助方法：从列表里随机拿一个 (带防空检查)
    private T GetRandomItem<T>(List<T> list) where T : class
    {
        if (list == null || list.Count == 0) return null;
        return list[UnityEngine.Random.Range(0, list.Count)];
    }
    // 经典的权重随机算法
    private Enum.RoomType GetRandomRoomType(List<RoomTypeWeight> weights)
    {
        if (weights == null || weights.Count == 0) return Enum.RoomType.Battle;

        int totalWeight = 0;
        foreach (var w in weights) totalWeight += w.weight;

        int rng = UnityEngine.Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var w in weights)
        {
            currentWeight += w.weight;
            if (rng < currentWeight)
            {
                return w.type;
            }
        }
        return Enum.RoomType.Battle; // 兜底
    }
}