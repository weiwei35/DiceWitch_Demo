using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Abilities/Flail (Link Enemies)")]
public class Ability_Flail : DiceAbilitySO
{
    [Header("Link Config")]
    public StatusEffectSO linkStatus; // 拖入 Status_SoulLink
    public GameObject linkStartVFX;   // 链接成功的特效

    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        // 只能连敌人
        if (target.team != Enum.TargetTeam.Enemy) return;
        EnemyTarget primary = (EnemyTarget)target;

        // 1. 寻找一个随机的、活着的、且不是自己的敌人
        EnemyTarget partner = GetRandomPartner(primary);

        if (partner != null)
        {
            // 2. 建立双向链接
            primary.soulLinkPartner = partner;
            partner.soulLinkPartner = primary;

            // 3. 给双方都挂上状态图标
            if (linkStatus != null)
            {
                primary.ApplyStatus(linkStatus, 1);
                partner.ApplyStatus(linkStatus, 1);
            }

            // 4. 播放特效
            if (linkStartVFX != null)
            {
                // 在两人中间播个特效，或者两头都播
                Instantiate(linkStartVFX, primary.transform.position, Quaternion.identity);
                Instantiate(linkStartVFX, partner.transform.position, Quaternion.identity);
            }

            Debug.Log($"链枷链接成功：{primary.name} <---> {partner.name}");
        }
        else
        {
            Debug.Log("场上没有其他敌人，无法链接！");
        }
    }

    private EnemyTarget GetRandomPartner(EnemyTarget exclusion)
    {
        var enemies = BattleManager.Instance.enemies;
        List<EnemyTarget> candidates = new List<EnemyTarget>();

        foreach (var e in enemies)
        {
            if (e != null && e.currentHp > 0 && e != exclusion)
            {
                // 可选：如果 e 已经有伙伴了，是否允许覆盖？
                // 这里假设允许覆盖（抢夺链接），或者你可以加 if(e.soulLinkPartner == null)
                candidates.Add(e);
            }
        }

        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }
        return null;
    }
}