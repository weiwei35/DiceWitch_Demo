using System.Collections.Generic;

/// <summary>
/// 冥想/锻造系统使用的材料属性分类，用于决定启迪词条的抽取池。
/// </summary>
public enum ForgeResourceType
{
    Blank,   // 通用/空白资源
    Fire,    // 火焰属性
    Ice,     // 冰霜属性
    Poison,  // 剧毒属性
    Special  // 特殊催化剂（解锁隐藏词条池）
}

/// <summary>
/// 记录一次冥想生成的启迪节点。
/// 包含启迪词条、它在法术周围的位置，以及是否已经被刻印。
/// </summary>
[System.Serializable]
public class ForgeInspiration
{
    public ForgeAffixSO affix;
    public int optionIndex = -1;
    public bool isCommitted;
    public int slotIndex = -1;
}

/// <summary>
/// 骰子上最终被刻印的词条槽位。
/// 每个骰子最多拥有三个槽位，对应 T1/T2/T3。
/// </summary>
[System.Serializable]
public class ForgeSlot
{
    public int tier;          // 1-3
    public ForgeAffixSO affix;
    public bool isForged;
    public int optionIndex = -1; // 刻印时启迪所在的位置，-1 表示旧数据回退到 tier 位置
}

/// <summary>
/// 一次正在进行的冥想会话。
/// 负责保存目标骰子、当前槽位、已投入材料和本轮可选择的启迪。
/// </summary>
public class ForgeSession
{
    public PlayerDice targetDice;
    public int currentTier;                              // 0-based: 0=T1, 1=T2, 2=T3
    public List<ForgeResourceSO> investedResources = new List<ForgeResourceSO>();
    public List<ForgeAffixSO> generatedOptions = new List<ForgeAffixSO>();
    public List<ForgeInspiration> generatedInspirations = new List<ForgeInspiration>();
    public bool diceLocked = false;                      // 首次锻造后锁定骰子

    public bool CanForgeMore => generatedOptions.Count < 3;
    public int ForgeCount => generatedOptions.Count;
}

/// <summary>
/// Inspector 中配置初始材料库存的键值结构。
/// </summary>
[System.Serializable]
public struct ResourceInventoryEntry
{
    public ForgeResourceSO resource;
    public int count;
}
