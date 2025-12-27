using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Split Effect")]
public class Ability_Split : DiceAbilitySO
{
    [Header("Split Settings")]
    public GameObject projectilePrefab; // 拖入 MiniDiceProjectile
    public float damageMultiplier = 0.5f; // 伤害衰减系数 (0.5 = 一半)
    public int minDamage = 1; // 最小伤害阈值

    // 1. 钩子：当主骰子击中敌人后触发
    public override void OnPostHit(BattleTarget target, int finalDamage)
    {
        // 从主骰子的位置，或者敌人的位置开始分裂
        // 这里选择从受击敌人身上蹦出来
        TrySpawnNextSplit(target.transform.position, target, finalDamage);
    }

    // 2. 核心逻辑：尝试生成下一次分裂
    // originPos: 起飞点
    // currentVictim: 当前受害者 (下一发不能打他)
    // currentDamage: 当前造成的伤害
    public void TrySpawnNextSplit(Vector3 originPos, BattleTarget currentVictim, int currentDamage)
    {
        // 计算下一跳伤害 (向下取整)
        int nextDamage = Mathf.FloorToInt(currentDamage * damageMultiplier);

        // 终止条件：伤害太低，或者 Prefab 没配
        if (nextDamage < minDamage || projectilePrefab == null)
        {
            return; 
        }

        // 寻找下一个受害者
        BattleTarget nextTarget = BattleManager.Instance.GetRandomTarget(currentVictim); 

        // 如果找到了目标，就发射！
        if (nextTarget != null)
        {
            // 生成投射物
            GameObject projObj = Instantiate(projectilePrefab, originPos, Quaternion.identity);
            
            // 初始化投射物
            SplittingProjectile script = projObj.GetComponent<SplittingProjectile>();
            if (script != null)
            {
                // 这里的 'this' 把能力本身传进去，为了让投射物能调回这个方法
                script.Setup(originPos, nextTarget, nextDamage, this);
            }
        }
    }
}