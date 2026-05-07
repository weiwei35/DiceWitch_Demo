using System;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance;

    [Header("Map UI")]
    public GameObject _mapPanel;
    public GameObject _roomUIRoot;

    [Header("Visual Feedback")]
    public GameObject _selectionModeTip;

    // Static access for state classes
    public static GameObject MapPanel => Instance._mapPanel;
    public static GameObject RoomUIRoot => Instance._roomUIRoot;
    public static GameObject SelectionModeTip => Instance._selectionModeTip;

    private IGameState _currentState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnBattleVictoryEvent += HandleBattleVictory;
            BattleManager.Instance.OnBattleDefeatEvent += HandleBattleDefeat;
        }

        ChangeState(new MapState());
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
}
