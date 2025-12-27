using UnityEngine;

// 这是一个抽象基类，不能直接创建实例，只能继承
public abstract class DiceAbilitySO : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description; // 能力描述，显示在UI上

    // --- 定义钩子 (Hooks) ---
    // 这里的 virtual 方法是空的，子类按需重写（Override）即可

    // 1. 当骰子物理停下，数值确定时调用
    // 返回值：修改后的数值 (如果不修改就返回 primitiveValue)
    public virtual int OnRollEnd(int primitiveValue) 
    {
        return primitiveValue;
    }

    // 2. 当准备计算伤害时调用
    // 可以修改最终伤害
    public virtual int OnCalculateDamage(int baseDamage, BattleTarget target)
    {
        return baseDamage;
    }

    // 3. 当已经造成伤害后调用 (用于加Buff、特效等)
    public virtual void OnPostHit(BattleTarget target, int finalDamage)
    {
        // 默认啥也不做
    }

    public virtual void OnRollFinished(PhysicsDice sourceDice)
    {
        
    }
}