using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapInteractionManager : MonoBehaviour
{
    public static MapInteractionManager Instance;

    [Header("References")]
    public Button rollDiceButton;        
    public DiceDataSO mapDiceData;       
    public MapViewController mapUI;

    [Header("Pawn Settings")]
    public GameObject playerPawnPrefab;  
    
    private MapPlayerPawn _spawnedPawn;
    private bool _isProcessing = false;

    void Awake() { Instance = this; }

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
                // 把棋子生在 Content 下面，跟随地图滚动
                GameObject pawnObj = Instantiate(playerPawnPrefab, mapUI.contentParent);
                pawnObj.transform.SetAsLastSibling(); 
                _spawnedPawn = pawnObj.GetComponent<MapPlayerPawn>();
            }

            if (_spawnedPawn != null)
            {
                Vector3 localPos = mapUI.contentParent.InverseTransformPoint(rect.position);
                _spawnedPawn.transform.localPosition = localPos;
                
                // 告诉地图 UI，锁定这颗棋子！并开启自动跟随
                mapUI.targetPawn = _spawnedPawn.transform;
                mapUI.ResumeAutoFollow();
            }
        }
        else
        {
            Debug.LogError($"致命错误：找不到 Index={currentIndex} 的格子！");
        }
    }

    public void OnRollDiceClicked()
    {
        if (_isProcessing || _spawnedPawn == null) return;
        
        _isProcessing = true;
        rollDiceButton.interactable = false;

        DiceThrower thrower = DiceThrower.Instance;
        PhysicsDice dice = thrower.SpawnSingleDice(RuntimeDiceData.FromSO(mapDiceData));

        DiceDragger dragger = dice.GetComponent<DiceDragger>();
        if (dragger != null) dragger.enabled = false;

        dice.OnDiceSettled += HandleDiceResult;
    }

    private void HandleDiceResult(int steps)
    {
        Debug.Log($"大地图骰子掷出了：{steps} 点！");
        DiceThrower.Instance.ClearOldDice();

        // =========================================================
        // 【新增】掷出骰子了，立刻恢复摄像机的自动跟随模式！
        // =========================================================
        if (mapUI != null)
        {
            mapUI.ResumeAutoFollow();
        }

        int startIndex = MapManager.Instance.currentPlayerNodeIndex;
        int maxIndex = MapManager.Instance.boardNodes.Count - 1;
        
        List<Vector3> path = new List<Vector3>();
        int targetIndex = startIndex;

        BoardNode startNode = MapManager.Instance.boardNodes[startIndex];
        bool shouldSkipToNextRoom = startNode.roomDataRef != null && 
                                    startNode.roomDataRef.skipRemainingNodesOnClear && 
                                    MapManager.Instance.clearedRoomIds.Contains(startNode.roomId);

        for (int i = 0; i < steps; i++)
        {
            if (targetIndex >= maxIndex) break; 
            
            if (shouldSkipToNextRoom && i == 0)
            {
                int jumpIndex = MapManager.Instance.GetNextRoomStartIndex(startNode.roomId);
                if (jumpIndex != -1 && jumpIndex <= maxIndex)
                    targetIndex = jumpIndex; 
                else
                    targetIndex++; 
            }
            else
            {
                targetIndex++; 
            }
            
            if (mapUI.nodeUIRects.TryGetValue(targetIndex, out RectTransform rect))
            {
                Vector3 localPos = mapUI.contentParent.InverseTransformPoint(rect.position);
                path.Add(localPos);
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
            MapManager.Instance.OnPlayerLanded(landedNode, _spawnedPawn.transform.position);

            _isProcessing = false;
            rollDiceButton.interactable = true;
        }));
    }
}