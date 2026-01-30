using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/AOE Damage (Splash)")]
public class Ability_AOE : DiceAbilitySO
{
    [Header("AOE Settings")]
    [Tooltip("全场伤害倍率 (0.5 = 所有人都受到 50% 点数的伤害)")]
    public float damageRatio = 0.5f;

    [Header("Visuals")]
    public GameObject explosionVFX;

    // --- 第一步：修改主目标的伤害 ---
    // 这个方法会在产生伤害前被调用，返回值是最终施加给“被撞到的那个敌人”的伤害
    public override int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice)
    {
        // 直接把即将造成的物理伤害打折
        int reducedDamage = Mathf.FloorToInt(baseDamage * damageRatio);
        
        // 至少 1 点
        return Mathf.Max(1, reducedDamage);
    }

    // --- 第二步：波及其他敌人 ---
    // 这里的 finalDamage 已经是上面 OnCalculateDamage 修改过的值了 (也就是 50% 的值)
    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        Debug.Log($"全场 AOE 触发！每人受到 {finalDamage} 点伤害");

        // 1. 播放特效
        if (explosionVFX != null)
        {
            // 在场地中心或者受击者位置播放特效
            Instantiate(explosionVFX, target.transform.position, Quaternion.identity);
        }

        // 2. 遍历敌人
        var allEnemies = BattleManager.Instance.enemies;

        foreach (var enemy in allEnemies)
        {
            // 跳过无效目标
            if (enemy == null || enemy.currentHp <= 0) continue;

            // 【关键】跳过“主目标”
            // 为什么？因为主目标刚刚已经吃了一次“物理撞击伤害”了
            // 而那个物理伤害在上面的 OnCalculateDamage 里已经被我们改成了 50%
            // 所以这里只需要处理“没被撞到的人”
            if (enemy == target) continue;

            // 3. 施加同等伤害
            enemy.ApplyDirectValue(finalDamage);
        }
    }
}