using UnityEngine;

public class DiceInputManager : MonoBehaviour
{
    public static DiceInputManager Instance;
    public DiceViewMonitor diceViewMonitor;
    public bool logDiceInputDebug = false;
    
    // 当前正在拖拽的骰子
    private DiceDragger _currentDragger;
    private DiceHover _currentHover;
    public bool IsDragging => _currentDragger != null && _currentDragger.IsDragging;

    void Awake() { Instance = this; }

    void Update()
    {
        HandleInput();
        HandleHover();
    }

    void HandleInput()
    {
        if (DiceThrower.Instance != null && !DiceThrower.Instance.AreDiceReadyForInput)
            return;

        // --- 1. 按下鼠标：尝试抓取骰子 ---
        if (Input.GetMouseButtonDown(0))
        {
            TooltipSystem.Instance?.Hide();

            DiceViewMonitor monitor = GetDiceViewMonitor();
            if (monitor == null) return;

            PhysicsDice dice = monitor.GetDiceUnderScreenPoint(Input.mousePosition);
            DiceDragger dragger = dice != null ? dice.GetComponent<DiceDragger>() : null;
            if (dragger != null && dragger.enabled)
            {
                _currentDragger = dragger;
                dragger.OnManualMouseDown();
                if (logDiceInputDebug) Debug.Log($"抓取骰子: {dragger.name}");
            }
            else if (logDiceInputDebug)
            {
                Debug.Log("点击骰子盘，但没有命中 DiceDragger。");
            }
        }

        // --- 2. 拖拽中：通知骰子更新位置 ---
        if (_currentDragger != null)
        {
            // 持续调用拖拽逻辑
            _currentDragger.OnManualMouseDrag();

            // --- 3. 松开鼠标：释放 ---
            if (Input.GetMouseButtonUp(0))
            {
                TooltipSystem.Instance?.Hide();
                _currentDragger.OnManualMouseUp();
                _currentDragger = null;
            }
        }
    }

    void HandleHover()
    {
        // 骰子滚动或法术响应中，跳过射线检测防止唤醒物理睡眠
        if (DiceThrower.Instance != null && !DiceThrower.Instance.AreDiceReadyForInput)
        {
            if (_currentHover != null)
            {
                _currentHover.OnManualMouseExit();
                _currentHover = null;
            }
            return;
        }

        // 如果正在拖拽中，就不要检测悬浮了，防止 Tips 乱跳
        if (_currentDragger != null) 
        {
            if (_currentHover != null)
            {
                _currentHover.OnManualMouseExit();
                _currentHover = null;
            }
            return;
        }

        DiceViewMonitor monitor = GetDiceViewMonitor();
        if (monitor == null) return;

        PhysicsDice dice = monitor.GetDiceUnderScreenPoint(Input.mousePosition);
        if (dice != null)
        {
            DiceHover hitHover = dice.GetComponent<DiceHover>();
            
            // 如果指到了一个新的骰子
            if (hitHover != _currentHover)
            {
                // 离开旧的
                if (_currentHover != null) _currentHover.OnManualMouseExit();
                
                // 进入新的
                _currentHover = hitHover;
                if (_currentHover != null) _currentHover.OnManualMouseEnter();
            }
        }
        else
        {
            // 指到了空地
            if (_currentHover != null)
            {
                _currentHover.OnManualMouseExit();
                _currentHover = null;
            }
        }
    }

    private DiceViewMonitor GetDiceViewMonitor()
    {
        if (diceViewMonitor != null) return diceViewMonitor;
        if (DiceViewMonitor.Instance != null)
        {
            diceViewMonitor = DiceViewMonitor.Instance;
            return diceViewMonitor;
        }

        DiceViewMonitor[] monitors = FindObjectsOfType<DiceViewMonitor>();
        foreach (var monitor in monitors)
        {
            if (monitor != null && monitor.gameObject.name == "UI_DiceView")
            {
                diceViewMonitor = monitor;
                return diceViewMonitor;
            }
        }

        if (monitors.Length > 0)
            diceViewMonitor = monitors[0];

        return diceViewMonitor;
    }
}
