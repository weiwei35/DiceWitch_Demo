using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Chain Link (Explosion)")]
public class Ability_Explosion : DiceAbilitySO
{
    [Header("Chain Settings")]
    public float transferRatio = 0.5f; // 传递比例
    public GameObject linkVFX;         // 链枷激活时的特效
    public GameObject damageVFX;       // 传递伤害时的爆炸特效

    // 1. 抹除初始伤害
    // 因为需求说“被攻击的敌人本次不会收到伤害”
    public override int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice)
    {
        // 返回 0，表示本次物理撞击不扣血
        return 0;
    }

    // 2. 挂载链枷状态
    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        // 播放激活特效（比如一个锁链套在敌人身上）
        if (linkVFX != null) 
            Instantiate(linkVFX, target.transform.position, Quaternion.identity);

        // 检查是否已经有这个状态了，避免重复挂载
        var existingStatus = target.GetComponent<DamageLinkStatus>();
        if (existingStatus == null)
        {
            // 挂载新组件
            DamageLinkStatus status = target.gameObject.AddComponent<DamageLinkStatus>();
            status.Setup((EnemyTarget)target, transferRatio, damageVFX);
        }
        else
        {
            // 如果已经有了，可以刷新持续时间，或者叠加倍率（看你设计）
            Debug.Log("目标已有链枷，跳过。");
        }
    }
}