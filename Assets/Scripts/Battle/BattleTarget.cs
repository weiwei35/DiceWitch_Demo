using UnityEngine;

public abstract class BattleTarget : MonoBehaviour
{
    public GameEnums.TargetTeam team; // 属于哪一队
    public string targetName;    // 方便 Debug 打印名字

    // =========================================================
    // 触发入口：由物理骰子抛出调用
    // =========================================================
    public virtual void OnHit(DiceFaceData data, BattleTarget attacker = null)
    {
        switch (data.type)
        {
            case GameEnums.DiceActionType.Attack:
                // 【核心改动】不再直接扣血，而是打包丢给 BattleManager 的流水线！
                DamageInfo info = new DamageInfo(attacker, this, data.value, DamageType.Normal);
                BattleManager.Instance.ProcessDamage(info);
                break;
                
            case GameEnums.DiceActionType.Defend: 
                GainArmor(data.value);
                break;
                
            case GameEnums.DiceActionType.Magic:
                break;
        }
    }

    // 兼容原有的“直接造成伤害”逻辑（例如以前的连锁伤害）
    public virtual void ApplyDirectValue(int value, bool isChainReaction = false)
    {
        DamageType type = isChainReaction ? DamageType.Reflect : DamageType.Normal;
        DamageInfo info = new DamageInfo(null, this, value, type);
        BattleManager.Instance.ProcessDamage(info);
    }

    // =========================================================
    // 管线必须调用的底层接口 (交由具体的 EnemyTarget / Player 去实现)
    // =========================================================
    
    // 遍历自身 Status，调用 OnBeforeAttack
    public abstract void TriggerBeforeAttack(DamageInfo info);
    
    // 遍历自身 Status，调用 OnBeforeDefend
    public abstract void TriggerBeforeDefend(DamageInfo info);
    
    // 遍历自身 Status，调用 OnAfterTakeDamage
    public abstract void TriggerAfterTakeDamage(DamageInfo info);
    
    // 纯粹的数值扣减与死亡判定（剥离了所有的 Buff 判断）
    public abstract void ExecuteDamage(int actualDamage);

    // =========================================================
    // 原有的其他抽象方法
    // =========================================================
    public abstract void ApplyStatus(StatusEffectSO status, int amount);
    public abstract void GainArmor(int amount);
}