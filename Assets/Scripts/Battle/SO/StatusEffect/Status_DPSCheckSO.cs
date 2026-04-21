using UnityEngine;

[CreateAssetMenu(menuName = "Status/DPS Check (Regen if low damage)")]
public class Status_DPSCheckSO : StatusEffectSO
{
    [Header("DPS Check Config")]
    public int damageThreshold = 10; // 需要达到的伤害阈值

    public override void OnTurnStart(BattleTarget target, int stacks)
    {
        if (target is EnemyTarget enemy)
        {
            // 1. 如果满血，无需回血，直接跳过
            if (enemy.currentHp >= enemy.maxHp) return;

            // 2. 检查本回合受到的总伤害是否达标
            if (enemy.damageTakenThisRound < damageThreshold)
            {
                int healAmount = enemy.maxHp - enemy.currentHp;

                Debug.Log(
                    $"<color=green>【DPS检测失败】{enemy.name} 本回合仅受到 {enemy.damageTakenThisRound} 伤害 (需>={damageThreshold})，恢复全部生命值！</color>");

                // 触发回血
                enemy.Heal(healAmount);
            }
            else
            {
                Debug.Log($"【DPS检测通过】{enemy.name} 本回合受到 {enemy.damageTakenThisRound} 伤害，压制了它的再生！");
            }
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