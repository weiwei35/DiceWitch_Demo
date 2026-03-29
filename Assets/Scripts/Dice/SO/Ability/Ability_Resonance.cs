using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Resonance")]
public class Ability_Resonance : DiceAbilitySO
{
    [Header("Configuration")]
    public GameObject resonanceVFX; // (可选) 击中目标时播放的视觉特效

    /// <summary>
    /// 重写伤害计算：最终伤害 = 骰子基础面值 + 场上剩余的骰子总数
    /// </summary>
    public override int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice)
    {
        int extraDamage = GetRemainingDiceCount();
        int finalDamage = baseDamage + extraDamage;
        
        Debug.Log($"<color=cyan>【共鸣结算】基础面值:{baseDamage} + 场上剩余骰子:{extraDamage} = 最终伤害:{finalDamage}</color>");
        
        return finalDamage;
    }

    /// <summary>
    /// 击中后触发可选的视觉特效
    /// </summary>
    public override void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice)
    {
        if (resonanceVFX != null) 
            Instantiate(resonanceVFX, target.transform.position, Quaternion.identity);
    }

    /// <summary>
    /// 重写动态描述：让 UI Tooltip 实时显示当前能附加多少伤害
    /// </summary>
    public override string GetDynamicDescription(PhysicsDice sourceDice = null)
    {
        // 如果是在抽卡界面等非战斗场景查看，只显示基础描述
        if (sourceDice == null || !BattleManager.Instance.isPlayerTurn) 
        {
            return description;
        }

        int extraDamage = GetRemainingDiceCount();
        
        // 在策划填写的描述后面，自动拼接一个绿色的、实时的动态加成提示
        return description + $"\n<color=#00FF00>(当前附加伤害: +{extraDamage})</color>";
    }

    /// <summary>
    /// 辅助方法：统计当前战场上还剩下多少个可以被拖拽的物理骰子
    /// </summary>
    private int GetRemainingDiceCount()
    {
        // 直接查找场景中所有激活的 PhysicsDice 对象
        // 因为这个方法是在骰子被拖拽、但尚未销毁时调用的，所以总数就是剩余的数量
        var allDice = FindObjectsOfType<PhysicsDice>();

        // 如果场上只剩它自己一个，则没有额外加成
        if (allDice.Length <= 1)
        {
            return 0;
        }
        
        // 伤害加成 = 场上骰子总数 - 它自己
        return allDice.Length - 1;
    }
}