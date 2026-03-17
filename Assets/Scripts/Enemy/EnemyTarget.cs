using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI; // 需要用到协程
[System.Serializable]
public struct EnemyStatusConfig
{
    public StatusEffectSO status;
    public int initialStacks;      // 初始几层？
    public bool resetPerTurn;      // 是否每回合重置到这个层数？
}
public class EnemyTarget : BattleTarget
{
    // 1. 定义一个事件：当收到伤害时触发 (参数：收到的伤害值)
    public event System.Action<int> OnDamageTaken;
    [Header("Stats")]
    public int maxHp = 50;
    public int currentHp;
    public TextMeshPro hpText; // 拖入显示血量的3D Text
    
    [Header("Intent")]
    public int nextDamageValue; // 下回合要打多少
    private int _permanentGrowthValue;//累计成长值
    // 最终伤害 = 基础 + 成长
    public int CurrentFinalDamage => nextDamageValue + _permanentGrowthValue;
    public TextMeshPro intentText; // 拖入头顶的一个新的 3D Text
    public List<EnemyStatusConfig> initialStatusConfigs; 
    public Transform statusPanel; // 在敌人头顶放一个 Horizontal Layout Group
    public GameObject statusIconPrefab; // 状态图标的预制体
    [Header("Runtime Links")]
    // 【新增】当前绑定的灵魂链接伙伴
    public EnemyTarget soulLinkPartner;
    [Header("Rewards")]
    public int xpReward = 5;
    public int manaDustReward = 10;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    // 记录本回合受到的总伤害
    public int damageTakenThisRound { get; private set; } = 0;
    // 存储当前身上的状态：Key=状态配置, Value=层数
    private Dictionary<StatusEffectSO, int> currentStatuses = new Dictionary<StatusEffectSO, int>();
    
    // UI缓存，避免每次都Destroy重建
    private Dictionary<StatusEffectSO, GameObject> statusUIMap = new Dictionary<StatusEffectSO, GameObject>();
    void Start()
    {
        team = Enum.TargetTeam.Enemy;
        // 初始设为极小
        originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
        // 弹出来的动画 (0.5秒内变回原大小)
        transform.DOScale(originalScale, 0.5f).SetEase(Ease.OutBack);
        currentHp = maxHp;
        UpdateUI();
        //初始化附加状态
        _permanentGrowthValue = 0;
        if (initialStatusConfigs != null)
        {
            foreach (var config in initialStatusConfigs)
            {
                if (config.status != null && config.initialStacks > 0)
                {
                    ApplyStatus(config.status, config.initialStacks);
                }
            }
        }
        // 游戏开始时先随机一个意图
        PlanNextMove();
        originalPosition = transform.position;
        
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnPlayerUseDice += HandlePlayerDiceUsed;
    }

    // --- 1. 策划阶段：决定下回合干嘛 ---
    public void PlanNextMove()
    {
        // 这里写你的AI逻辑，比如随机攻击 5-10
        // nextDamageValue = Random.Range(5, 11);
        
        // 更新头顶UI显示意图
        if (intentText != null)
        {
            intentText.text = $"A: {CurrentFinalDamage}";
            intentText.color = Color.red;
        }
        
        damageTakenThisRound = 0; 
        
        UpdateIntentUI();
    }

    // --- 2. 行动阶段：真正的攻击 ---
    public IEnumerator ExecuteAction()
    {
        // 1. 获取最终计算出的伤害
        int damageDeal = CurrentFinalDamage;

        // =========================================================
        // 检查攻击力是否为 0
        // =========================================================
        if (damageDeal <= 0)
        {
            Debug.Log($"<color=gray>{name} 攻击力为 0，放弃了攻击行动。</color>");
            
            // 可选：播放一个“发呆”或“无奈”的小动效，让玩家知道轮到它了但它没打
            transform.DOPunchScale(new Vector3(0.05f, -0.05f, 0), 0.2f);
            
            // 稍微停顿极短的时间，让玩家看清它跳过了，然后直接退出协程
            yield return new WaitForSeconds(0.2f);
            yield break; 
        }
        // =========================================================

        // 2. 如果攻击力 > 0，才执行正常的攻击动画和伤害逻辑
        Vector3 originalPos = transform.position;
        
        // 攻击前摇 (震动/冲刺)
        transform.DOShakePosition(0.5f, 0.5f);
        yield return new WaitForSeconds(0.5f);
        
        // 造成伤害
        PlayerManager.Instance.TakeDamage(damageDeal);
        Debug.Log($"{name} 攻击玩家，造成 {damageDeal} 点伤害 (基础{nextDamageValue} + 成长{_permanentGrowthValue})");

        // 攻击后摇
        yield return new WaitForSeconds(0.2f);
    }
    void UpdateUI()
    {
        if(hpText != null) hpText.text = $"HP: {currentHp}";
    }
    void Die()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnPlayerUseDice -= HandlePlayerDiceUsed;// 如果我有伙伴，告诉伙伴我挂了，断开链接
        if (soulLinkPartner != null)
        {
            soulLinkPartner.soulLinkPartner = null; // 对方也清空
            soulLinkPartner = null;
        }

        DOTween.Kill(transform);
        BattleManager.Instance.RemoveEnemy(this);
        Destroy(gameObject);
    }
    public override void ApplyStatus(StatusEffectSO status, int amount)
    {
        if (status == null) return;

        // 1. 更新数据
        if (currentStatuses.ContainsKey(status))
        {
            currentStatuses[status] += amount;
        }
        else
        {
            currentStatuses.Add(status, amount);
        }

        // 2. 检查是否归零
        if (currentStatuses[status] <= 0)
        {
            RemoveStatus(status);
        }
        else
        {
            // 3. 更新UI
            UpdateStatusUI(status);
        }
    }

    void RemoveStatus(StatusEffectSO status)
    {
        if (currentStatuses.ContainsKey(status))
        {
            currentStatuses.Remove(status);
            
            // 移除UI
            if (statusUIMap.ContainsKey(status))
            {
                Destroy(statusUIMap[status]);
                statusUIMap.Remove(status);
                LayoutRebuilder.ForceRebuildLayoutImmediate(statusPanel as RectTransform);
            }
        }
    }
    // 事件处理函数
    private void HandlePlayerDiceUsed(int amount)
    {
        // 只有活着的时候才成长
        if (currentHp <= 0) return;

        // 遍历所有状态触发钩子
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys)
        {
            status.OnPlayerUseDice(this, currentStatuses[status]);
        }
    }
    // --- 核心逻辑：伤害预处理 ---
    // 遍历所有状态，让它们有机会修改或拦截伤害
    private int ProcessDamageModifiers(int rawDamage)
    {
        int finalDamage = rawDamage;
        
        // 复制 Keys 防止遍历时修改字典报错
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        
        foreach (var status in keys)
        {
            // 调用 StatusEffectSO.OnTakeDamage
            // 如果状态想免疫伤害，它会返回 0
            finalDamage = status.OnTakeDamage(this, finalDamage, currentStatuses[status]);
            
            // 如果伤害已经被减为0了，通常后面的状态也不用跑了 (看具体需求，这里先继续跑)
        }
        
        return finalDamage;
    }

    // --- 辅助：造成直接伤害 (不触发受击特效/反伤等) ---
    public void ApplyDirectDamage(int dmg)
    {
        currentHp -= dmg;
        transform.DOShakePosition(0.2f, 0.2f); 
        UpdateUI(); // 记得刷新血条
        if(currentHp <= 0) Die();
    }
    
    // --- 钩子插入点 ---
    
    // 1. 在 BattleManager 调用敌人回合开始时调用此方法
    public void OnTurnStart()
    {
        // 1. 【新增】重置/再生机制
        // 这一步必须在处理具体状态逻辑之前执行
        if (initialStatusConfigs != null)
        {
            foreach (var config in initialStatusConfigs)
            {
                // 如果配置了每回合重置
                if (config.resetPerTurn && config.status != null)
                {
                    // 获取当前层数 (如果没有就是 0)
                    int currentStack = 0;
                    if (currentStatuses.ContainsKey(config.status))
                    {
                        currentStack = currentStatuses[config.status];
                    }

                    // 如果当前层数少于目标层数，补齐
                    if (currentStack < config.initialStacks)
                    {
                        int amountToAdd = config.initialStacks - currentStack;
                        ApplyStatus(config.status, amountToAdd);
                        Debug.Log($"<color=cyan>{name} 的 {config.status.statusName} 自动重置/再生了 {amountToAdd} 层。</color>");
                    }
                }
            }
        }
        // 遍历所有状态 (复制一份Key防止在遍历时修改字典报错)
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys)
        {
            status.OnTurnStart(this, currentStatuses[status]);
        }
    }

    // --- UI 更新逻辑 ---
    void UpdateStatusUI(StatusEffectSO status)
    {
        // 1. 如果还没有这个状态的图标，就生成一个
        if (!statusUIMap.ContainsKey(status))
        {
            GameObject iconObj = Instantiate(statusIconPrefab, statusPanel);
            statusUIMap.Add(status, iconObj);
        }

        // 2. 获取图标对象
        GameObject uiObj = statusUIMap[status];
        
        // 3. 【修改】获取交互脚本并初始化
        StatusIconUI iconScript = uiObj.GetComponent<StatusIconUI>();
        
        // 如果你的 Prefab 上还没挂这个脚本，先尝试 Get 现在的逻辑做兼容，但建议去编辑器挂上
        if (iconScript != null)
        {
            iconScript.Setup(status, currentStatuses[status]);
        }
        else
        {
            // (兜底逻辑，防止你还没去改 Prefab 报错)
            // 建议删掉下面这块，直接去 Prefab 挂脚本
            var img = uiObj.GetComponent<UnityEngine.UI.Image>();
            if(img) { img.sprite = status.icon; img.color = status.color; }
            var text = uiObj.GetComponentInChildren<TextMeshProUGUI>();
            if(text) text.text = currentStatuses[status].ToString();
        }
    }

    public override void TakeDamage(DiceFaceData damageData)
    {
        if (damageData.type == Enum.DiceActionType.Attack)
        {
            // 1. 扣血
            int damage = damageData.value;
            damage = ProcessDamageModifiers(damage);
            if (damage <= 0)
            {
                Debug.Log("伤害被格挡/免疫！");
                // 也可以在这里播一个 "Block" 的特效或飘字
                if (DamageNumberManager.Instance != null) 
                    DamageNumberManager.Instance.ShowDamage(transform.position, 0, false);
                return; 
            }
            currentHp -= damage;
            damageTakenThisRound += damage;
            if (damage > 0 && DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowDamage(transform.position, damage, false);
            }
            // 触发受伤事件！
            // 这样 DamageLinkStatus 才能监听到这次物理攻击
            if (damage > 0)
            {
                OnDamageTaken?.Invoke(damage);
            }
            if (damage > 0) TriggerStatusOnDamage(damage, false); // 物理攻击肯定不是连锁

            Debug.Log($"<color=red>敌人受到 {damage} 点伤害！剩余HP: {currentHp}</color>");
            
            // ... (原本的动画代码) ...
            transform.DOKill(true); 
            transform.position = originalPosition;
            transform.DOShakePosition(0.5f, 0.5f);
        }
        else
        {
            Debug.Log("这个骰子不是攻击类型！");
        }

        if (currentHp <= 0) Die();
        UpdateUI();
    }

    public override void GainArmor(int amount)
    {
        // currentArmor += amount;
    }

    public override void ApplyDirectValue(int value,bool isChainReaction = false)
    {
        // 简单的扣血逻辑
        int damageToTake = value;
        if (!isChainReaction) 
        {
            damageToTake = ProcessDamageModifiers(value);
        }
        if (damageToTake <= 0) return; // 被免疫了
        // 如果有护甲逻辑，在这里处理
        // if (currentArmor > 0) ... 

        currentHp -= damageToTake;
        damageTakenThisRound += damageToTake;
        UpdateUI(); // 刷新血条
        
        // 飘字特效
        if (damageToTake > 0 && DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamage(transform.position, damageToTake, isChainReaction);
        }
        
        // 【新增】如果不是连锁反应造成的伤害，且伤害大于0，则广播事件
        // 这样可以防止：A连B，B连A，导致无限死循环炸机
        if (!isChainReaction && value > 0)
        {
            OnDamageTaken?.Invoke(value);
        }
        if (value > 0) TriggerStatusOnDamage(value, isChainReaction);
        if (currentHp <= 0)
        {
            // 调用死亡逻辑
            // 如果是 EnemyTarget，可能需要通知 BattleManager
            if (this is EnemyTarget enemy) 
            {
                BattleManager.Instance.RemoveEnemy(enemy);
                Destroy(gameObject); // 或者播放死亡动画后销毁
            }
        }
    }
    private void TriggerStatusOnDamage(int damage, bool isChainReaction)
    {
        if (currentStatuses.Count == 0) return;

        // 复制 Keys 防止在遍历中修改字典导致报错
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys)
        {
            status.OnPostTakeDamage(this, damage, currentStatuses[status], isChainReaction);
        }
    }
    public void AddGrowth(int amount)
    {
        _permanentGrowthValue += amount;
        
        // 实时刷新 UI，并播放跳动动画
        UpdateIntentUI();
        if (intentText != null)
        {
            intentText.transform.DOKill();
            intentText.transform.localScale = Vector3.one;
            intentText.transform.DOPunchScale(Vector3.one * 0.4f, 0.2f);
            intentText.color = Color.red; 
        }
    }
    // =========================================================
    // 回血方法 (供状态系统调用)
    // =========================================================
    public void Heal(int amount)
    {
        if (currentHp <= 0 || currentHp >= maxHp) return; // 死了或者满血就不加了

        currentHp += amount;
        if (currentHp > maxHp) currentHp = maxHp; // 防止溢出

        UpdateUI();

        // 视觉反馈：变绿、放大
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.3f, 0.5f);
        
        // 可选：利用你现有的跳字系统显示绿色的回复数字
        // 如果你的 DamagePopup 还不支持绿色，下面教你稍微改一下
        if (DamageNumberManager.Instance != null)
        {
            // 传个负数或者你可以单独写个 ShowHeal 方法，这里假设用 -amount 表示回血以便区分
            DamageNumberManager.Instance.ShowHeal(transform.position, amount); 
        }
    }
    void UpdateIntentUI()
    {
        if (intentText != null)
        {
            // 显示总伤害
            intentText.text = $"A: {CurrentFinalDamage}";
        }
    }
}