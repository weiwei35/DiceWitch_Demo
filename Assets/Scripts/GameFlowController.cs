using System;
using UnityEngine;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance;

    [Header("UI Managers")]
    [SerializeField] private MagicCircleDisplay _circleDisplay;
    [SerializeField] private SpellDraftPanel _draftPanel;
    [SerializeField] private AttributeDraftPanel _attrDraftPanel;
    [SerializeField] private MiniActionMenu _miniMenu;
    [SerializeField] private EventUIManager _eventUIManager; // 事件 UI 管理器
    [Header("Map UI")]
    [SerializeField] private GameObject _mapPanel; // 整个地图界面的根节点
    [SerializeField] private GameObject _roomUIRoot; // 战斗界面的根节点

    [Header("Visual Feedback")]
    [SerializeField] private GameObject _selectionModeTip; // 比如显示一行字："请选择目标槽位..."

    // 【新增】存储外部传入的回调函数
    private Action _onDraftProcessComplete;

    private Enum.GameState _currentState = Enum.GameState.Idle;
    
    // 缓存数据：当前正在等待镶嵌的法术
    private DiceAbilitySO _pendingAbilityToBind;
    private MagicCircleSlot _pendingSlotForUpgrade;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 初始化状态
        EnterMapState();
    }

    // =========================================================
    // 状态切换逻辑 (State Machine)
    // =========================================================

    public void EnterIdleState()
    {
        _currentState = Enum.GameState.Idle;
        _pendingAbilityToBind = null;

        // UI 重置
        _draftPanel.Hide();
        if (_selectionModeTip) _selectionModeTip.SetActive(false);
        
        // 刷新魔法阵显示 (确保最新数据)
        _circleDisplay.RefreshAll();
    }
    public void EnterMapState()
    {
        _currentState = Enum.GameState.Map; // 需要去 Enum 定义里加一个 Map
        
        // 显隐控制
        if(_mapPanel) _mapPanel.SetActive(true);
        if(_roomUIRoot) _roomUIRoot.SetActive(false);
        
        // 通知地图绘制器刷新
        // FindObjectOfType<MapViewController>()?.DrawMap();
    }
    public void EnterRoom(RoomDataSO roomData)
    {
        if (roomData == null)
        {
            Debug.LogError("试图进入的房间数据为空！");
            return;
        }

        Debug.Log($"<color=cyan>GameFlow: 准备处理房间事件 -> {roomData.roomName} ({roomData.roomType})</color>");

        // 根据房间类型，切换到不同的状态和UI
        switch (roomData.roomType)
        {
            // --- 战斗类房间 ---
            case Enum.RoomType.Battle:
            case Enum.RoomType.Elite:
            case Enum.RoomType.Boss:
                // 将基类强转为子类并进入战斗状态
                BattleRoomSO battleData = roomData as BattleRoomSO;
                if (battleData != null)
                {
                    EnterBattleState(battleData);
                }
                else
                {
                    Debug.LogError("房间类型是战斗，但数据不是 BattleRoomSO！");
                }
                break;

            // --- 非战斗类房间 (UI 交互类) ---
            case Enum.RoomType.Shop:
            case Enum.RoomType.Rest:
            case Enum.RoomType.Treasure:
            case Enum.RoomType.Event:
                EnterNonBattleState(roomData, roomData.roomType);
                break;

            default:
                Debug.LogWarning($"未处理的房间类型: {roomData.roomType}");
                break;
        }
    }
    public void EnterBattleState(BattleRoomSO roomData)
    {
        _currentState = Enum.GameState.Idle; // 战斗里的 Idle 状态

        // 1. UI 开关
        if(_mapPanel) _mapPanel.SetActive(false);
        if(_roomUIRoot) _roomUIRoot.SetActive(true);

        // 2. 通知 BattleManager 开打 (直接调用，不用 LoadScene)
        BattleManager.Instance.StartNewBattle(roomData);
    }
    public void EnterNonBattleState(RoomDataSO data, Enum.RoomType type)
    {
        Debug.Log($"打开 {type} 面板: {data.name}");

        if (type == Enum.RoomType.Event && data is EventRoomSO eventRoom)
        {
            // 防空检查
            if (_eventUIManager == null)
            {
                Debug.LogError("GameFlowController 中未绑定 EventUIManager！");
                EnterMapState();
                return;
            }
            if(_mapPanel) _mapPanel.SetActive(false);
            if(_roomUIRoot) _roomUIRoot.SetActive(true);
            // 从事件房间配置的“可能事件列表”中，随机抽取一个事件
            if (eventRoom.possibleEvents.Count > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, eventRoom.possibleEvents.Count);
                RandomEventSO randomEvent = eventRoom.possibleEvents[randomIndex];

                // 呼出事件 UI，并传入回调函数：当事件结束时，回到大地图状态
                _eventUIManager.ShowEvent(randomEvent, () => 
                {
                    Debug.Log("事件处理完毕，返回大地图。");
                    EnterMapState();
                    // 这里可以通知 MapDiceThrower 允许再次投掷地图骰子
                });
            }
            else
            {
                Debug.LogWarning("这个事件房间没有配置任何事件！直接无事发生返回。");
                EnterMapState();
            }
        }
        else if (type == Enum.RoomType.Shop)
        {
            // TODO: _shopPanel.SetActive(true);
        }
    }

    // --- 流程入口 1：触发抽卡 (外部调用，比如升级后) ---
    public void StartDraftProcess(Action onComplete = null)
    {
        if (_currentState != Enum.GameState.Idle) return;

        // 1. 存下回调：等会儿完事了要执行这个
        _onDraftProcessComplete = onComplete;

        _currentState = Enum.GameState.Drafting;
        _draftPanel.OnSpellSelected = OnSpellSelectedFromDraft;
        _draftPanel.ShowDraft();
    }

    // --- 流程入口 2：点击了槽位 (由 MagicSlotUI 调用) ---
    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
        switch (_currentState)
        {
            // 场景 A：平时没事干 -> 打开 Mini Menu 进行升级/查看属性
            case Enum.GameState.Idle:
                OpenMiniMenu(slotData, uiPos);
                break;

            // 场景 B：抽完卡，正在选人 -> 执行附魔
            case Enum.GameState.TargetSelection:
                TryBindAbilityToSlot(slotData);
                break;

            // 其他状态（比如正在抽卡中），忽略点击
            case Enum.GameState.Drafting:
                break;
        }
    }

    // ---原本的 OpenMiniMenu 保持不变，变成私有方法供上面调用即可---
    private void OpenMiniMenu(MagicCircleSlot slot, Vector3 pos)
    {
        _miniMenu.Show(slot, pos);
    }

    // =========================================================
    // 具体业务逻辑
    // =========================================================

    // 2. 处理抽卡选择
    private void OnSpellSelectedFromDraft(DiceAbilitySO selectedSpell)
    {
        _currentState = Enum.GameState.TargetSelection;
        _pendingAbilityToBind = selectedSpell;

        // 【建议】通知 Display 进入“选择模式”（比如让所有圈圈闪烁，或者鼠标变成法术图标）
        FindObjectOfType<MagicCircleDisplay>().SetSelectionMode(true);
        
        Debug.Log("请选择目标槽位...");
    }

    // 3. 处理附魔逻辑
    private void TryBindAbilityToSlot(MagicCircleSlot slot)
    {
        // ... 之前的校验逻辑 (未解锁/为空) ...
        if (!slot.isUnlocked || slot.currentDice == null) return;

        // 1. 执行附魔
        PlayerProgressionManager.Instance.ImprintAbilityToDice(slot.currentDice, _pendingAbilityToBind);
        Debug.Log("附魔成功！");

        // 2. 回到空闲
        FindObjectOfType<MagicCircleDisplay>().SetSelectionMode(false);
        EnterIdleState();

        // 3. 【关键】如果有回调，说明这是战斗中途的抽卡，执行它！
        if (_onDraftProcessComplete != null)
        {
            // 执行回调 (通知 BattleManager)
            _onDraftProcessComplete.Invoke();
            
            // 清空回调，防止下次普通操作误触发
            _onDraftProcessComplete = null;
        }
    }
    // --- 入口：开始属性附魔流程 (由 DetailPanel 调用) ---
    public void StartAttributeEnchantProcess(MagicCircleSlot targetSlot)
    {
        int cost = 10; // 建议从配置读取

        // 检查资源
        if (PlayerProgressionManager.Instance.TrySpendManaDust(cost))
        {
            // >> 成功逻辑 <<
            
            // 2. 记录目标
            _pendingSlotForUpgrade = targetSlot;
            
            // 3. 切换状态
            _currentState = Enum.GameState.Drafting;
            
            // 4. 打开抽卡
            _attrDraftPanel.OnAttributeSelected = OnAttributeSelectedFromDraft;
            _attrDraftPanel.ShowDraft();
        }
        else
        {
            // >> 失败逻辑 <<
            Debug.LogWarning("资源不足，无法注入！");
            
            // 关键：状态保持不变 (ViewingDetails)，面板保持打开
            // 这里可以加一个 UI 震动或者文字提示告诉玩家没钱了
        }
    }
    // --- 2. 处理属性升级 ---
    public void UpgradeSlotAttribute(MagicCircleSlot targetSlot)
    {
        int cost = 5; // 建议根据等级动态计算

        if (PlayerProgressionManager.Instance.TrySpendManaDust(cost))
        {
            // 执行升级
            PlayerProgressionManager.Instance.Debug_UpgradeSlotAttribute(targetSlot.slotID);
            
            // 刷新显示
            _circleDisplay.RefreshAll();
            
            // 关键：因为还在详情页，状态不需要变，只需要刷新详情面板的数据
            
            Debug.Log("升级成功！");
        }
        else
        {
            Debug.LogWarning("资源不足，无法升级！");
            // 面板保持打开，不乱动
        }
    }
    // --- 回调：玩家选好了属性 ---
    private void OnAttributeSelectedFromDraft(SlotAttributeSO selectedAttr)
    {
        if (_pendingSlotForUpgrade != null)
        {
            // 执行注入
            PlayerProgressionManager.Instance.Debug_SetSlotAttribute(_pendingSlotForUpgrade.slotID, selectedAttr);
            
            Debug.Log("属性注入成功！");
            
            // 刷新显示
            _circleDisplay.RefreshAll();
        }
        
        // 归位
        _pendingSlotForUpgrade = null;
        EnterIdleState(); 
    }
}