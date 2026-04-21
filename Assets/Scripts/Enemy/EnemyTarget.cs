using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

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
    
    [Header("Run Info")]
    public Enum.EnemyTier tier = Enum.EnemyTier.Normal; // 默认普通怪
    
    [Header("Stats")]
    public int maxHp = 50;
    public int currentHp;
    public TextMeshPro hpText; // 拖入显示血量的3D Text
    
    [Header("Intent")]
    public int nextDamageValue; // 下回合要打多少
    private int _permanentGrowthValue; //累计成长值
    
    // 最终伤害 = 基础 + 成长
    public int CurrentFinalDamage => nextDamageValue + _permanentGrowthValue;
    public TextMeshPro intentText; // 拖入头顶的一个新的 3D Text
    public List<EnemyStatusConfig> initialStatusConfigs; 
    public Transform statusPanel; // 在敌人头顶放一个 Horizontal Layout Group
    public GameObject statusIconPrefab; // 状态图标的预制体
    
    [Header("Runtime Links")]
    public EnemyTarget soulLinkPartner; // 当前绑定的灵魂链接伙伴
    
    [Header("Rewards")]
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

    // =========================================================
    // 阶段逻辑
    // =========================================================
    public void PlanNextMove()
    {
        // 更新头顶UI显示意图
        if (intentText != null)
        {
            intentText.text = $"A: {CurrentFinalDamage}";
            intentText.color = Color.red;
        }
        
        damageTakenThisRound = 0; 
        UpdateIntentUI();
    }

    public IEnumerator ExecuteAction()
    {
        int damageDeal = CurrentFinalDamage;

        if (damageDeal <= 0)
        {
            Debug.Log($"<color=gray>{name} 攻击力为 0，放弃了攻击行动。</color>");
            transform.DOPunchScale(new Vector3(0.05f, -0.05f, 0), 0.2f);
            yield return new WaitForSeconds(0.2f);
            yield break; 
        }

        Vector3 originalPos = transform.position;
        
        // 攻击前摇 (震动/冲刺)
        transform.DOShakePosition(0.5f, 0.5f);
        yield return new WaitForSeconds(0.5f);
        
        // 造成伤害
        PlayerManager.Instance.TakeDamage(damageDeal);
        Debug.Log($"{name} 攻击玩家，造成 {damageDeal} 点伤害");

        // 如果这一击杀死了玩家，记录凶手
        if (PlayerManager.Instance.currentHp <= 0 && RunTracker.Instance != null)
        {
            string roomName = BattleManager.Instance.CurrentRoomData != null ? BattleManager.Instance.CurrentRoomData.roomName : "未知房间";
            string cleanKillerName = this.gameObject.name.Replace("(Clone)", ""); 
            RunTracker.Instance.RecordDeath(roomName, cleanKillerName);
        }
        
        // 攻击后摇
        yield return new WaitForSeconds(0.2f);
    }

    // =========================================================
    // 基础属性与UI
    // =========================================================
    void UpdateUI()
    {
        if(hpText != null) hpText.text = $"HP: {currentHp}";
    }

    void UpdateIntentUI()
    {
        if (intentText != null) intentText.text = $"A: {CurrentFinalDamage}";
    }

    void Die()
    {
        if (RunTracker.Instance != null) RunTracker.Instance.AddKill(tier);
        if (BattleManager.Instance != null) BattleManager.Instance.OnPlayerUseDice -= HandlePlayerDiceUsed;
        
        if (soulLinkPartner != null)
        {
            soulLinkPartner.soulLinkPartner = null; 
            soulLinkPartner = null;
        }

        DOTween.Kill(transform);
        BattleManager.Instance.RemoveEnemy(this);
        Destroy(gameObject);
    }

    // =========================================================
    // 状态管理
    // =========================================================
    public override void ApplyStatus(StatusEffectSO status, int amount)
    {
        if (status == null) return;

        if (currentStatuses.ContainsKey(status))
            currentStatuses[status] += amount;
        else
            currentStatuses.Add(status, amount);

        if (currentStatuses[status] <= 0)
            RemoveStatus(status);
        else
            UpdateStatusUI(status);
    }

    void RemoveStatus(StatusEffectSO status)
    {
        if (currentStatuses.ContainsKey(status))
        {
            currentStatuses.Remove(status);
            
            if (statusUIMap.ContainsKey(status))
            {
                Destroy(statusUIMap[status]);
                statusUIMap.Remove(status);
                LayoutRebuilder.ForceRebuildLayoutImmediate(statusPanel as RectTransform);
            }
        }
    }

    void UpdateStatusUI(StatusEffectSO status)
    {
        if (!statusUIMap.ContainsKey(status))
        {
            GameObject iconObj = Instantiate(statusIconPrefab, statusPanel);
            statusUIMap.Add(status, iconObj);
        }

        GameObject uiObj = statusUIMap[status];
        StatusIconUI iconScript = uiObj.GetComponent<StatusIconUI>();
        
        if (iconScript != null)
        {
            iconScript.Setup(status, currentStatuses[status]);
        }
        else
        {
            var img = uiObj.GetComponent<UnityEngine.UI.Image>();
            if(img) { img.sprite = status.icon; img.color = status.color; }
            var text = uiObj.GetComponentInChildren<TextMeshProUGUI>();
            if(text) text.text = currentStatuses[status].ToString();
        }
    }

    // =========================================================
    // 原有的特有机制钩子
    // =========================================================
    private void HandlePlayerDiceUsed(int amount)
    {
        if (currentHp <= 0) return;
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys)
            status.OnPlayerUseDice(this, currentStatuses[status]);
    }

    public void OnTurnStart()
    {
        if (initialStatusConfigs != null)
        {
            foreach (var config in initialStatusConfigs)
            {
                if (config.resetPerTurn && config.status != null)
                {
                    int currentStack = currentStatuses.ContainsKey(config.status) ? currentStatuses[config.status] : 0;
                    if (currentStack < config.initialStacks)
                    {
                        ApplyStatus(config.status, config.initialStacks - currentStack);
                    }
                }
            }
        }
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys) status.OnTurnStart(this, currentStatuses[status]);
    }

    public override void GainArmor(int amount) { /* currentArmor += amount; */ }

    public int ProcessGlobalDamageModifiers(int rawDamage, int usedOrder, int remainingAtThrow)
    {
        int finalDamage = rawDamage;
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys)
            finalDamage = status.OnGlobalCalculateDamage(this, finalDamage, usedOrder, remainingAtThrow);
        return finalDamage;
    }

    public void HandlePlayerGainArmor(int amount)
    {
        if (currentHp <= 0 || currentStatuses.Count == 0) return;
        var keys = new List<StatusEffectSO>(currentStatuses.Keys);
        foreach (var status in keys) status.OnPlayerGainArmor(this, amount, currentStatuses[status]);
    }

    public void AddGrowth(int amount)
    {
        _permanentGrowthValue += amount;
        UpdateIntentUI();
        if (intentText != null)
        {
            intentText.transform.DOKill();
            intentText.transform.localScale = Vector3.one;
            intentText.transform.DOPunchScale(Vector3.one * 0.4f, 0.2f);
            intentText.color = Color.red; 
        }
    }

    public void Heal(int amount)
    {
        if (currentHp <= 0 || currentHp >= maxHp) return; 
        currentHp = Mathf.Min(currentHp + amount, maxHp);
        UpdateUI();
        transform.DOKill(true);
        transform.DOPunchScale(Vector3.one * 0.3f, 0.5f);
        if (DamageNumberManager.Instance != null) DamageNumberManager.Instance.ShowHeal(transform.position, amount); 
    }

    // =========================================================
    // 【核心】管线钩子实现 (适配 Dictionary 结构)
    // =========================================================
    private List<StatusEffectSO> _statusCache = new List<StatusEffectSO>();

    public override void TriggerBeforeAttack(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys); 
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnBeforeAttack(this, info, stacks);
        }
    }

    public override void TriggerBeforeDefend(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys);
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnBeforeDefend(this, info, stacks);
        }
    }

    public override void TriggerAfterTakeDamage(DamageInfo info)
    {
        _statusCache.Clear();
        _statusCache.AddRange(currentStatuses.Keys);
        for (int i = _statusCache.Count - 1; i >= 0; i--)
        {
            if (currentStatuses.TryGetValue(_statusCache[i], out int stacks))
                _statusCache[i].OnAfterTakeDamage(this, info, stacks);
        }
    }

    // =========================================================
    // 【核心】管线终点：最终执行伤害 (合并了以前飘字和震动等表现逻辑)
    // =========================================================
    public override void ExecuteDamage(int actualDamage)
    {
        // 1. 扣减血量并记录本回合受到的伤害
        currentHp -= actualDamage;
        damageTakenThisRound += actualDamage;
        
        Debug.Log($"<color=red>{gameObject.name} 受到 {actualDamage} 点最终伤害！剩余 HP: {currentHp}</color>");
        
        // 2. 触发受伤事件 (兼容你以前的代码)
        if (actualDamage > 0)
        {
            OnDamageTaken?.Invoke(actualDamage);
        }

        // 3. 伤害飘字
        if (actualDamage > 0 && DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamage(transform.position, actualDamage, false);
        }

        // 4. UI更新与视觉震动
        UpdateUI();
        transform.DOKill(true); 
        transform.position = originalPosition;
        transform.DOShakePosition(0.5f, 0.5f);

        // 5. 死亡判定
        if (currentHp <= 0)
        {
            Die();
        }
    }
}