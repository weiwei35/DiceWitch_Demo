using UnityEngine;

public class DiceInputManager : MonoBehaviour
{
    public static DiceInputManager Instance;
    
    // 当前正在拖拽的骰子
    private DiceDragger _currentDragger;
    private DiceHover _currentHover;

    void Awake() { Instance = this; }

    void Update()
    {
        HandleInput();
        HandleHover();
    }

    void HandleInput()
    {
        // --- 1. 按下鼠标：尝试抓取骰子 ---
        if (Input.GetMouseButtonDown(0))
        {
            // 获取转换后的射线
            Ray ray = DiceViewMonitor.Instance.GetDiceRay(Input.mousePosition);
            
            // 在骰子世界 (DiceWorld Layer) 进行检测
            int layerMask = 1 << LayerMask.NameToLayer("DiceArea");
            
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
            {
                // 看看是不是打中了骰子
                DiceDragger dragger = hit.collider.GetComponentInParent<DiceDragger>();
                if (dragger != null)
                {
                    _currentDragger = dragger;
                    // 手动通知骰子被点击了
                    dragger.OnManualMouseDown(); 
                }
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
                _currentDragger.OnManualMouseUp();
                _currentDragger = null;
            }
        }
    }
    void HandleHover()
    {
        // 骰子还在滚动中，跳过射线检测防止唤醒物理睡眠
        if (DiceThrower.Instance != null && DiceThrower.Instance.IsAnyDiceRolling())
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

        // 1. 获取转换后的射线
        Ray ray = DiceViewMonitor.Instance.GetDiceRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("DiceArea");

        // 2. 射线检测
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, layerMask))
        {
            DiceHover hitHover = hit.collider.GetComponentInParent<DiceHover>();
            
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
}