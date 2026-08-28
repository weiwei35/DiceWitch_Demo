using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(PhysicsDice))]
public class DiceDragger : MonoBehaviour
{
    private PhysicsDice physicsDice;
    private Rigidbody rb;
    private Collider _collider; // 缓存碰撞体引用

    public bool isDragging = false;
    public bool IsDragging => isDragging;
    
    public DiceSquadGroup squadGroup; // 我属于哪个小队？
    private Vector3 originalPos; 
    private Quaternion originalRot;
    
    void Awake()
    {
        physicsDice = GetComponent<PhysicsDice>();
        rb = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>(); // 或者是 GetComponentInChildren<Collider>()，视你Prefab结构而定
        
        originalPos = transform.position;
        originalRot = transform.rotation;
    }

    // 提供给外部控制物理的接口
    public void SetKinematic(bool state)
    {
        if(rb != null) rb.isKinematic = state;
    }

    public void CaptureTrayPose()
    {
        originalPos = transform.position;
        originalRot = transform.rotation;
    }

    // --- 手动输入入口 (由 DiceInputManager 调用) ---

    public void OnManualMouseDown()
    {
        if (physicsDice.isRolling) return;
        TooltipSystem.Instance?.Hide();

        // 记录归位点
        originalPos = transform.position;
        originalRot = transform.rotation;
        
        if (squadGroup != null)
        {
            squadGroup.OnSquadDragStart(this);
        }
        else 
        {
            isDragging = true;
            rb.isKinematic = true;
        }
    }

    public void OnManualMouseDrag()
    {
        if (squadGroup != null)
        {
            UpdateTargetingArrow();
        }
        else
        {
            if (!isDragging) return;
            
            UpdateTargetingArrow();
        }
    }

    public void OnManualMouseUp()
    {
        if (squadGroup != null)
        {
            BattleTarget target = GetTargetUnderMouse_MainCamera();
            squadGroup.OnSquadDragEnd(target);
        }
        else
        {
            if (!isDragging) return;
            isDragging = false;
            
            // 隐藏箭头
            TargetingArrow.Instance.Hide();
            
            // 检测是否松开在了目标上
            CheckDrop();
        }
    }

    // --- 核心辅助逻辑 ---

    private BattleTarget GetTargetUnderMouse_MainCamera()
    {
        // --- 1. 先检测 UI (玩家) ---
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.mousePosition;

        List<RaycastResult> uiResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, uiResults);

        foreach (var result in uiResults)
        {
            // 看看 UI 上有没有挂 PlayerUITarget
            PlayerUITarget uiTarget = result.gameObject.GetComponent<PlayerUITarget>();
            if (uiTarget != null)
            {
                return uiTarget;
            }
        }

        // --- 2. 再检测 3D (敌人) ---
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        int mask = (1 << LayerMask.NameToLayer("Enemy")); // 只检测敌人层即可，因为玩家在UI层

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, mask))
        {
            return hit.collider.GetComponent<BattleTarget>();
        }

        return null;
    }

    // --- 结算逻辑 ---

    void CheckDrop()
    {
        BattleTarget target = GetTargetUnderMouse_MainCamera();
        
        if (target != null)
        {
            Debug.Log("命中目标！");
            BattleManager.Instance?.NotifyPlayerDiceTargeted(target);
            
            //通知战斗管理器，使用了一个骰子
            if (BattleManager.Instance != null)
            {
                BattleManager.Instance.TriggerPlayerUseDice();
            }
            
            DiceFaceData data = physicsDice.GetCurrentData();
            int usedOrder = BattleManager.Instance != null ? BattleManager.Instance.diceUsedThisTurn : 1;
            int remaining = DiceThrower.Instance.GetValidDiceCount();
            // 视觉效果：让骰子飞过去撞击
            StartCoroutine(FlyAndHit(target, data, usedOrder, remaining));
        }
        else
        {
            // 没打中，归位
            ReturnToTray();
        }
    }

    public void ReturnToTray()
    {
        ReturnToTray(false);
    }

    public void ReturnToTray(bool keepKinematic)
    {
        if (this == null || gameObject == null) return;

        transform.position = originalPos;
        transform.rotation = originalRot;
        
        physicsDice.StopMotionAndSetKinematic(keepKinematic);
        
        isDragging = false;
    }

    // --- 箭头绘制 (跨次元) ---
    public void UpdateTargetingArrow()
    {
        // --- 1. 计算视觉起点 (简化版) ---
        // 我们不需要去算复杂的屏幕坐标再转回来。
        // 骰子盘是 Camera 模式的 UI，它就在 3D 世界里！
        // 我们直接把骰子在 DiceCamera 里的相对位置，映射到 RawImage 在 MainCamera 前的世界位置。

        DiceViewMonitor monitor = DiceInputManager.Instance != null
            ? DiceInputManager.Instance.diceViewMonitor
            : DiceViewMonitor.Instance;
        if (monitor == null) return;
        Vector3 uiWorldPos = monitor.GetWorldPositionFromDice(transform.position);

        // D. 稍微往摄像机反方向拉一点点，防止穿模
        Vector3 arrowStart = uiWorldPos + new Vector3(0, 0, -2);

        // --- 2. 计算终点 (保持不变) ---
        Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 arrowEnd;
        BattleTarget target = GetTargetUnderMouse_MainCamera();
        if (target != null)
        {
            arrowEnd = target.transform.position;
        }
        else
        {
            // 简单的地面检测
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (groundPlane.Raycast(mouseRay, out float enter)) arrowEnd = mouseRay.GetPoint(enter);
            else arrowEnd = mouseRay.GetPoint(10f);
        }
        
        TargetingArrow.Instance.Show(arrowStart, arrowEnd);
    }

    // --- 攻击与飞行 ---

    public IEnumerator FlyAndHit(BattleTarget target, DiceFaceData damageData, int usedOrder, int remainingAtThrow)
    {
        // 1. 准备阶段：冻结物理
        isDragging = false;
        rb.isKinematic = true;
        // 关闭碰撞，防止飞行途中撞到玩家的 CharacterController 或其他东西
        if(GetComponent<Collider>()) GetComponent<Collider>().enabled = false;

        // =================================================================
        // 🪄 核心修改：偷天换日 (Teleport & Layer Switch)
        // =================================================================

        // A. 计算视觉起点 (和抛物线起点的算法一模一样)
        Vector3 viewportPos = DiceViewMonitor.Instance.diceCamera.WorldToViewportPoint(transform.position);
        
        Vector3[] corners = new Vector3[4];
        DiceViewMonitor.Instance.rectTrans.GetWorldCorners(corners);
        
        Vector3 bottomEdge = Vector3.Lerp(corners[0], corners[3], viewportPos.x);
        Vector3 topEdge = Vector3.Lerp(corners[1], corners[2], viewportPos.x);
        Vector3 uiWorldPos = Vector3.Lerp(bottomEdge, topEdge, viewportPos.y);

        // B. 瞬移：把骰子直接搬到摄像机面前
        // 稍微往里面推一点点 (+ forward * 0.2f)，防止穿插进摄像机近裁剪面导致看不见
        Vector3 visualStartPos = uiWorldPos + Camera.main.transform.forward * 0.2f;
        transform.position = visualStartPos;

        // C. 换层：让主相机能看见它
        // 必须把子物体(Mesh, Text)也一起换了，否则你看不到模型和数字
        SetLayerRecursively(gameObject, LayerMask.NameToLayer("Default"));

        // =================================================================

        float duration = 0.35f; // 飞行稍微快一点，打击感更强
        float timer = 0f;

        Vector3 endPos = target.transform.position + Vector3.up * 0.5f; 

        // 贝塞尔曲线控制点
        Vector3 midPoint = (visualStartPos + endPos) / 2;
        // 弧度不用太高，因为现在是从屏幕射出去的
        Vector3 controlPoint = midPoint + Vector3.up * Vector3.Distance(visualStartPos, endPos) * 0.2f;

        Vector3 initialScale = transform.localScale; // 记录当前的 UI 尺寸
        Vector3 targetScale = Vector3.one;

        while (timer < duration)
        {
            if (target == null) 
            {
                Destroy(gameObject);
                yield break;
            }

            timer += Time.deltaTime;
            float t = timer / duration;

            // 移动
            Vector3 m1 = Vector3.Lerp(visualStartPos, controlPoint, t);
            Vector3 m2 = Vector3.Lerp(controlPoint, endPos, t);
            transform.position = Vector3.Lerp(m1, m2, t);

            // 旋转：疯狂旋转
            transform.Rotate(new Vector3(360, 180, 90) * Time.deltaTime * 5f);

            // (可选) 视觉优化：从 UI 出来时可以由小变大，或者保持原样
            transform.localScale = Vector3.Lerp(initialScale, targetScale, t);

            yield return null;
        }

        // --- 撞击结算 (代码保持不变) ---
        if (target != null)
        {
            // 1. 伤害副本
            DiceFaceData calculatedData = new DiceFaceData();
            calculatedData.value = damageData.TotalValue;
            calculatedData.icon = damageData.icon;
            calculatedData.color = damageData.color;
            calculatedData.effectDescription = damageData.effectDescription;

            // 2. 能力修饰
            var abilities = physicsDice.GetAbilities();
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    calculatedData.value = ability.OnCalculateDamage(calculatedData.value, target);
                }
            }
            var forgeSlots = physicsDice.GetActiveForgeSlots();
            if (forgeSlots != null && forgeSlots.Count > 0)
            {
                Debug.Log($"<color=#FF8800>【锻造结算】找到 {forgeSlots.Count} 个生效词条, 基础伤害: {calculatedData.value}</color>");
                foreach (var slot in forgeSlots)
                {
                    int before = calculatedData.value;
                    calculatedData.value = slot.affix.OnCalculateDamage(calculatedData.value, target, physicsDice);
                    Debug.Log($"<color=#FF8800>  词条 [{slot.affix.affixName}] OnCalculateDamage: {before} → {calculatedData.value}</color>");
                }
            }
            else
            {
                Debug.Log($"<color=#888888>【锻造结算】无生效词条 (sourceDataRef={physicsDice.sourceDataRef != null}, forgeSlots count={physicsDice.sourceDataRef?.forgeSlots?.Count})</color>");
            }
            if (BattleManager.Instance != null)
            {
                calculatedData.value = BattleManager.Instance.ProcessGlobalDamageModifiers(calculatedData.value, usedOrder, remainingAtThrow);
            }
            // 3. 造成伤害
            target.OnHit(calculatedData);

            // 4. 击后效果
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    ability.OnPostHit(target, calculatedData.value, physicsDice);
                }
            }
            if (forgeSlots != null)
            {
                foreach (var slot in forgeSlots)
                {
                    slot.affix.OnPostHit(target, calculatedData.value, physicsDice);
                }
            }
        }
        DiceThrower.Instance?.HandlePlayerDiceResolved(physicsDice, physicsDice.PhysicalValue);
        Destroy(gameObject);
    }

    // --- 辅助方法：递归修改层级 ---
    void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}
