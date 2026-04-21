using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "DiceWitch/Status/Chain Link")]
public class Status_ChainLinkSO : StatusEffectSO
{
    [Header("Chain Config")]
    public float transferRatio = 0.5f; 
    public GameObject damageVFX;       

    public override void OnAfterTakeDamage(BattleTarget target, DamageInfo info, int stacks)
    {
        // 【重构】利用管线的标签：如果这个伤害本身就是反弹的，或者持续掉血，绝不二次传播！
        if (info.DmgType == DamageType.Reflect || info.DmgType == DamageType.StatusDOT || info.FinalDamage <= 0) return;

        int spreadDamage = Mathf.FloorToInt(info.FinalDamage * transferRatio);
        if (spreadDamage < 1) return;

        List<EnemyTarget> enemies = BattleManager.Instance.enemies;
        
        foreach (var enemy in enemies)
        {
            if (enemy == target || enemy == null || enemy.currentHp <= 0) continue;

            // 【重构】打包新的连锁伤害，类型标记为 Reflect (防止连锁循环)
            DamageInfo chainDmg = new DamageInfo(target, enemy, spreadDamage, DamageType.Reflect);
            BattleManager.Instance.ProcessDamage(chainDmg);
            
            if (damageVFX != null) 
                Instantiate(damageVFX, enemy.transform.position, Quaternion.identity);
        }

        Debug.Log($"{target.name} 触发链枷，全场分摊 {spreadDamage} 伤害");
    }

    public override void OnTurnStart(BattleTarget target, int stacks)
    {
        if (target is EnemyTarget enemy) enemy.ApplyStatus(this, -stacks); 
    }
}