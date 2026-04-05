using System;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance;

    [Header("UI Managers")]
    public MagicCircleDisplay _circleDisplay; // 改为 public 方便 State 类访问，或者写 Get 方法
    public SpellDraftPanel _draftPanel;
    public EventUIManager _eventUIManager;
    public RunSummaryUIManager _runSummaryUI;
    
    [Header("Map UI")]
    public GameObject _mapPanel; 
    public GameObject _roomUIRoot; 

    [Header("Visual Feedback")]
    public GameObject _selectionModeTip;

    // 【新增】当前的状态对象
    private IGameState _currentState;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 监听 BattleManager 的广播
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnBattleVictoryEvent += HandleBattleVictory;
            BattleManager.Instance.OnBattleDefeatEvent += HandleBattleDefeat;
        }

        // 初始进入大地图状态
        ChangeState(new MapState(this));
    }

    void OnDestroy()
    {
        // 养成好习惯，销毁时取消监听
        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.OnBattleVictoryEvent -= HandleBattleVictory;
            BattleManager.Instance.OnBattleDefeatEvent -= HandleBattleDefeat;
        }
    }

    // =========================================================
    // 核心：状态切换引擎
    // =========================================================
    public void ChangeState(IGameState newState)
    {
        if (_currentState != null)
        {
            _currentState.Exit(); // 让老状态清理现场
        }

        _currentState = newState;
        Debug.Log($"<color=green>游戏状态切换至: {newState.GetType().Name}</color>");
        _currentState.Enter(); // 让新状态开始工作
    }

    // =========================================================
    // 玩家输入传递
    // =========================================================
    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
        // 直接把点击事件丢给当前状态去处理，控制器本身不管具体逻辑
        // _currentState?.OnSlotClicked(slotData, uiPos);
    }

    // =========================================================
    // 事件响应逻辑
    // =========================================================
    private void HandleBattleVictory()
    {
        // 战斗胜利后触发抽卡，抽完卡回到大地图状态
        // 这里直接调用我们写好的 StartDraftProcess 方法即可
        StartDraftProcess(() => ChangeState(new MapState(this)));
    }

    private void HandleBattleDefeat()
    {
        ShowRunSummary(false);
    }

    public void ShowRunSummary(bool isVictory)
    {
        if (RunTracker.Instance != null) RunTracker.Instance.isVictory = isVictory;
        if (_runSummaryUI != null) _runSummaryUI.ShowSummary();
    }
    
    // =========================================================
    // 房间路由逻辑 (Room Routing)
    // =========================================================
    public void EnterRoom(RoomDataSO roomData)
    {
        if (roomData == null)
        {
            Debug.LogError("试图进入的房间数据为空！");
            return;
        }

        Debug.Log($"<color=cyan>GameFlow: 准备进入房间 -> {roomData.roomName} ({roomData.roomType})</color>");
        if (RunTracker.Instance != null) RunTracker.Instance.roomsVisited++;

        // 核心：根据房间类型，分配不同的状态对象！控制器本身不再处理具体逻辑。
        switch (roomData.roomType)
        {
            case Enum.RoomType.Battle:
            case Enum.RoomType.Elite:
            case Enum.RoomType.Boss:
                if (roomData is BattleRoomSO battleData)
                {
                    // 切换到战斗状态
                    ChangeState(new BattleState(this, battleData));
                }
                break;

            case Enum.RoomType.Event:
                if (roomData is EventRoomSO eventData)
                {
                    // 切换到事件状态
                    ChangeState(new EventState(this, eventData));
                }
                break;

            case Enum.RoomType.Shop:
                // 未来扩展：ChangeState(new ShopState(this, roomData));
                Debug.Log("商店状态尚未实现，暂回大地图");
                ChangeState(new MapState(this));
                break;

            case Enum.RoomType.Rest:
            case Enum.RoomType.Treasure:
                // 未来扩展：ChangeState(new RestState(this, roomData));
                Debug.Log($"{roomData.roomType} 状态尚未实现，暂回大地图");
                ChangeState(new MapState(this));
                break;

            default:
                Debug.LogWarning($"未处理的房间类型: {roomData.roomType}");
                ChangeState(new MapState(this));
                break;
        }
    }
    // 触发抽卡 (外部调用，比如升级后)
    public void StartDraftProcess(Action onComplete = null)
    {
        // 适配新的状态机：切换到法术抽卡状态，并将你的回调函数传给它
        ChangeState(new SpellDraftState(this, onComplete));
    }
}