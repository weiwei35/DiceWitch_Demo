using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Status/Chain Link")]
public class Status_ChainLinkSO : StatusEffectSO
{
    [Header("Chain Config")]
    public float transferRatio = 0.5f; // 传递 50%
    public GameObject damageVFX;       // 传递伤害时的特效

    // 逻辑：当宿主受伤时，分摊给别人
    public override void OnPostTakeDamage(EnemyTarget target, int incomingDamage, int stacks, bool isChainReaction)
    {
        // 1. 只有非连锁伤害才触发传播 (防止 A炸B，B炸A 的死循环)
        if (isChainReaction || incomingDamage <= 0) return;

        // 2. 计算传递伤害
        int spreadDamage = Mathf.FloorToInt(incomingDamage * transferRatio);
        if (spreadDamage < 1) return;

        // 3. 获取其他敌人
        List<EnemyTarget> enemies = BattleManager.Instance.enemies;
        
        foreach (var enemy in enemies)
        {
            // 跳过自己，跳过死人
            if (enemy == target || enemy == null || enemy.currentHp <= 0) continue;

            // 4. 施加伤害 (关键：标记 isChainReaction = true)
            enemy.ApplyDirectValue(spreadDamage, true);
            
            // 播放特效
            if (damageVFX != null) 
                Instantiate(damageVFX, enemy.transform.position, Quaternion.identity);
        }

        Debug.Log($"{target.name} 触发链枷，全场分摊 {spreadDamage} 伤害");
    }

    // 逻辑：回合开始时自动移除 (通常 Debuff 持续 1 回合)
    public override void OnTurnStart(EnemyTarget target, int stacks)
    {
        // 移除所有层数 (或者 -1)
        target.ApplyStatus(this, -stacks); 
    }
}