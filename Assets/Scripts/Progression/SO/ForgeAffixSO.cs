using UnityEngine;

[CreateAssetMenu(menuName = "Forge/Affix")]
public class ForgeAffixSO : ScriptableObject
{
    [Header("Basic")]
    public string affixName;
    [Range(1, 3)] public int tier = 1;
    public ForgeResourceType tag = ForgeResourceType.Blank;
    [Range(1, 3)] public int quality = 1;
    public Sprite icon;

    [TextArea]
    public string description;

    [Header("Effects")]
    public int bonus; // 骰子点数加成，所见即所得

    // ---- 战斗钩子 ----

    public virtual int OnRollEnd(int primitiveValue, PhysicsDice sourceDice = null)
    {
        return primitiveValue + bonus;
    }

    public virtual int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice = null)
    {
        return baseDamage; // 点数已在 OnRollEnd 中加成，此处不再叠加
    }

    public virtual void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice = null)
    {
    }
}
