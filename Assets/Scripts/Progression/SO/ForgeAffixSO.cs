using UnityEngine;

/// <summary>
/// 冥想可生成的启迪/词条配置。
/// 词条被刻印到骰子后，通过战斗钩子影响投掷点数、伤害或命中后的效果。
/// </summary>
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

    /// <summary>
    /// 投掷结果落定后修正骰子点数。
    /// </summary>
    /// <param name="primitiveValue">骰子原始面值。</param>
    /// <param name="sourceDice">触发该词条的物理骰子，可为空。</param>
    /// <returns>修正后的骰子点数。</returns>
    public virtual int OnRollEnd(int primitiveValue, PhysicsDice sourceDice = null)
    {
        return primitiveValue + bonus;
    }

    /// <summary>
    /// 计算攻击伤害时提供额外修正入口。
    /// </summary>
    /// <param name="baseDamage">进入词条计算前的基础伤害。</param>
    /// <param name="target">本次攻击的目标。</param>
    /// <param name="sourceDice">触发该词条的物理骰子，可为空。</param>
    /// <returns>修正后的伤害。</returns>
    public virtual int OnCalculateDamage(int baseDamage, BattleTarget target, PhysicsDice sourceDice = null)
    {
        return baseDamage; // 点数已在 OnRollEnd 中加成，此处不再叠加
    }

    /// <summary>
    /// 攻击命中并造成最终伤害后触发的后处理钩子。
    /// </summary>
    /// <param name="target">被命中的目标。</param>
    /// <param name="finalDamage">最终造成的伤害。</param>
    /// <param name="sourceDice">触发该词条的物理骰子，可为空。</param>
    public virtual void OnPostHit(BattleTarget target, int finalDamage, PhysicsDice sourceDice = null)
    {
    }
}
