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
                GameObject pawnObj = Instantiate(playerPawnPrefab, mapUI.transform);
                pawnObj.transform.SetAsLastSibling(); 
                _spawnedPawn = pawnObj.GetComponent<MapPlayerPawn>();
            }

            if (_spawnedPawn != null)
            {
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

        for (int i = 0; i < steps; i++)
        {
            if (targetIndex >= maxIndex) break; 
            targetIndex++;
            
            if (mapUI.nodeUIRects.TryGetValue(targetIndex, out RectTransform rect))
            {
                path.Add(rect.position);
            }
        }

        MapManager.Instance.currentPlayerNodeIndex = targetIndex;

        StartCoroutine(_spawnedPawn.MoveAlongPath(path, () => 
        {
            // =========================================================
            // 【核心修复】使用 mapUI 统一刷新格子颜色，替代已被删除的 MapNodeButton
            // =========================================================
            if (mapUI != null)
            {
                mapUI.UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);
            }

            BoardNode landedNode = MapManager.Instance.boardNodes[targetIndex];
            MapManager.Instance.OnPlayerLanded(landedNode);

            _isProcessing = false;
            rollDiceButton.interactable = true;
        }));
    }
}