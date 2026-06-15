using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 冥想/锻造系统的业务管理器。
/// 负责材料库存、冥想会话、启迪生成、词条刻印，以及根据材料组合抽取词条。
/// </summary>
public class ForgeManager : MonoBehaviour
{
    public static ForgeManager Instance;

    [Header("Affix Library")]
    public List<ForgeAffixSO> allAffixes = new List<ForgeAffixSO>();

    [Header("Resource Library")]
    public List<ForgeResourceSO> allResources = new List<ForgeResourceSO>();
    public List<ResourceInventoryEntry> initialInventory = new List<ResourceInventoryEntry>();

    private Dictionary<ForgeResourceSO, int> _inventory = new Dictionary<ForgeResourceSO, int>();

    public ForgeSession CurrentSession { get; private set; }
    public bool CanForgeMore => CurrentSession != null && CurrentSession.CanForgeMore;

    /// <summary>
    /// 初始化单例和 Inspector 配置的初始材料库存。
    /// </summary>
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            foreach (var entry in initialInventory)
                if (entry.resource != null && entry.count > 0)
                    _inventory[entry.resource] = entry.count;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // =========================================================
    // 库存接口
    // =========================================================

    /// <summary>
    /// 获得材料，通常由地图奖励、事件或战斗奖励调用。
    /// </summary>
    /// <param name="res">要获得的材料配置。</param>
    /// <param name="amount">获得数量。</param>
    public void GainResource(ForgeResourceSO res, int amount)
    {
        if (res == null || amount <= 0) return;
        if (!_inventory.ContainsKey(res)) _inventory[res] = 0;
        _inventory[res] += amount;
        Debug.Log($"<color=#88FF88>获得材料: {res.resourceName} x{amount} (当前: {_inventory[res]})</color>");
    }

    /// <summary>
    /// 查询某种材料当前库存数量。
    /// </summary>
    /// <param name="res">要查询的材料配置。</param>
    /// <returns>当前库存数量；材料为空或未拥有时返回 0。</returns>
    public int GetResourceCount(ForgeResourceSO res)
    {
        if (res == null) return 0;
        return _inventory.TryGetValue(res, out int count) ? count : 0;
    }

    /// <summary>
    /// 尝试从库存中扣除指定数量的材料。
    /// </summary>
    /// <param name="res">要扣除的材料配置。</param>
    /// <param name="amount">扣除数量。</param>
    /// <returns>扣除成功返回 true；库存不足或参数无效时返回 false。</returns>
    public bool TryConsumeResource(ForgeResourceSO res, int amount = 1)
    {
        if (res == null || amount <= 0) return false;
        if (!_inventory.TryGetValue(res, out int count) || count < amount) return false;
        _inventory[res] = count - amount;
        return true;
    }

    /// <summary>
    /// 将材料返还到库存。
    /// </summary>
    /// <param name="res">要返还的材料配置。</param>
    /// <param name="amount">返还数量。</param>
    public void RefundResource(ForgeResourceSO res, int amount = 1)
    {
        if (res == null || amount <= 0) return;
        if (!_inventory.ContainsKey(res)) _inventory[res] = 0;
        _inventory[res] += amount;
    }

    // =========================================================
    // 锻造流程入口
    // =========================================================

    /// <summary>
    /// 为目标骰子开启一轮冥想会话，并定位第一个未刻印槽位。
    /// </summary>
    /// <param name="dice">本轮冥想的目标骰子。</param>
    public void StartForgeSession(PlayerDice dice)
    {
        EnsureInspirationList(dice);

        if (dice.forgeSlots == null || dice.forgeSlots.Count < 3)
        {
            Debug.LogError("目标骰子的 forgeSlots 未正确初始化！");
            return;
        }

        int tierIndex = -1;
        for (int i = 0; i < 3; i++)
        {
            if (!dice.forgeSlots[i].isForged)
            {
                tierIndex = i;
                break;
            }
        }

        if (tierIndex == -1)
        {
            Debug.Log("该骰子 3 个槽位均已锻造完毕，无法继续锻造。");
            return;
        }

        CurrentSession = new ForgeSession
        {
            targetDice = dice,
            currentTier = tierIndex
        };

        Debug.Log($"<color=cyan>开始锻造: {dice.diceName} — 第 {tierIndex + 1} 槽位 (T{tierIndex + 1})</color>");
    }

    /// <summary>
    /// 旧版逐个投入材料的入口。
    /// 当前 UI 主要使用 MeditateWithResources 一次投入三个材料；保留此方法用于兼容。
    /// </summary>
    /// <param name="resource">要投入的材料。</param>
    /// <returns>投入并生成启迪成功返回 true。</returns>
    public bool AddResource(ForgeResourceSO resource)
    {
        if (CurrentSession == null || resource == null) return false;

        if (!TryConsumeResource(resource))
        {
            Debug.LogWarning($"材料 [{resource.resourceName}] 库存不足！");
            return false;
        }

        CurrentSession.investedResources.Add(resource);
        CurrentSession.diceLocked = true;

        int tier = CurrentSession.currentTier + 1;
        ForgeAffixSO newOption = GenerateAffix(tier, CurrentSession.investedResources);
        CurrentSession.generatedOptions.Add(newOption);
        ForgeInspiration inspiration = CreateInspiration(CurrentSession.targetDice, newOption, CurrentSession.generatedInspirations.Count);
        CurrentSession.generatedInspirations.Add(inspiration);

        Debug.Log($"<color=yellow>投入 [{resource.resourceName}]，生成选项: {newOption.affixName} (T{newOption.tier})  已锻造次数: {CurrentSession.ForgeCount}</color>");
        return true;
    }

    /// <summary>
    /// 使用三个材料完成一次冥想，生成一个启迪并记录在目标骰子上。
    /// </summary>
    /// <param name="dice">本次冥想的目标骰子。</param>
    /// <param name="resources">已经从背包扣除并放入槽位的三个材料。</param>
    /// <param name="optionIndex">启迪在法术周围的显示位置索引。</param>
    /// <returns>生成的持久启迪记录；失败时返回 null。</returns>
    public ForgeInspiration MeditateWithResources(PlayerDice dice, List<ForgeResourceSO> resources, int optionIndex = -1)
    {
        if (dice == null || resources == null || resources.Count != 3)
        {
            Debug.LogWarning("冥想需要选择目标骰子，并放满 3 个材料槽。");
            return null;
        }

        foreach (var resource in resources)
        {
            if (resource == null)
            {
                Debug.LogWarning("冥想材料中存在空槽位。");
                return null;
            }
        }

        if (CurrentSession == null || CurrentSession.targetDice != dice)
            StartForgeSession(dice);

        if (CurrentSession == null || !CurrentSession.CanForgeMore)
        {
            Debug.Log("当前锻造会话已达启迪上限，或目标骰子不可锻造。");
            return null;
        }

        CurrentSession.investedResources.AddRange(resources);
        CurrentSession.diceLocked = true;

        int tier = CurrentSession.currentTier + 1;
        ForgeAffixSO newOption = GenerateAffix(tier, resources);
        if (newOption == null) return null;

        CurrentSession.generatedOptions.Add(newOption);
        ForgeInspiration inspiration = CreateInspiration(dice, newOption, optionIndex);
        CurrentSession.generatedInspirations.Add(inspiration);

        Debug.Log($"<color=yellow>冥想完成，生成启迪: {newOption.affixName} (T{newOption.tier})  启迪次数: {CurrentSession.ForgeCount}/3</color>");
        return inspiration;
    }

    /// <summary>
    /// 将一个持久启迪刻印到当前槽位。
    /// </summary>
    /// <param name="inspiration">玩家选择并长按完成的启迪记录。</param>
    public void CommitAffix(ForgeInspiration inspiration)
    {
        if (inspiration == null) return;
        CommitAffix(inspiration.affix, inspiration.optionIndex, inspiration);
    }

    /// <summary>
    /// 按词条配置刻印到当前槽位的兼容入口。
    /// </summary>
    /// <param name="affix">要刻印的词条配置。</param>
    /// <param name="optionIndex">该词条在法术周围的显示位置索引。</param>
    public void CommitAffix(ForgeAffixSO affix, int optionIndex = -1)
    {
        CommitAffix(affix, optionIndex, FindCurrentInspiration(affix, optionIndex));
    }

    /// <summary>
    /// 执行刻印的共享实现，写入骰子槽位并更新启迪状态。
    /// </summary>
    /// <param name="affix">最终刻印的词条配置。</param>
    /// <param name="optionIndex">启迪显示位置索引。</param>
    /// <param name="inspiration">对应的持久启迪记录；旧数据路径可为空。</param>
    private void CommitAffix(ForgeAffixSO affix, int optionIndex, ForgeInspiration inspiration)
    {
        if (CurrentSession == null || affix == null) return;

        int idx = CurrentSession.currentTier;
        CurrentSession.targetDice.forgeSlots[idx].affix = affix;
        CurrentSession.targetDice.forgeSlots[idx].isForged = true;
        CurrentSession.targetDice.forgeSlots[idx].tier = idx + 1;
        CurrentSession.targetDice.forgeSlots[idx].optionIndex = optionIndex;
        if (inspiration != null)
        {
            inspiration.isCommitted = true;
            inspiration.slotIndex = idx;
            if (inspiration.optionIndex < 0)
                inspiration.optionIndex = optionIndex;
        }

        Debug.Log($"<color=green>刻印完成！{CurrentSession.targetDice.diceName} 的 T{idx + 1} 槽位获得词条: {affix.affixName}，启迪位置: {optionIndex}</color>");

        CurrentSession = null;
    }

    /// <summary>
    /// 获取当前会话中生成的词条配置列表。
    /// </summary>
    /// <returns>当前会话的词条列表；没有会话时返回空列表。</returns>
    public List<ForgeAffixSO> GetCurrentOptions()
    {
        return CurrentSession?.generatedOptions ?? new List<ForgeAffixSO>();
    }

    /// <summary>
    /// 获取当前会话中生成的持久启迪记录。
    /// </summary>
    /// <returns>当前会话的启迪记录列表；没有会话时返回空列表。</returns>
    public List<ForgeInspiration> GetCurrentInspirations()
    {
        return CurrentSession?.generatedInspirations ?? new List<ForgeInspiration>();
    }

    /// <summary>
    /// 创建并挂载一个持久启迪记录。
    /// </summary>
    /// <param name="dice">启迪所属的目标骰子。</param>
    /// <param name="affix">启迪包含的词条配置。</param>
    /// <param name="optionIndex">启迪在法术周围的位置索引。</param>
    /// <returns>新建的启迪记录。</returns>
    private ForgeInspiration CreateInspiration(PlayerDice dice, ForgeAffixSO affix, int optionIndex)
    {
        EnsureInspirationList(dice);
        ForgeInspiration inspiration = new ForgeInspiration
        {
            affix = affix,
            optionIndex = optionIndex,
            isCommitted = false,
            slotIndex = CurrentSession != null ? CurrentSession.currentTier : -1
        };
        dice.forgeInspirations.Add(inspiration);
        return inspiration;
    }

    /// <summary>
    /// 在当前会话中查找与词条和位置匹配的启迪记录。
    /// </summary>
    /// <param name="affix">要匹配的词条配置。</param>
    /// <param name="optionIndex">要匹配的位置索引；小于 0 时只按词条匹配。</param>
    /// <returns>匹配的启迪记录；未找到时返回 null。</returns>
    private ForgeInspiration FindCurrentInspiration(ForgeAffixSO affix, int optionIndex)
    {
        if (CurrentSession == null || affix == null || CurrentSession.generatedInspirations == null) return null;

        foreach (var inspiration in CurrentSession.generatedInspirations)
        {
            if (inspiration == null || inspiration.affix != affix) continue;
            if (optionIndex < 0 || inspiration.optionIndex == optionIndex)
                return inspiration;
        }

        return null;
    }

    /// <summary>
    /// 确保骰子拥有启迪历史列表，兼容旧数据中列表为空的情况。
    /// </summary>
    /// <param name="dice">需要检查的玩家骰子。</param>
    private void EnsureInspirationList(PlayerDice dice)
    {
        if (dice != null && dice.forgeInspirations == null)
            dice.forgeInspirations = new List<ForgeInspiration>();
    }

    // =========================================================
    // 概率引擎
    // =========================================================

    /// <summary>
    /// 根据当前槽位等级和材料组合抽取一个词条。
    /// </summary>
    /// <param name="tier">当前刻印槽位等级，范围通常为 1-3。</param>
    /// <param name="resources">本次冥想投入的材料列表。</param>
    /// <returns>抽取到的词条配置。</returns>
    private ForgeAffixSO GenerateAffix(int tier, List<ForgeResourceSO> resources)
    {
        bool isPure = DetermineTrack(resources);

        if (isPure)
        {
            ForgeResourceType pureType = resources[0].resourceType;
            return RollFromPurePool(tier, pureType);
        }
        else
        {
            return RollFromMixedPool(tier, resources);
        }
    }

    /// <summary>
    /// 判断材料组合是否进入纯度轨道：全部同属性且不是 Blank/Special。
    /// </summary>
    /// <param name="resources">本次冥想投入的材料列表。</param>
    /// <returns>满足纯度轨道条件返回 true，否则走混合池。</returns>
    private bool DetermineTrack(List<ForgeResourceSO> resources)
    {
        if (resources.Count == 0) return false;

        ForgeResourceType firstType = resources[0].resourceType;
        if (firstType == ForgeResourceType.Blank || firstType == ForgeResourceType.Special)
            return false;

        foreach (var r in resources)
        {
            if (r.resourceType != firstType)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 从纯度池抽取词条，只在指定属性且等级不高于当前槽位的词条中等概率选择。
    /// </summary>
    /// <param name="tier">当前刻印槽位等级。</param>
    /// <param name="tag">纯度轨道对应的材料属性。</param>
    /// <returns>抽取到的词条配置。</returns>
    private ForgeAffixSO RollFromPurePool(int tier, ForgeResourceType tag)
    {
        var pool = allAffixes.Where(a => a.tier <= tier && a.tag == tag).ToList();
        if (pool.Count == 0)
        {
            Debug.LogWarning($"纯度池为空 (tier<={tier}, tag={tag})，回退到通用池");
            pool = allAffixes.Where(a => a.tier <= tier && a.tag == ForgeResourceType.Blank).ToList();
        }
        if (pool.Count == 0)
        {
            Debug.LogError("锻造池完全为空！请检查 ForgeManager 的 allAffixes 配置。");
            return null;
        }
        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>
    /// 从混合池抽取词条，根据投入材料属性为对应词条增加权重。
    /// </summary>
    /// <param name="tier">当前刻印槽位等级。</param>
    /// <param name="resources">本次冥想投入的材料列表。</param>
    /// <returns>按权重抽取到的词条配置。</returns>
    private ForgeAffixSO RollFromMixedPool(int tier, List<ForgeResourceSO> resources)
    {
        var candidates = allAffixes.Where(a => a.tier <= tier).ToList();
        if (candidates.Count == 0)
        {
            Debug.LogError($"无可用词条 (tier<={tier})");
            return null;
        }

        // 计算权重：基础权重 1，每种属性资源按 rarity 叠加权重给对应 tag 的词条
        Dictionary<ForgeAffixSO, float> weights = new Dictionary<ForgeAffixSO, float>();
        foreach (var affix in candidates)
        {
            float w = 1f;
            foreach (var res in resources)
            {
                if (res.resourceType != ForgeResourceType.Blank && res.resourceType != ForgeResourceType.Special)
                {
                    if (affix.tag == res.resourceType)
                        w += res.rarity;
                }
            }
            weights[affix] = w;
        }

        float totalWeight = weights.Values.Sum();
        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var kvp in weights)
        {
            cumulative += kvp.Value;
            if (roll <= cumulative)
                return kvp.Key;
        }

        return candidates[candidates.Count - 1]; // 兜底
    }
}
