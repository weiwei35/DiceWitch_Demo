using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Explosion")]
public class Ability_Explosion : DiceAbilitySO
{
    public GameObject vfxPrefab; // 爆炸特效

    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        // 1. 播放特效
        if (vfxPrefab) Instantiate(vfxPrefab, target.transform.position, Quaternion.identity);

        // 2. 检测周围敌人 (使用 Physics.OverlapSphere)
        List<EnemyTarget> enemyies = BattleManager.Instance.enemies;
        int aoeDamage = Mathf.FloorToInt(finalDamage * 0.5f); // 50% 溅射

        foreach (var enemy in enemyies)
        {
            if (enemy != target)
            {
                enemy.ApplyDirectValue(aoeDamage);
            }
        }
        Debug.Log($"爆炸造成了 {enemyies.Count - 1} 个目标的溅射伤害！");
    }
}