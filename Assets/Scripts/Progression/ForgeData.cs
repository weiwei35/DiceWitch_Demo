using System.Collections.Generic;

public enum ForgeResourceType
{
    Blank,   // 通用/空白资源
    Fire,    // 火焰属性
    Ice,     // 冰霜属性
    Poison,  // 剧毒属性
    Special  // 特殊催化剂（解锁隐藏词条池）
}

[System.Serializable]
public class ForgeSlot
{
    public int tier;          // 1-3
    public ForgeAffixSO affix;
    public bool isForged;
}

public class ForgeSession
{
    public PlayerDice targetDice;
    public int currentTier;                              // 0-based: 0=T1, 1=T2, 2=T3
    public List<ForgeResourceSO> investedResources = new List<ForgeResourceSO>();
    public List<ForgeAffixSO> generatedOptions = new List<ForgeAffixSO>();
    public bool diceLocked = false;                      // 首次锻造后锁定骰子

    public bool CanForgeMore => generatedOptions.Count < 3;
    public int ForgeCount => generatedOptions.Count;
}

[System.Serializable]
public struct ResourceInventoryEntry
{
    public ForgeResourceSO resource;
    public int count;
}
