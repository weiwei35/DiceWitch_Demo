using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    /// <summary>获得材料（外部奖励入口）</summary>
    public void GainResource(ForgeResourceSO res, int amount)
    {
        if (res == null || amount <= 0) return;
        if (!_inventory.ContainsKey(res)) _inventory[res] = 0;
        _inventory[res] += amount;
        Debug.Log($"<color=#88FF88>获得材料: {res.resourceName} x{amount} (当前: {_inventory[res]})</color>");
    }

    /// <summary>查询材料数量</summary>
    public int GetResourceCount(ForgeResourceSO res)
    {
        if (res == null) return 0;
        return _inventory.TryGetValue(res, out int count) ? count : 0;
    }

    private bool ConsumeResource(ForgeResourceSO res)
    {
        if (res == null) return false;
        if (!_inventory.TryGetValue(res, out int count) || count <= 0) return false;
        _inventory[res] = count - 1;
        return true;
    }

    // =========================================================
    // 锻造流程入口
    // =========================================================

    public void StartForgeSession(PlayerDice dice)
    {
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

    public bool AddResource(ForgeResourceSO resource)
    {
        if (CurrentSession == null || resource == null) return false;

        if (!ConsumeResource(resource))
        {
            Debug.LogWarning($"材料 [{resource.resourceName}] 库存不足！");
            return false;
        }

        CurrentSession.investedResources.Add(resource);
        CurrentSession.diceLocked = true;

        int tier = CurrentSession.currentTier + 1;
        ForgeAffixSO newOption = GenerateAffix(tier, CurrentSession.investedResources);
        CurrentSession.generatedOptions.Add(newOption);

        Debug.Log($"<color=yellow>投入 [{resource.resourceName}]，生成选项: {newOption.affixName} (T{newOption.tier})  已锻造次数: {CurrentSession.ForgeCount}</color>");
        return true;
    }

    public void CommitAffix(ForgeAffixSO affix)
    {
        if (CurrentSession == null || affix == null) return;

        int idx = CurrentSession.currentTier;
        CurrentSession.targetDice.forgeSlots[idx].affix = affix;
        CurrentSession.targetDice.forgeSlots[idx].isForged = true;
        CurrentSession.targetDice.forgeSlots[idx].tier = idx + 1;

        Debug.Log($"<color=green>刻印完成！{CurrentSession.targetDice.diceName} 的 T{idx + 1} 槽位获得词条: {affix.affixName}</color>");

        CurrentSession = null;
    }

    public List<ForgeAffixSO> GetCurrentOptions()
    {
        return CurrentSession?.generatedOptions ?? new List<ForgeAffixSO>();
    }

    // =========================================================
    // 概率引擎
    // =========================================================

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
    /// 轨道判定：全部同属性（非 Blank/非 Special）→ 纯度轨道
    /// </summary>
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
    /// 轨道 B — 绝对纯度池：只出该属性的 ≤tier 词条，等概率
    /// </summary>
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
    /// 轨道 A — 混合偏移池：属性资源提供额外权重，仍可能歪出其他词条
    /// </summary>
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
