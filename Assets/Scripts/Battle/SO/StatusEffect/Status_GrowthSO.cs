using UnityEngine;

[CreateAssetMenu(menuName = "Status/Growth (Permanent)")]
public class Status_GrowthSO : StatusEffectSO
{
    [Header("Growth Config")]
    public int damagePerDice = 1; // 每颗骰子永久加多少攻击

    public override void OnPlayerUseDice(EnemyTarget target, int stacks)
    {
        // 计算增量
        int amount = damagePerDice * stacks;

        // 调用永久成长接口
        target.AddGrowth(amount);

        Debug.Log($"<color=orange>{target.name} 正在成长... 永久攻击力 +{amount}</color>");
    }
}