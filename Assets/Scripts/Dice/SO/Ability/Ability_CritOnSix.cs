using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Crit On Six")]
public class Ability_CritOnSix : DiceAbilitySO
{
    public override int OnCalculateDamage(int baseDamage, BattleTarget target)
    {
        // 如果基础点数是 6 (假设 baseDamage 就是点数)
        // 注意：这里我们可能需要更多上下文，为了简化，假设 baseDamage 就是骰面点数
        if (baseDamage == 6)
        {
            Debug.Log("触发暴击！伤害翻倍！");
            return baseDamage * 2;
        }
        return baseDamage;
    }
}