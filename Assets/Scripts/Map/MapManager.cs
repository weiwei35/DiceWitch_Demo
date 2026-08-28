using System;
using UnityEngine;
using System.Collections.Generic;

public class MapManager : MonoBehaviour
{
    public static MapManager Instance;

    [Header("Config")]
    public BoardMapConfigSO boardConfig;
    public MapPresentationCatalogSO MapPresentationCatalog => boardConfig != null ? boardConfig.presentationCatalog : null;
    
    [Header("Runtime Data")]
    public List<BoardNode> boardNodes = new List<BoardNode>();
    public List<BoardRoom> boardRooms = new List<BoardRoom>();
    public int currentPlayerNodeIndex = 0; 
    private readonly HashSet<int> _visitedNodeIndices = new HashSet<int>();
    public IReadOnlyCollection<int> VisitedNodeIndices => _visitedNodeIndices;
    
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
        boardRooms.Clear();
        clearedRoomIds.Clear(); // 重置通关记录
        _visitedNodeIndices.Clear();
        currentPlayerNodeIndex = 0;
        
        int globalNodeIndex = 0;
        int globalRoomId = 0; // 全局房间ID，用来标记房间是否通关
        Dictionary<MapRoomLayout, int> roomIdByLayout = new Dictionary<MapRoomLayout, int>();
        List<MapRoomLayout> flattenedRooms = new List<MapRoomLayout>();
        List<int> flattenedRegionIndices = new List<int>();

        // 防空检查
        if (boardConfig.regions == null || boardConfig.regions.Count == 0)
        {
            Debug.LogWarning("BoardConfig 中没有任何 Region 配置！");
            return;
        }

        if (boardConfig.presentationCatalog == null)
            Debug.LogError("BoardConfig 未配置 presentationCatalog。地图节点图标、tooltip 和状态颜色将无法正常显示。");

        foreach (var region in boardConfig.regions)
        {
            if (region.regionPrefab == null) continue;
            
            var layout = region.regionPrefab.GetComponent<MapRegionLayout>();
            if (layout == null) 
            {
                Debug.LogError($"预制体 {region.regionPrefab.name} 上没有挂载 MapRegionLayout 脚本！");
                continue;
            }

            int regionIndex = boardConfig.regions.IndexOf(region);
            foreach (var roomLayout in layout.orderedRooms)
            {
                if (roomLayout == null) continue; // 防空检查
                if (roomIdByLayout.ContainsKey(roomLayout)) continue;

                roomIdByLayout[roomLayout] = flattenedRooms.Count;
                flattenedRooms.Add(roomLayout);
                flattenedRegionIndices.Add(regionIndex);
            }
        }

        for (int roomIndex = 0; roomIndex < flattenedRooms.Count; roomIndex++)
        {
            var roomLayout = flattenedRooms[roomIndex];
            int regionIndex = flattenedRegionIndices[roomIndex];
            int roomStartIndex = globalNodeIndex;

            // 遍历房间里的所有节点
            foreach (var anchor in roomLayout.roomNodes)
            {
                if (anchor == null) continue; // 防空检查

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

            BoardRoom room = new BoardRoom
            {
                roomId = globalRoomId,
                regionIndex = regionIndex,
                roomDataRef = roomLayout.roomData,
                startNodeIndex = roomStartIndex,
                endNodeIndex = Mathf.Max(roomStartIndex, globalNodeIndex - 1)
            };
            boardRooms.Add(room);
            globalRoomId++; // 切换到下一个房间
        }

        for (int roomIndex = 0; roomIndex < flattenedRooms.Count; roomIndex++)
        {
            MapRoomLayout roomLayout = flattenedRooms[roomIndex];
            BoardRoom room = boardRooms[roomIndex];

            if (roomLayout.nextRooms != null && roomLayout.nextRooms.Count > 0)
            {
                foreach (MapRoomLayout nextRoom in roomLayout.nextRooms)
                {
                    if (nextRoom == null) continue;
                    if (roomIdByLayout.TryGetValue(nextRoom, out int nextRoomId) && !room.nextRoomIds.Contains(nextRoomId))
                        room.nextRoomIds.Add(nextRoomId);
                }
            }
            else if (roomIndex + 1 < boardRooms.Count)
            {
                room.nextRoomIds.Add(boardRooms[roomIndex + 1].roomId);
            }
        }
        
        if (boardNodes.Count > 0)
            MarkNodeVisited(currentPlayerNodeIndex);

        Debug.Log($"<color=green>地图数据生成完毕！共 {boardNodes.Count} 个节点，{globalRoomId} 个房间。</color>");
        OnMapGenerated?.Invoke();
    }

    public bool IsNodeVisited(int nodeIndex)
    {
        return _visitedNodeIndices.Contains(nodeIndex);
    }

    public bool MarkNodeVisited(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= boardNodes.Count) return false;
        return _visitedNodeIndices.Add(nodeIndex);
    }

    // =================================================================
    // 【核心机制】当玩家的棋子停在一个节点上时调用
    // =================================================================
    public void OnPlayerLanded(BoardNode landedNode, Vector3 pawnPos)
    {
        Debug.Log($"玩家落在第 {landedNode.index} 格，类型：{landedNode.type}");

        // 1. 先生效格子自身的效果，并在棋子头上飘字
        ProcessNodeEffect(landedNode, pawnPos);

        // 2. 如果是锻造节点，先进入锻造流程，完成后根据房间是否已通关决定去向
        if (landedNode.type == GameEnums.BoardNodeType.锻造)
        {
            GameFlowController.Instance.StartForgeProcess(() =>
            {
                if (landedNode.roomDataRef != null && !clearedRoomIds.Contains(landedNode.roomId))
                {
                    // 房间未通关，进入房间事件
                    StartCoroutine(DelayEnterRoom(landedNode, 0f));
                }
                else
                {
                    // 房间已通关，直接回到地图
                    GameFlowController.Instance.ChangeState(new MapState());
                }
            });
        }
        else
        {
            StartCoroutine(DelayEnterRoom(landedNode));
        }
    }

    // 【新增】延迟进入房间的协程
    private System.Collections.IEnumerator DelayEnterRoom(BoardNode landedNode, float? delayOverride = null)
    {
        // 如果有实际效果，多等一会 (1秒)；如果是空地或者直接进事件的格子，少等一会 (0.3秒缓冲)
        float delayTime = delayOverride ?? ((landedNode.type == GameEnums.BoardNodeType.空 || landedNode.type == GameEnums.BoardNodeType.事件) ? 0.3f : 2.0f);
        
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
        GameEnums.BoardNodeType effectType = ResolveExecutableNodeEffect(node);
        if (effectType == GameEnums.BoardNodeType.空) return;

        ApplyNodeEffect(effectType, node.effectValue);
        ShowNodeEffectFeedback(effectType, node.effectValue, pawnPos);
    }

    private GameEnums.BoardNodeType ResolveExecutableNodeEffect(BoardNode node)
    {
        if (node == null) return GameEnums.BoardNodeType.空;
        if (node.type == GameEnums.BoardNodeType.锻造)
            return node.forgeBonusType;

        return node.type;
    }

    private void ApplyNodeEffect(GameEnums.BoardNodeType effectType, int value)
    {
        switch (effectType)
        {
            case GameEnums.BoardNodeType.加减Hp:
                if (value > 0) PlayerManager.Instance.Heal(value);
                else if (value < 0) PlayerManager.Instance.TakeDamage(Mathf.Abs(value));
                break;
                
            case GameEnums.BoardNodeType.加减资源:
                if (value > 0) ResourceManager.Instance.AddManaDust(value);
                else if (value < 0) ResourceManager.Instance.TrySpendManaDust(Mathf.Abs(value));
                break;
                
            case GameEnums.BoardNodeType.一次护甲:
                PlayerManager.Instance.nextBattleArmorBonus += value;
                break;
                
            case GameEnums.BoardNodeType.骰子点数必中:
                PlayerManager.Instance.nextBattleFixedDiceValue = value;
                break;
                
            case GameEnums.BoardNodeType.抵消下一次伤害:
                PlayerManager.Instance.hasBlockNextDamageShield = true;
                PlayerManager.Instance.UpdateUI();
                break;
                
            case GameEnums.BoardNodeType.一次伤害增加:
                PlayerManager.Instance.nextBattleDamageBonus += value;
                break;
        }
    }

    private void ShowNodeEffectFeedback(GameEnums.BoardNodeType effectType, int value, Vector3 pawnPos)
    {
        if (FloatingTextManager.Instance == null || MapPresentationCatalog == null) return;

        if (MapPresentationCatalog.TryBuildFloatingText(effectType, value, out string floatText, out Color floatColor))
            FloatingTextManager.Instance.ShowText(pawnPos, floatText, floatColor);
    }
    //找下一个房间的起点
    public int GetNextRoomStartIndex(int currentRoomId)
    {
        BoardRoom currentRoom = GetRoom(currentRoomId);
        if (currentRoom != null && currentRoom.nextRoomIds.Count > 0)
        {
            return GetRoomStartIndex(currentRoom.nextRoomIds[0]);
        }
        
        // 如果返回 -1，说明这已经是整个大地图的最后一个房间了，没地方可跳了
        return -1; 
    }

    public BoardRoom GetRoom(int roomId)
    {
        if (roomId < 0 || roomId >= boardRooms.Count) return null;
        return boardRooms[roomId];
    }

    public int GetRoomStartIndex(int roomId)
    {
        BoardRoom room = GetRoom(roomId);
        return room != null ? room.startNodeIndex : -1;
    }

    public bool TryGetNextNode(int currentIndex, out int nextIndex, out List<BoardRoom> branchChoices)
    {
        nextIndex = -1;
        branchChoices = null;

        if (currentIndex < 0 || currentIndex >= boardNodes.Count) return false;

        BoardNode currentNode = boardNodes[currentIndex];
        BoardRoom currentRoom = GetRoom(currentNode.roomId);
        if (currentRoom == null) return false;

        bool shouldSkipRemainingRoomNodes =
            currentNode.roomDataRef != null &&
            currentNode.roomDataRef.skipRemainingNodesOnClear &&
            clearedRoomIds.Contains(currentNode.roomId) &&
            currentIndex < currentRoom.endNodeIndex;

        if (!shouldSkipRemainingRoomNodes && currentIndex < currentRoom.endNodeIndex)
        {
            nextIndex = currentIndex + 1;
            return true;
        }

        if (currentRoom.nextRoomIds == null || currentRoom.nextRoomIds.Count == 0)
            return false;

        if (currentRoom.nextRoomIds.Count == 1)
        {
            nextIndex = GetRoomStartIndex(currentRoom.nextRoomIds[0]);
            return nextIndex >= 0;
        }

        branchChoices = new List<BoardRoom>();
        foreach (int nextRoomId in currentRoom.nextRoomIds)
        {
            BoardRoom nextRoom = GetRoom(nextRoomId);
            if (nextRoom != null)
                branchChoices.Add(nextRoom);
        }

        if (branchChoices.Count == 1)
        {
            nextIndex = branchChoices[0].startNodeIndex;
            branchChoices = null;
            return nextIndex >= 0;
        }

        return branchChoices.Count > 1;
    }

    public void CommitBranchChoice(int sourceNodeIndex, int chosenRoomId)
    {
        if (sourceNodeIndex < 0 || sourceNodeIndex >= boardNodes.Count) return;

        BoardRoom sourceRoom = GetRoom(boardNodes[sourceNodeIndex].roomId);
        if (sourceRoom == null || sourceRoom.nextRoomIds == null || !sourceRoom.nextRoomIds.Contains(chosenRoomId))
            return;

        HashSet<int> chosenReachableRooms = CollectReachableRoomIds(chosenRoomId);
        HashSet<int> rejectedExclusiveRooms = new HashSet<int>();

        foreach (int nextRoomId in sourceRoom.nextRoomIds)
        {
            if (nextRoomId == chosenRoomId) continue;

            foreach (int reachableRoomId in CollectReachableRoomIds(nextRoomId))
            {
                if (!chosenReachableRooms.Contains(reachableRoomId))
                    rejectedExclusiveRooms.Add(reachableRoomId);
            }
        }

        foreach (BoardNode node in boardNodes)
        {
            if (rejectedExclusiveRooms.Contains(node.roomId))
                node.isInvalidated = true;
        }
    }

    private HashSet<int> CollectReachableRoomIds(int startRoomId)
    {
        HashSet<int> visited = new HashSet<int>();
        Stack<int> pending = new Stack<int>();
        pending.Push(startRoomId);

        while (pending.Count > 0)
        {
            int roomId = pending.Pop();
            if (!visited.Add(roomId)) continue;

            BoardRoom room = GetRoom(roomId);
            if (room == null || room.nextRoomIds == null) continue;

            foreach (int nextRoomId in room.nextRoomIds)
            {
                if (!visited.Contains(nextRoomId))
                    pending.Push(nextRoomId);
            }
        }

        return visited;
    }
}
