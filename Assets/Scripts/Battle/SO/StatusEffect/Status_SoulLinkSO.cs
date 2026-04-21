using UnityEngine;

[CreateAssetMenu(menuName = "DiceWitch/Status/Soul Link")]
public class Status_SoulLinkSO : StatusEffectSO
{
    public GameObject transferVFX;

    public override void OnBeforeDefend(BattleTarget target, DamageInfo info, int stacks)
    {
        EnemyTarget enemy = target as EnemyTarget;
        
        // 避免无限回弹
        if (info.DmgType == DamageType.Reflect || info.FinalDamage <= 1 || enemy == null || enemy.soulLinkPartner == null || enemy.soulLinkPartner.currentHp <= 0) return;

        // 计算分给伙伴的伤害
        int damageToPartner = Mathf.FloorToInt(info.FinalDamage * 0.5f);

        // 1. 将自己的包裹里的伤害减去要分摊的部分
        info.AddModifier(-damageToPartner, statusName);

        // 2. 扔一个反弹包裹给伙伴
        DamageInfo partnerDmg = new DamageInfo(info.Attacker, enemy.soulLinkPartner, damageToPartner, DamageType.Reflect);
        BattleManager.Instance.ProcessDamage(partnerDmg);

        if (transferVFX != null) Instantiate(transferVFX, target.transform.position, Quaternion.identity);
    }

    public override void OnTurnStart(BattleTarget target, int stacks)
    {
        if (target is EnemyTarget enemy)
        {
            if (enemy.soulLinkPartner != null) enemy.soulLinkPartner.soulLinkPartner = null;
            enemy.soulLinkPartner = null;
            enemy.ApplyStatus(this, -stacks);
        }
    }
}