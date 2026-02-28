using UnityEngine;

[CreateAssetMenu(menuName = "Status/Reflect (Thorns)")]
public class Status_ReflectSO : StatusEffectSO
{
    [Header("Reflect Config")]
    public GameObject reflectVFX; // 反弹特效

    public override int OnTakeDamage(EnemyTarget target, int incomingDamage, int stacks)
    {
        // 如果伤害无效，或者没有层数，直接返回
        if (incomingDamage <= 0 || stacks <= 0) return incomingDamage;

        // 1. 执行反伤：对玩家造成等量伤害
        Debug.Log($"<color=red>触发反伤！玩家受到 {incomingDamage} 点反噬伤害。</color>");
        
        // 假设 PlayerManager 单例有 TakeDamage 方法
        PlayerManager.Instance.TakeDamage(incomingDamage);

        // 播放特效
        if (reflectVFX != null)
        {
            Instantiate(reflectVFX, target.transform.position, Quaternion.identity);
        }
        
        // 飘字提示 "Reflect!"
        // DamageNumberManager.Instance.ShowText(target.transform.position, "反伤!"); 

        // 2. 消耗层数 (前 1 次攻击无效 -> 消耗 1 层)
        target.ApplyStatus(this, -1);

        // 3. 【关键】返回 0
        // 这意味着敌人受到的最终伤害为 0 (无效化)
        return 0;
    }
}