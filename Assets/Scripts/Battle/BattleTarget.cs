using UnityEngine;

public enum TargetTeam { Player, Enemy }

public abstract class BattleTarget : MonoBehaviour
{
    public TargetTeam team; // 属于哪一队

    // 通用的受击/生效接口
    public virtual void OnHit(DiceFaceData data)
    {
        // 根据骰子类型决定做什么
        switch (data.type)
        {
            case DiceActionType.Attack:
                TakeDamage(data);
                break;
            case DiceActionType.Defend: // 假设你在 DiceFaceData 里定义了 Defend
                GainArmor(data.value);
                break;
            case DiceActionType.Magic:
                // 处理魔法...
                break;
        }
    }

    // 这些方法由子类具体实现
    public abstract void TakeDamage(DiceFaceData damageData);
    public abstract void ApplyStatus(StatusEffectSO status, int amount);
    public abstract void GainArmor(int amount);
    public abstract void ApplyDirectValue(int value); // 用于分裂造成的直接数值影响
}