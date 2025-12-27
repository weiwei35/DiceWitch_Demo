using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Fire Effect")]
public class Ability_Fire : DiceAbilitySO
{
    public StatusEffectSO burnStatus; 

    public override void OnPostHit(BattleTarget target, int finalDamage)
    {
        if (burnStatus != null)
        {
            Debug.Log($"施加燃烧：{finalDamage} 层");
            target.ApplyStatus(burnStatus, finalDamage);
        }
    }
}