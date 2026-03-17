using UnityEngine;

[CreateAssetMenu(menuName = "Status/DPS Check (Regen if low damage)")]
public class Status_DPSCheckSO : StatusEffectSO
{
    [Header("DPS Check Config")]
    public int damageThreshold = 10; // 需要达到的伤害阈值

    public override void OnTurnStart(EnemyTarget target, int stacks)
    {
        // 1. 如果满血，无需回血，直接跳过
        if (target.currentHp >= target.maxHp) return;

        // 2. 检查本回合受到的总伤害是否达标
        if (target.damageTakenThisRound < damageThreshold)
        {
            int healAmount = target.maxHp - target.currentHp;
            
            Debug.Log($"<color=green>【DPS检测失败】{target.name} 本回合仅受到 {target.damageTakenThisRound} 伤害 (需>={damageThreshold})，恢复全部生命值！</color>");
            
            // 触发回血
            target.Heal(healAmount);
        }
        else
        {
            Debug.Log($"【DPS检测通过】{target.name} 本回合受到 {target.damageTakenThisRound} 伤害，压制了它的再生！");
        }
    }

    // 动态描述
    public override string GetDescription(int stacks)
    {
        try
        {
            // 描述填："如果单回合内受到的总伤害低于 {0} 点，在回合结束时恢复所有生命值。"
            return string.Format(description, damageThreshold);
        }
        catch
        {
            return description;
        }
    }
}