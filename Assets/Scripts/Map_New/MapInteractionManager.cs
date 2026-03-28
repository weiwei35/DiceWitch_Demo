using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapInteractionManager : MonoBehaviour
{
    [Header("References")]
    public Button rollDiceButton;        
    public DiceDataSO mapDiceData;       
    public MapViewController mapUI;

    [Header("Pawn Settings")]
    public GameObject playerPawnPrefab;  
    
    private MapPlayerPawn _spawnedPawn;  
    private bool _isProcessing = false;  

    void Start()
    {
        rollDiceButton.onClick.AddListener(OnRollDiceClicked);
    }

    public void InitPawnPosition()
    {
        if (MapManager.Instance == null || mapUI == null) return;

        int currentIndex = MapManager.Instance.currentPlayerNodeIndex;
        
        if (mapUI.nodeUIRects.TryGetValue(currentIndex, out RectTransform rect))
        {
            if (_spawnedPawn == null && playerPawnPrefab != null)
            {
                // =========================================================
                // 【核心修复】将实例化的父节点设为 mapUI.contentParent 
                // 这样棋子就会成为滚动区域的一部分，跟随地图一起拖动了！
                // =========================================================
                GameObject pawnObj = Instantiate(playerPawnPrefab, mapUI.contentParent);
                
                // 设置为同层级最后，保证渲染在所有地图和节点的上方，不被遮挡
                pawnObj.transform.SetAsLastSibling(); 
                _spawnedPawn = pawnObj.GetComponent<MapPlayerPawn>();
            }

            if (_spawnedPawn != null)
            {
                // UI 里的 position 是世界坐标，直接赋值即可准确对齐
                _spawnedPawn.TeleportTo(rect.position);
            }
        }
        else
        {
            Debug.LogError($"致命错误：找不到 Index={currentIndex} 的格子！目前字典里存了 {mapUI.nodeUIRects.Count} 个坐标。");
        }
    }

    public void OnRollDiceClicked()
    {
        if (_isProcessing || _spawnedPawn == null) return;
        
        _isProcessing = true;
        rollDiceButton.interactable = false;

        DiceThrower thrower = FindObjectOfType<DiceThrower>();
        PhysicsDice dice = thrower.SpawnSingleDice(mapDiceData);

        DiceDragger dragger = dice.GetComponent<DiceDragger>();
        if (dragger != null) dragger.enabled = false;

        dice.OnDiceSettled += HandleDiceResult;
    }

    private void HandleDiceResult(int steps)
    {
        Debug.Log($"大地图骰子掷出了：{steps} 点！");
        FindObjectOfType<DiceThrower>().ClearOldDice();

        int startIndex = MapManager.Instance.currentPlayerNodeIndex;
        int maxIndex = MapManager.Instance.boardNodes.Count - 1;
        
        List<Vector3> path = new List<Vector3>();
        int targetIndex = startIndex;

        // =================================================================
        // 【核心新增】判断起点是否在“已打通且允许跳关”的房间内
        // =================================================================
        BoardNode startNode = MapManager.Instance.boardNodes[startIndex];
        // 如果当前节点挂载了房间数据，且该房间配置了跳关，并且已经被标记为通关
        bool shouldSkipToNextRoom = startNode.roomDataRef != null && 
                                    startNode.roomDataRef.skipRemainingNodesOnClear && 
                                    MapManager.Instance.clearedRoomIds.Contains(startNode.roomId);


        // =================================================================
        // 开始计算这几步的具体落点坐标
        // =================================================================
        for (int i = 0; i < steps; i++)
        {
            if (targetIndex >= maxIndex) break; 
            
            // 如果允许跳关，并且这是掷骰子后走的第一步 (i == 0)
            if (shouldSkipToNextRoom && i == 0)
            {
                int jumpIndex = MapManager.Instance.GetNextRoomStartIndex(startNode.roomId);
                
                if (jumpIndex != -1 && jumpIndex <= maxIndex)
                {
                    targetIndex = jumpIndex; // 直接把目标索引指向下个房间的起点
                    Debug.Log($"<color=magenta>【跨房间起飞】越过了前面多余的节点，直接飞跃到 Index: {targetIndex}！</color>");
                }
                else
                {
                    targetIndex++; // 兜底：已经是最后一关了，没得跳，只能往前挪一格
                }
            }
            else
            {
                targetIndex++; // 正常的移动逻辑（如果是跳过之后剩下的步数，就在新房间里正常 +1）
            }
            
            // 将算出来的这一步落点放入寻路列表
            if (mapUI.nodeUIRects.TryGetValue(targetIndex, out RectTransform rect))
            {
                path.Add(rect.position);
            }
        }

        MapManager.Instance.currentPlayerNodeIndex = targetIndex;

        StartCoroutine(_spawnedPawn.MoveAlongPath(path, () => 
        {
            if (mapUI != null)
            {
                mapUI.UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);
            }

            BoardNode landedNode = MapManager.Instance.boardNodes[targetIndex];
            
            // =========================================================
            // 【关键修改】把当前棋子 _spawnedPawn 的位置传过去，作为飘字的起点！
            // =========================================================
            MapManager.Instance.OnPlayerLanded(landedNode, _spawnedPawn.transform.position);

            _isProcessing = false;
            rollDiceButton.interactable = true;
        }));
    }
}