using UnityEngine;

[CreateAssetMenu(menuName = "Status/Growth (Permanent)")]
public class Status_GrowthSO : StatusEffectSO
{
    [Header("Growth Config")]
    public int damagePerDice = 1; // 每颗骰子永久加多少攻击
    public override void OnPlayerUseDice(BattleTarget target, int stacks)
    {
        if (target is EnemyTarget enemy)
        {
            enemy.AddGrowth(damagePerDice * stacks);
        }
    }
}