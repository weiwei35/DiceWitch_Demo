using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; // 需要用到协程

public class EnemyTarget : BattleTarget
{
    [Header("Stats")]
    public int maxHp = 50;
    public int currentHp;
    public TextMeshPro hpText; // 拖入显示血量的3D Text
    
    [Header("Intent")]
    public int nextDamageValue; // 下回合要打多少
    public TextMeshPro intentText; // 拖入头顶的一个新的 3D Text
    
    public Transform statusPanel; // 在敌人头顶放一个 Horizontal Layout Group
    public GameObject statusIconPrefab; // 状态图标的预制体
    
    private Vector3 originalPosition;
    // 存储当前身上的状态：Key=状态配置, Value=层数
    private Dictionary<StatusEffectSO, int> currentStatuses = new Dictionary<StatusEffectSO, int>();
    
    // UI缓存，避免每次都Destroy重建
    private Dictionary<StatusEffectSO, GameObject> statusUIMap = new Dictionary<StatusEffectSO, GameObject>();
    void Start()
    {
        team = TargetTeam.Enemy;
        // 初始设为极小
        transform.localScale = Vector3.zero;
        // 弹出来的动画 (0.5秒内变回原大小)
        transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
        currentHp = maxHp;
        UpdateUI();
        // 游戏开始时先随机一个意图
        PlanNextMove();
        originalPosition = transform.position;
    }

    // --- 1. 策划阶段：决定下回合干嘛 ---
    public void PlanNextMove()
    {
        // 这里写你的AI逻辑，比如随机攻击 5-10
        // nextDamageValue = Random.Range(5, 11);
        
        // 更新头顶UI显示意图
        if (intentText != null)
        {
            intentText.text = $"A: {nextDamageValue}";
            intentText.color = Color.red;
        }
    }

    // --- 2. 行动阶段：真正的攻击 ---
    public IEnumerator ExecuteAction()
    {
        // 播放攻击动画（这里用简单的位移模拟）
        Vector3 originalPos = transform.position;
        Vector3 targetPos = transform.position + Vector3.back * 1.5f; // 往前冲一点

        // 冲出去
        float t = 0;
        while(t < 0.1f) { transform.position = Vector3.Lerp(originalPos, targetPos, t/0.1f); t+=Time.deltaTime; yield return null; }
        
        // 造成伤害
        PlayerManager.Instance.TakeDamage(nextDamageValue);

        // 回来
        t = 0;
        while(t < 0.2f) { transform.position = Vector3.Lerp(targetPos, originalPos, t/0.2f); t+=Time.deltaTime; yield return null; }
    }
    void UpdateUI()
    {
        if(hpText != null) hpText.text = $"HP: {currentHp}";
    }
    void Die()
    {
        DOTween.Kill(transform);
        // 死了要通知 BattleManager 从列表中移除自己，否则报错
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
            }
        }
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
        // 如果还没有这个状态的图标，就生成一个
        if (!statusUIMap.ContainsKey(status))
        {
            GameObject iconObj = Instantiate(statusIconPrefab, statusPanel);
            statusUIMap.Add(status, iconObj);
        }

        // 更新图标显示 (假设Prefab里有 Image 和 TextMeshProUGUI)
        GameObject ui = statusUIMap[status];
        
        // 设置图片
        var img = ui.GetComponent<UnityEngine.UI.Image>();
        if(img) { img.sprite = status.icon; img.color = status.color; }
        
        // 设置层数文字
        var text = ui.GetComponentInChildren<TextMeshProUGUI>();
        if(text) text.text = currentStatuses[status].ToString();
    }

    public override void TakeDamage(DiceFaceData damageData)
    {
        // 这里可以判断类型，比如必须是 Attack 类型才扣血
        if (damageData.type == DiceActionType.Attack)
        {
            currentHp -= damageData.value;
            Debug.Log($"<color=red>敌人受到 {damageData.value} 点伤害！剩余HP: {currentHp}</color>");
            
            // 播放受击动画/特效 (Juice)
            // 1. 立刻杀掉当前正在进行的任何抖动，防止叠加
            transform.DOKill(true); 

            // 2. 【关键】强制瞬间回到原点
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

    public override void ApplyDirectValue(int value)
    {
        ApplyDirectDamage(value);
    }
}