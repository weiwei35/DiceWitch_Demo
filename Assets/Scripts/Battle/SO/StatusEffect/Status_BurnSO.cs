using UnityEngine;

[CreateAssetMenu(menuName = "Status/Burn")]
public class Status_BurnSO : StatusEffectSO
{
    public override void OnTurnStart(EnemyTarget target, int stacks)
    {
        // 1. 造成伤害
        Debug.Log($"<color=orange>燃烧生效！造成 {stacks} 点伤害</color>");
        
        // 注意：这里我们调用 ApplyDamage 而不是 TakeDamage，防止无限递归触发状态
        target.ApplyDirectDamage(stacks); 

        // 2. 减少层数 (比如每次回合开始层数 -1)
        target.ApplyStatus(this, -1);
    }
}