using UnityEngine;[CreateAssetMenu(menuName = "Status/Damage Threshold (Block low damage)")]
public class Status_ThresholdSO : StatusEffectSO
{
    [Header("Threshold Settings")]
    [Tooltip("最小受击阈值。小于此值的伤害将被免疫。")]
    public int minDamageRequired = 4;

    public GameObject blockVFX; // 格挡/弹开时的特效 (可选)

    public override int OnTakeDamage(EnemyTarget target, int incomingDamage, int stacks)
    {
        // 如果原本就没有伤害，直接跳过
        if (incomingDamage <= 0) return 0;

        // 【核心逻辑】检查伤害是否达标
        if (incomingDamage < minDamageRequired)
        {
            Debug.Log($"<color=gray>【阈值】触发！{incomingDamage} 点伤害被免疫 (要求 >= {minDamageRequired})</color>");
            
            // 播放格挡特效 (比如一个护盾闪烁)
            if (blockVFX != null)
            {
                Instantiate(blockVFX, target.transform.position, Quaternion.identity);
            }

            // 返回 0，意味着伤害被完全抹除了
            return 0; 
        }

        // 如果 >= 4，正常承受伤害
        return incomingDamage;
    }

    // 重写描述，使其支持动态显示阈值数值
    public override string GetDescription(int stacks)
    {
        try
        {
            // 假设你的 description 配置为："仅接受大于等于 {0} 点的单次伤害。"
            return string.Format(description, minDamageRequired);
        }
        catch
        {
            return description;
        }
    }
}