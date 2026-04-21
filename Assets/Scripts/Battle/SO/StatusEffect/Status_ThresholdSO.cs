using UnityEngine;

[CreateAssetMenu(menuName = "DiceWitch/Status/Damage Threshold")]
public class Status_ThresholdSO : StatusEffectSO
{
    public int minDamageRequired = 4;
    public GameObject blockVFX;

    public override void OnBeforeDefend(BattleTarget target, DamageInfo info, int stacks)
    {
        // 核心逻辑：如果在结算前的预测伤害不达标，直接将包裹里的最终伤害抹除
        if (info.FinalDamage > 0 && info.FinalDamage < minDamageRequired)
        {
            Debug.Log($"<color=gray>【阈值】触发！{info.FinalDamage} 点伤害被免疫 (要求 >= {minDamageRequired})</color>");
            
            // 抹零伤害，管线后续拿到 0 就不会执行实际扣血了
            info.FinalDamage = 0; 

            if (blockVFX != null)
                Instantiate(blockVFX, target.transform.position, Quaternion.identity);
        }
    }
}