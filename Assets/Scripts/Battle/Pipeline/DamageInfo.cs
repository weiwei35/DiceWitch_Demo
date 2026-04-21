using UnityEngine;
using System.Collections.Generic;

// 伤害类型枚举，用于区分机制
// 极其重要：反弹伤害(Reflect)绝不能再次触发反弹机制！
public enum DamageType
{
    Normal,     // 普通物理/物理打击
    Magic,      // 魔法伤害
    TrueDamage, // 真实伤害（无视护甲）
    Reflect,    // 反弹伤害（用于防止死循环）
    StatusDOT   // 状态持续伤害（比如燃烧、中毒，通常不触发受击特效）
}

public class DamageInfo
{
    // 参与者
    public BattleTarget Attacker { get; private set; }
    public BattleTarget Defender { get; private set; }

    // 核心数值
    public int BaseDamage { get; private set; }     // 初始设定的伤害
    public int FinalDamage { get; set; }            // 经过各种Buff修饰后的最终伤害
    
    // 伤害标签
    public DamageType DmgType { get; private set; }
    public bool IsCritical { get; set; }            // 是否暴击
    public bool IsEvaded { get; set; }              // 是否被闪避

    // 追踪修饰来源 (方便UI显示或Debug: 比如 "伤害由 [力量] 增加了 2")
    public List<string> ModifiersLog = new List<string>();

    // 构造函数
    public DamageInfo(BattleTarget attacker, BattleTarget defender, int baseDamage, DamageType type = DamageType.Normal)
    {
        Attacker = attacker;
        Defender = defender;
        BaseDamage = baseDamage;
        FinalDamage = baseDamage; // 初始时，最终伤害等于基础伤害
        DmgType = type;
        IsCritical = false;
        IsEvaded = false;
    }

    // 辅助方法：方便Buff修改伤害并记录日志
    public void AddModifier(int amount, string sourceName)
    {
        FinalDamage += amount;
        if (FinalDamage < 0) FinalDamage = 0; // 伤害不能为负
        ModifiersLog.Add($"{sourceName}: {(amount > 0 ? "+" : "")}{amount}");
    }

    public void MultiplyModifier(float multiplier, string sourceName)
    {
        int oldDamage = FinalDamage;
        FinalDamage = Mathf.RoundToInt(FinalDamage * multiplier);
        ModifiersLog.Add($"{sourceName}: x{multiplier} ({oldDamage} -> {FinalDamage})");
    }
}