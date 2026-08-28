using System;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance;

    [Header("Map UI")]
    public GameObject _mapPanel;
    public GameObject _roomUIRoot;
    public GameObject _battleUIRoot;

    [Header("Start UI")]
    public GameObject _startPanelRoot;

    [Header("Visual Feedback")]
    public GameObject _selectionModeTip;

#if UNITY_EDITOR
    [Header("Development")]
    [Tooltip("勾选后，每次进入 Play Mode 都会清空全部弱引导完成记录，方便重复测试完整引导流程。")]
    public bool resetAllWeakGuidesOnPlay;
#endif

    // Static access for state classes
    public static GameObject MapPanel => Instance._mapPanel;
    public static GameObject RoomUIRoot => Instance._roomUIRoot;
    public static GameObject SelectionModeTip => Instance._selectionModeTip;
    public static GameObject BattleUIRoot => Instance.GetBattleUIRoot();

    private IGameState _currentState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

#if UNITY_EDITOR
        if (Instance == this && resetAllWeakGuidesOnPlay)
            WeakGuideService.Instance?.ResetAllProgressForDevelopment();
#endif
    }

    void Start()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnBattleVictoryEvent += HandleBattleVictory;
            BattleManager.Instance.OnBattleDefeatEvent += HandleBattleDefeat;
        }

        if (_startPanelRoot != null)
        {
            ShowStartPanel();
            return;
        }

        BeginGame();
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnBattleVictoryEvent -= HandleBattleVictory;
            BattleManager.Instance.OnBattleDefeatEvent -= HandleBattleDefeat;
        }
    }

    public void ChangeState(IGameState newState)
    {
        _currentState?.Exit();
        _currentState = newState;
        Debug.Log($"<color=green>游戏状态切换至: {newState.GetType().Name}</color>");
        _currentState.Enter();
    }

    public void BeginGame()
    {
        if (_startPanelRoot != null)
            _startPanelRoot.SetActive(false);

        ChangeState(new MapState());
    }

    private void ShowStartPanel()
    {
        _currentState?.Exit();
        _currentState = null;

        _startPanelRoot.SetActive(true);
        _mapPanel?.SetActive(false);
        _roomUIRoot?.SetActive(false);
        SetBattleUIVisible(false);
        _selectionModeTip?.SetActive(false);
        SpellDraftPanel.Instance?.Hide();
        RewardDiceSelectionPanel.Instance?.Hide();
        TooltipSystem.Instance?.Hide();
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
        _currentState?.OnSlotClicked(slotData, uiPos);
    }

    private void HandleBattleVictory()
    {
        StartDraftProcess(() => ChangeState(new MapState()));
    }

    private void HandleBattleDefeat()
    {
        ShowRunSummary(false);
    }

    public void ShowRunSummary(bool isVictory)
    {
        if (RunTracker.Instance != null) RunTracker.Instance.isVictory = isVictory;
        RunSummaryUIManager.Instance?.ShowSummary();
    }

    public void EnterRoom(RoomDataSO roomData)
    {
        if (roomData == null)
        {
            Debug.LogError("试图进入的房间数据为空！");
            return;
        }

        Debug.Log($"<color=cyan>GameFlow: 准备进入房间 -> {roomData.roomName} ({roomData.roomType})</color>");
        if (RunTracker.Instance != null) RunTracker.Instance.roomsVisited++;

        switch (roomData.roomType)
        {
            case GameEnums.RoomType.Start:
                Debug.Log("起点房间不触发额外流程，返回大地图");
                ChangeState(new MapState());
                break;

            case GameEnums.RoomType.Battle:
            case GameEnums.RoomType.Elite:
            case GameEnums.RoomType.Boss:
                if (roomData is BattleRoomSO battleData)
                    ChangeState(new BattleState(battleData));
                break;

            case GameEnums.RoomType.Event:
                if (roomData is EventRoomSO eventData)
                    ChangeState(new EventState(eventData));
                break;

            case GameEnums.RoomType.Shop:
                Debug.Log("商店状态尚未实现，暂回大地图");
                ChangeState(new MapState());
                break;

            case GameEnums.RoomType.Rest:
            case GameEnums.RoomType.Treasure:
                Debug.Log($"{roomData.roomType} 状态尚未实现，暂回大地图");
                ChangeState(new MapState());
                break;

            default:
                Debug.LogWarning($"未处理的房间类型: {roomData.roomType}");
                ChangeState(new MapState());
                break;
        }
    }

    public void StartDraftProcess(Action onComplete = null)
    {
        ChangeState(new SpellDraftState(onComplete));
    }

    public void StartForgeProcess(Action onComplete = null)
    {
        ChangeState(new ForgeState(onComplete));
    }

    public static void SetBattleUIVisible(bool visible)
    {
        GameObject root = BattleUIRoot;
        if (root != null) root.SetActive(visible);
    }

    private GameObject GetBattleUIRoot()
    {
        if (_battleUIRoot != null) return _battleUIRoot;
        if (_roomUIRoot == null) return null;

        Transform battleRoot = _roomUIRoot.transform.Find("Battle");
        if (battleRoot == null) return null;

        _battleUIRoot = battleRoot.gameObject;
        return _battleUIRoot;
    }
}
