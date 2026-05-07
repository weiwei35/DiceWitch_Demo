using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Ghost Dice")]
public class Ability_Ghost : DiceAbilitySO
{
    [Header("Ghost Data")]
    // 务必在这里拖入配置了本 Ability 的 DiceDataSO，以实现无限循环
    public DiceDataSO overrideGhostData; 

    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        if (overrideGhostData != null)
        {
            int bonusToPass = 0;

            // 1. 从来源骰子获取当前的属性加成值
            if (sourceDice != null)
            {
                // 获取当前那一面的数据
                DiceFaceData currentFace = sourceDice.GetCurrentData();
                if (currentFace != null)
                {
                    bonusToPass = currentFace.bonusValue;
                }
            }

            // 2. 传给 UI 管理器
            GhostDiceUIManager.Instance.AddGhost(RuntimeDiceData.FromSO(overrideGhostData), bonusToPass);
        }
        else
        {
            Debug.LogWarning("Ability_Ghost: 未配置 GhostData");
        }
    }
}