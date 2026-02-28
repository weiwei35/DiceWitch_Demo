using UnityEngine;

[CreateAssetMenu(menuName = "Status/Soul Link (Split Damage)")]
public class Status_SoulLinkSO : StatusEffectSO
{
    [Header("Visuals")]
    public GameObject transferVFX; // 传递伤害时的特效

    // 1. 核心逻辑：伤害分摊
    public override int OnTakeDamage(EnemyTarget target, int incomingDamage, int stacks)
    {
        // 基础检查：伤害太小，或者没有伙伴，或者伙伴已经挂了
        if (incomingDamage <= 1 || target.soulLinkPartner == null || target.soulLinkPartner.currentHp <= 0)
        {
            return incomingDamage; // 不分摊，自己全吃
        }

        // 2. 计算分摊值 (对半劈)
        int damageToPartner = Mathf.FloorToInt(incomingDamage * 0.5f);
        int damageToSelf = incomingDamage - damageToPartner;

        // 3. 给伙伴造成伤害
        // 【关键】标记 isChainReaction=true，防止伙伴又把伤害弹回来导致死循环
        target.soulLinkPartner.ApplyDirectValue(damageToPartner, true);

        // 播放特效
        if (transferVFX != null)
        {
            Instantiate(transferVFX, target.transform.position, Quaternion.identity);
        }
        
        Debug.Log($"灵魂链接生效：{target.name} 承受 {damageToSelf}，分摊给 {target.soulLinkPartner.name} {damageToPartner}");

        // 4. 返回自己剩余应受的伤害
        return damageToSelf;
    }

    // 2. 回合开始自动移除 (断开链接)
    public override void OnTurnStart(EnemyTarget target, int stacks)
    {
        // 清空引用
        if (target.soulLinkPartner != null)
        {
            // 对方也要移除状态图标 (可选，如果对方也有层数的话会由对方的OnTurnStart处理)
            // 这里主要负责断开引用
            target.soulLinkPartner.soulLinkPartner = null;
            target.soulLinkPartner = null;
        }

        // 移除自身状态
        target.ApplyStatus(this, -stacks);
    }
}