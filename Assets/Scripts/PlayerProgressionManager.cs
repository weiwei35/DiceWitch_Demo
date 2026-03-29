using System.Collections.Generic;
using UnityEngine;

// --- 数据类定义 ---

[System.Serializable]
public class PlayerDice
{
    public string uid;
    public string diceName = "普通骰子";
    public DiceAbilitySO boundAbility; // 绑定的法术，null代表白板
    public RuntimeSlotAttribute currentAttribute; 

    public PlayerDice() { uid = System.Guid.NewGuid().ToString(); }
}

[System.Serializable]
public class RuntimeSlotAttribute
{
    public SlotAttributeSO data;
    public int level = 1;
    public RuntimeSlotAttribute(SlotAttributeSO so) { data = so; }
    public int GetCurrentValue() => data.GetValue(level);
}

[System.Serializable]
public class MagicCircleSlot
{
    public int slotID;
    public bool isUnlocked = false;
    public PlayerDice currentDice; // 当前放了哪个骰子
    public RuntimeSlotAttribute currentAttribute; 

    // 核心：构建战斗用的临时数据
    public DiceDataSO BuildDiceData()
    {
        // 基础检查
        if (!isUnlocked || currentDice == null) return null;

        DiceDataSO runtimeDice = ScriptableObject.CreateInstance<DiceDataSO>();
        runtimeDice.name = $"Runtime_Slot_{slotID}";

        // =========================================================
        // 【核心修复】防御性初始化
        // 防止 DiceDataSO 里的 abilities 列表或 faces 数组为空
        // =========================================================
        if (runtimeDice.abilities == null) 
            runtimeDice.abilities = new List<DiceAbilitySO>();
            
        if (runtimeDice.faces == null) 
            runtimeDice.faces = new DiceFaceData[6];
        // =========================================================

        // 1. 处理法术
        if (currentDice.boundAbility != null)
        {
            runtimeDice.abilities.Add(currentDice.boundAbility);
            runtimeDice.diceName = currentDice.boundAbility.abilityName;
            runtimeDice.bodyColor = currentDice.boundAbility.diceColor;
        }
        else
        {
            runtimeDice.diceName = "普通骰子";
        }

        // 2. 处理属性加成
        int valueAdd = 0; // 计算出来的总加成值
        if (currentAttribute != null && currentAttribute.data != null)
        {
            if (currentAttribute.data.type == Enum.SlotAttributeType.BaseValueAdd)
            {
                valueAdd += currentAttribute.GetCurrentValue();
            }
        }

        // 3. 生成 6 个面
        for (int i = 0; i < 6; i++)
        {
            runtimeDice.faces[i] = new DiceFaceData();
            
            // --- 修改核心 ---
            runtimeDice.faces[i].value = i + 1; // 基础值永远是 1-6
            runtimeDice.faces[i].bonusValue = valueAdd; // 加成值单独存
            runtimeDice.faces[i].color = Color.black;
            // ----------------

            runtimeDice.faces[i].type = Enum.DiceActionType.Attack; 
        }

        return runtimeDice;
    }
}

public class BattleDiceEntry
{
    public DiceDataSO combatData; // 战斗用的数值面板
    public PlayerDice sourceRef;  // 对应的养成数据引用 (用于UI联动)
}
// --- 管理器本体 ---

public class PlayerProgressionManager : MonoBehaviour
{
    public static PlayerProgressionManager Instance;

    [Header("Magic Circle")]
    public List<MagicCircleSlot> magicSlots = new List<MagicCircleSlot>();
    
    [Header("Resources")]
    public int manaDust = 0; // 核心货币
    // 资源变化事件 (用于刷新UI)
    public event System.Action OnResourceChanged;
    
    // 临时存储所有骰子的列表（背包）
    public List<PlayerDice> allOwnedDice = new List<PlayerDice>();
    public List<SlotAttributeSO> allAttributesLibrary_Slot = new List<SlotAttributeSO>();
    public List<DiceAbilitySO> allAttributesLibrary_Dice = new List<DiceAbilitySO>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame(); // 游戏启动时初始化
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeGame()
    {
        // 1. 初始化 7 个槽位
        for (int i = 0; i < 7; i++)
        {
            MagicCircleSlot newSlot = new MagicCircleSlot();
            newSlot.slotID = i;
            newSlot.isUnlocked = (i < 3); // 默认解锁前3个
            newSlot.currentAttribute = null; 
            magicSlots.Add(newSlot);
        }

        // 2. 给玩家发 3 个白板骰子，填入前 3 个槽位
        for (int i = 0; i < 3; i++)
        {
            PlayerDice startDice = new PlayerDice();
            allOwnedDice.Add(startDice);
            magicSlots[i].currentDice = startDice;
        }
        
        Debug.Log("养成系统初始化完成：3个槽位，3个白板骰子");
    }

    // 供 BattleManager 调用
    public List<BattleDiceEntry> GetBattleDeck()
    {
        List<BattleDiceEntry> deck = new List<BattleDiceEntry>();
        
        foreach (var slot in magicSlots)
        {
            // 只有解锁且有骰子的槽位才生成
            if (slot.isUnlocked && slot.currentDice != null)
            {
                DiceDataSO data = slot.BuildDiceData();
                
                if (data != null)
                {
                    // 【打包】把数据和源引用捆在一起
                    BattleDiceEntry entry = new BattleDiceEntry();
                    entry.combatData = data;
                    entry.sourceRef = slot.currentDice; // <--- 关键！把槽位里的骰子引用传出去
                    
                    deck.Add(entry);
                }
            }
        }
        return deck;
    }
    
    public void Debug_SetSlotAttribute(int slotIndex, SlotAttributeSO newAttributeData)
    {
        if (slotIndex < 0 || slotIndex >= magicSlots.Count) return;
        MagicCircleSlot slot = magicSlots[slotIndex];

        // 直接覆盖
        slot.currentAttribute = new RuntimeSlotAttribute(newAttributeData);
        Debug.Log($"槽位 {slotIndex} 属性已变更为：{newAttributeData.attributeName}");
    }

    // --- 接口 B：升级当前属性 (Upgrade) ---
    public void Debug_UpgradeSlotAttribute(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= magicSlots.Count) return;
        MagicCircleSlot slot = magicSlots[slotIndex];

        if (slot.currentAttribute != null)
        {
            slot.currentAttribute.level++;
            Debug.Log($"槽位 {slotIndex} 属性升级！当前 Lv.{slot.currentAttribute.level}");
        }
    }
    // --- 核心功能：随机抽取 N 个不重复的法术 ---
    public List<DiceAbilitySO> GetRandomAbilities(int count)
    {
        List<DiceAbilitySO> result = new List<DiceAbilitySO>();
        List<DiceAbilitySO> pool = new List<DiceAbilitySO>(allAttributesLibrary_Dice); // 复制一份池子

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;
            
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            
            pool.RemoveAt(randomIndex); // 移除已选的，防止重复
        }
        return result;
    }
    // --- 核心接口：给指定骰子赋予法术 ---
    public void ImprintAbilityToDice(PlayerDice targetDice, DiceAbilitySO ability)
    {
        // 1. 安全检查
        if (targetDice == null)
        {
            Debug.LogError("尝试附魔，但目标骰子实体为空！");
            return;
        }
        
        if (ability == null)
        {
            Debug.LogError("尝试附魔，但法术数据为空！");
            return;
        }

        // 2. 执行绑定
        // 直接修改引用数据，因为 PlayerDice 是类(Class)，引用传递
        targetDice.boundAbility = ability;

        // 3. 更新名字 (方便调试和UI显示)
        // 比如把 "普通骰子" 改成 "火焰骰"
        if (!string.IsNullOrEmpty(ability.abilityName))
        {
            targetDice.diceName = ability.abilityName; 
        }

        Debug.Log($"<color=cyan>【养成成功】骰子 [{targetDice.uid}] 已获得能力：{ability.abilityName}</color>");
        
        // 4. (可选) 可以在这里添加保存存档的逻辑
        // SaveGame();
    }

    // --- 资源操作 ---
    public void AddManaDust(int amount)
    {
        manaDust += amount;
        OnResourceChanged?.Invoke();
        Debug.Log($"获得资源: {amount}, 当前: {manaDust}");
    }

    public bool TrySpendManaDust(int amount)
    {
        if (manaDust >= amount)
        {
            manaDust -= amount;
            OnResourceChanged?.Invoke();
            return true;
        }
        return false;
    }

    // --- 核心功能：随机抽取 N 个属性 (用于附魔三选一) ---
    public List<SlotAttributeSO> GetRandomAttributes(int count)
    {
        List<SlotAttributeSO> result = new List<SlotAttributeSO>();
        // 这里的 allAttributesLibrary 是你在 Inspector 里拖进去的所有属性库
        List<SlotAttributeSO> pool = new List<SlotAttributeSO>(allAttributesLibrary_Slot); 

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            // 如果属性允许重复获取，就不用 RemoveAt；如果为了让选项多样化，建议 RemoveAt
            pool.RemoveAt(randomIndex); 
        }
        return result;
    }
}