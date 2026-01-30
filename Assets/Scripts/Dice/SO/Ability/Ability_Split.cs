using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Split Effect")]
public class Ability_Split : DiceAbilitySO
{
    [Header("Split Settings")]
    public GameObject projectilePrefab; // 拖入 MiniDiceProjectile
    public float damageMultiplier = 0.5f; // 伤害衰减系数
    public int minDamage = 1; // 最小伤害阈值

    // 1. 钩子：当主骰子击中敌人后触发 (起点)
    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice = null)
    {
        // 第一次分裂：传入 null，表示还没有现存的投射物，需要生成一个新的
        TrySpawnNextSplit(target.transform.position, target, finalDamage, null);
    }

    // 2. 核心逻辑：尝试生成下一次分裂
    // existingProjectile: 上一次飞过来的投射物 (如果是第一次则为 null)
    public void TrySpawnNextSplit(Vector3 originPos, BattleTarget currentVictim, int currentDamage, SplittingProjectile existingProjectile)
    {
        // 计算下一跳伤害
        int nextDamage = Mathf.FloorToInt(currentDamage * damageMultiplier);

        // --- 终止条件 A：伤害太低 或 没配置 Prefab ---
        if (nextDamage < minDamage || projectilePrefab == null)
        {
            // 如果链条断了，且有一个还在飞的投射物，需要销毁它 (因为它完成了使命)
            if (existingProjectile != null) Destroy(existingProjectile.gameObject);
            return; 
        }

        // 寻找下一个受害者
        BattleTarget nextTarget = BattleManager.Instance.GetRandomTarget(currentVictim); 

        // --- 终止条件 B：找不到目标 ---
        if (nextTarget == null)
        {
            // 同上，没目标了就销毁
            if (existingProjectile != null) Destroy(existingProjectile.gameObject);
            return;
        }

        // --- 继续分裂/飞行 ---
        SplittingProjectile script = existingProjectile;

        // 情况 1: 如果没有现成的投射物 (第一次分裂)，实例化一个新的
        if (script == null)
        {
            GameObject projObj = Instantiate(projectilePrefab, originPos, Quaternion.identity);
            script = projObj.GetComponent<SplittingProjectile>();
        }

        // 情况 2: 如果有现成的 (existingProjectile)，直接复用它！
        // (SplittingProjectile 脚本里需要有 Setup 方法来重置位置和目标)
        if (script != null)
        {
            script.Setup(originPos, nextTarget, nextDamage, this);
        }
    }
}