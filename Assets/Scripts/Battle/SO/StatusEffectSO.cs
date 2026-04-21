using UnityEngine;

// 这是一个抽象基类
public abstract class StatusEffectSO : ScriptableObject
{
    [Header("Basic Info")]
    public string statusName; // "燃烧"
    public Sprite icon;       // 图标
    public Color color = Color.white; // 图标颜色
    public bool isDebuff = true; // 是增益还是减益
    
    [TextArea] public string description = "描述文本";

    // =========================================================================
    // 【新增】标准战斗管线生命周期 (Damage Pipeline Hooks)
    // =========================================================================

    // 1. 回合开始时触发
    public virtual void OnTurnStart(BattleTarget target, int stacks) {}

    // 2. 回合结束时触发
    public virtual void OnTurnEnd(BattleTarget target, int stacks) {}

    // 3. 【攻击前】拦截并修饰发出的伤害
    public virtual void OnBeforeAttack(BattleTarget target, DamageInfo info, int stacks) {}

    // 4. 【受击前】拦截并修饰受到的伤害（在这里做格挡、免疫、减伤、反伤判定）
    public virtual void OnBeforeDefend(BattleTarget target, DamageInfo info, int stacks) {}

    // 5. 【受击后】真正扣血后触发（在这里做吸血、受伤回怒、链枷分摊等）
    public virtual void OnAfterTakeDamage(BattleTarget target, DamageInfo info, int stacks) {}

    // =========================================================================
    // 【保留】你原有的特定游戏机制钩子 (Custom Mechanics Hooks)
    // =========================================================================
    
    // 当玩家使用骰子时触发 (如：成长机制)
    public virtual void OnPlayerUseDice(BattleTarget target, int stacks) {}
    
    // 当玩家获得护甲时触发 (如：互斥机制)
    public virtual void OnPlayerGainArmor(BattleTarget target, int armorAmount, int stacks) {}

    // 全局物理骰子计算时的干预 (如：错位机制，干预特定顺序的骰子)
    public virtual int OnGlobalCalculateDamage(BattleTarget target, int incomingDamage, int usedOrder, int remainingAtThrow) 
    {
        return incomingDamage; // 默认不修改
    }

    // =========================================================================
    // 动态描述
    // =========================================================================
    public virtual string GetDescription(int stacks)
    {
        try
        {
            return string.Format(description, stacks);
        }
        catch
        {
            return description;
        }
    }
}