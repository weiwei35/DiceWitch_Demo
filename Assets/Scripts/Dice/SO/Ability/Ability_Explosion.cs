using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Chain Link Status")]
public class Ability_Explosion : DiceAbilitySO
{
    [Header("Configuration")]
    public StatusEffectSO chainLinkStatus; // 拖入 Status_ChainLinkSO
    public GameObject linkVFX;             // 激活特效

    // 1. 抹除初始物理伤害 (如果你希望这一下只是挂状态)
    public override int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice)
    {
        return 0;
    }

    // 2. 施加状态
    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        // 播放特效
        if (linkVFX != null) 
            Instantiate(linkVFX, target.transform.position, Quaternion.identity);

        // 施加状态 (层数设为 1)
        if (chainLinkStatus != null)
        {
            target.ApplyStatus(chainLinkStatus, 1);
            Debug.Log($"已给 {target.name} 施加链枷状态");
        }
    }
}