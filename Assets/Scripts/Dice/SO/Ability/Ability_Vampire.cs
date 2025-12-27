using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Vampire")]
public class Ability_Vampire : DiceAbilitySO
{
    public override void OnPostHit(BattleTarget target, int finalDamage)
    {
        // 调用玩家单例回血
        PlayerManager.Instance.Heal(finalDamage/2);
        Debug.Log($"触发吸血！恢复 {finalDamage/2} 点生命");
    }
}