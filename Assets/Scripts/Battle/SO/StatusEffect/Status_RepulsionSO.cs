using UnityEngine;
[CreateAssetMenu(menuName = "DiceWitch/Status/Repulsion")]
public class Status_RepulsionSO : StatusEffectSO
{
    public override void OnPlayerGainArmor(BattleTarget target, int armorAmount, int stacks)
    {
        var allDice = FindObjectsOfType<PhysicsDice>();
        foreach (var dice in allDice)
        {
            if (dice != null) dice.ApplyTemporaryBonus(-1);
        }
    }
}