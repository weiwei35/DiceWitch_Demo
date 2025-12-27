using UnityEngine;

// 这是一个抽象基类
public abstract class StatusEffectSO : ScriptableObject
{
    [Header("Basic Info")]
    public string statusName; // "燃烧"
    public Sprite icon;       // 图标
    public Color color = Color.white; // 图标颜色
    public bool isDebuff = true; // 是增益还是减益

    // --- 状态的钩子函数 (Hooks) ---
    
    // 1. 回合开始时触发 (比如：燃烧扣血)
    public virtual void OnTurnStart(EnemyTarget target, int stacks) {}

    // 2. 回合结束时触发 (比如：自动减少层数)
    public virtual void OnTurnEnd(EnemyTarget target, int stacks) {}

    // 3. 当受到伤害时触发 (比如：易伤增加伤害)
    // 返回值：修改后的伤害
    public virtual int OnTakeDamage(int incomingDamage, int stacks) 
    {
        return incomingDamage;
    }
}