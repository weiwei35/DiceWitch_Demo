using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class MapInteractionManager : MonoBehaviour
{
    public static MapInteractionManager Instance;

    [Header("References")]
    public DiceViewMonitor mapDiceViewMonitor;
    public DiceDataSO mapDiceData;
    public MapViewController mapUI;
    public TMP_FontAsset fontMaterial;

    [Header("Pawn Settings")]
    public GameObject playerPawnPrefab;  

    [Header("Branch Choice UI")]
    public Vector2 branchChoiceOffset = new Vector2(130f, 0f);
    public Vector2 branchChoiceButtonSize = new Vector2(150f, 42f);
    public float branchChoiceButtonSpacing = 48f;
    
    private MapPlayerPawn _spawnedPawn;
    private bool _isProcessing = false;
    private GameObject _branchChoiceRoot;
    private int? _selectedBranchRoomId;
    private PhysicsDice _mapDice;
    private ProjectedDiceWeakGuide _projectedDiceGuide;
    private bool _mapDiceStageActive;

    void Awake()
    {
        Instance = this;
        _projectedDiceGuide = GetComponent<ProjectedDiceWeakGuide>();
        if (_projectedDiceGuide == null)
            _projectedDiceGuide = gameObject.AddComponent<ProjectedDiceWeakGuide>();
    }

    private void Update()
    {
        if (!_mapDiceStageActive || _isProcessing || _spawnedPawn == null)
            return;
        if (!Input.GetMouseButtonDown(0) || mapDiceViewMonitor == null)
            return;

        PhysicsDice clickedDice = mapDiceViewMonitor.GetDiceUnderScreenPoint(Input.mousePosition);
        if (clickedDice == _mapDice)
            RollMapDice();
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

    public void EnterMapDiceStage()
    {
        _mapDiceStageActive = true;
        ReleaseMapDiceReference();
        WeakGuideService.Instance?.ActivateScreen(this);

        DiceThrower thrower = DiceThrower.Instance;
        if (thrower == null) return;

        thrower.ClearOldDice();
        EnsureMapDice();
        ShowMapDiceGuide();
    }

    public void ExitMapDiceStage()
    {
        _mapDiceStageActive = false;
        WeakGuideService.Instance?.DeactivateScreen(this);
        _projectedDiceGuide?.Hide();
        ReleaseMapDiceReference();
        DiceThrower.Instance?.ClearOldDice();
    }

    private void RollMapDice()
    {
        if (_isProcessing || _spawnedPawn == null) return;
        
        _isProcessing = true;

        DiceThrower thrower = DiceThrower.Instance;
        EnsureMapDice();
        if (_mapDice == null)
        {
            _isProcessing = false;
            return;
        }

        WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.MapRollDice);
        _projectedDiceGuide?.Hide();
        _mapDice.OnDiceSettled -= HandleDiceResult;
        _mapDice.OnDiceSettled += HandleDiceResult;
        thrower.RollExistingSingleDice(_mapDice);
    }

    private void EnsureMapDice()
    {
        if (_mapDice != null) return;
        if (DiceThrower.Instance == null || mapDiceData == null) return;

        _mapDice = DiceThrower.Instance.SpawnIdleSingleDice(RuntimeDiceData.FromSO(mapDiceData));
        if (_mapDice == null) return;

        DiceDragger dragger = _mapDice.GetComponent<DiceDragger>();
        if (dragger != null) dragger.enabled = false;
    }

    private void ShowMapDiceGuide()
    {
        if (!_mapDiceStageActive
            || _mapDice == null
            || mapDiceViewMonitor == null
            || WeakGuideService.Instance == null
            || WeakGuideService.Instance.IsCompleted(WeakGuideIds.MapRollDice))
        {
            _projectedDiceGuide?.Hide();
            return;
        }

        _projectedDiceGuide.Bind(mapDiceViewMonitor, _mapDice);
        _projectedDiceGuide.Show(this, WeakGuideIds.MapRollDice);
    }

    private void ReleaseMapDiceReference()
    {
        if (_mapDice != null)
            _mapDice.OnDiceSettled -= HandleDiceResult;

        _mapDice = null;
    }

    private void HandleDiceResult(int steps)
    {
        Debug.Log($"大地图骰子掷出了：{steps} 点！");
        if (_mapDice != null)
            _mapDice.OnDiceSettled -= HandleDiceResult;

        // =========================================================
        // 【新增】掷出骰子了，立刻恢复摄像机的自动跟随模式！
        // =========================================================
        if (mapUI != null)
        {
            mapUI.ResumeAutoFollow();
        }

        StartCoroutine(MoveBySteps(steps));
    }

    private IEnumerator MoveBySteps(int steps)
    {
        int targetIndex = MapManager.Instance.currentPlayerNodeIndex;
        int remainingSteps = steps;

        while (remainingSteps > 0)
        {
            if (!MapManager.Instance.TryGetNextNode(targetIndex, out int nextIndex, out List<BoardRoom> branchChoices))
                break;

            if (branchChoices != null && branchChoices.Count > 1)
            {
                ShowBranchChoiceButtons(targetIndex, branchChoices);
                if (_branchChoiceRoot == null)
                    _selectedBranchRoomId = branchChoices[0].roomId;

                yield return new WaitUntil(() => _selectedBranchRoomId.HasValue);

                int chosenRoomId = _selectedBranchRoomId.Value;
                MapManager.Instance.CommitBranchChoice(targetIndex, chosenRoomId);
                WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.MapChooseBranch);
                if (mapUI != null)
                    mapUI.UpdateNodeStates(targetIndex);

                nextIndex = MapManager.Instance.GetRoomStartIndex(chosenRoomId);
                _selectedBranchRoomId = null;
                HideBranchChoiceButtons();

                if (nextIndex < 0)
                    break;
            }

            if (mapUI != null && mapUI.nodeUIRects.TryGetValue(nextIndex, out RectTransform rect))
            {
                Vector3 localPos = mapUI.contentParent.InverseTransformPoint(rect.position);
                yield return _spawnedPawn.MoveAlongPath(new List<Vector3> { localPos }, null);
            }

            targetIndex = nextIndex;
            MapManager.Instance.currentPlayerNodeIndex = targetIndex;
            if (mapUI != null)
                mapUI.UpdateNodeStates(targetIndex);
            remainingSteps--;
        }

        MapManager.Instance.currentPlayerNodeIndex = targetIndex;

        if (mapUI != null)
            mapUI.UpdateNodeStates(MapManager.Instance.currentPlayerNodeIndex);

        BoardNode landedNode = MapManager.Instance.boardNodes[targetIndex];
        MapManager.Instance.OnPlayerLanded(landedNode, _spawnedPawn.transform.position);

        _isProcessing = false;
    }

    private void ShowBranchChoiceButtons(int currentNodeIndex, List<BoardRoom> branchChoices)
    {
        HideBranchChoiceButtons();
        _selectedBranchRoomId = null;

        if (mapUI == null || mapUI.contentParent == null) return;
        if (!mapUI.nodeUIRects.TryGetValue(currentNodeIndex, out RectTransform currentRect)) return;

        _branchChoiceRoot = new GameObject("MapBranchChoice", typeof(RectTransform));
        _branchChoiceRoot.transform.SetParent(mapUI.contentParent, false);
        _branchChoiceRoot.transform.SetAsLastSibling();

        RectTransform rootRect = _branchChoiceRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.localPosition = mapUI.contentParent.InverseTransformPoint(currentRect.position);
        rootRect.anchoredPosition += branchChoiceOffset;

        List<WeakGuideEffect> branchGuideEffects = new List<WeakGuideEffect>();
        for (int i = 0; i < branchChoices.Count; i++)
        {
            BoardRoom room = branchChoices[i];
            WeakGuideEffect effect = CreateBranchChoiceButton(rootRect, room, i, branchChoices.Count);
            if (effect != null)
                branchGuideEffects.Add(effect);
        }

        WeakGuideService.Instance?.ShowGuide(
            this,
            WeakGuideIds.MapChooseBranch,
            branchGuideEffects);
    }

    private WeakGuideEffect CreateBranchChoiceButton(
        RectTransform parent,
        BoardRoom room,
        int index,
        int totalCount)
    {
        GameObject buttonObject = new GameObject($"BranchChoice_{room.roomId}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = branchChoiceButtonSize;
        rect.anchoredPosition = new Vector2(0f, (totalCount - 1) * branchChoiceButtonSpacing * 0.5f - index * branchChoiceButtonSpacing);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.1f, 0.18f, 0.92f);

        Button button = buttonObject.GetComponent<Button>();
        int capturedRoomId = room.roomId;
        button.onClick.AddListener(() => _selectedBranchRoomId = capturedRoomId);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = fontMaterial;
        if (index < totalCount - 1)
        {
            text.text = "上";
        }
        else
        {
            text.text = "下";
        }
        
        // text.text = room.roomDataRef != null ? room.roomDataRef.roomName : $"房间 {room.roomId + 1}";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20f;
        text.color = Color.white;
        text.raycastTarget = false;

        return WeakGuideEffect.GetOrCreate(rect, image);
    }

    private void HideBranchChoiceButtons()
    {
        WeakGuideService.Instance?.ClearGuide(this);
        if (_branchChoiceRoot != null)
            Destroy(_branchChoiceRoot);
        _branchChoiceRoot = null;
    }
}
