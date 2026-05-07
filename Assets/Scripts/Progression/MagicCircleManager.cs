using System.Collections.Generic;
using UnityEngine;

public class MagicCircleManager : MonoBehaviour
{
    public static MagicCircleManager Instance;

    [Header("Magic Circle")]
    public List<MagicCircleSlot> magicSlots = new List<MagicCircleSlot>();

    public List<PlayerDice> allOwnedDice = new List<PlayerDice>();
    public List<SlotAttributeSO> allAttributesLibrary_Slot = new List<SlotAttributeSO>();
    public List<DiceAbilitySO> allAttributesLibrary_Dice = new List<DiceAbilitySO>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeGame()
    {
        for (int i = 0; i < 7; i++)
        {
            MagicCircleSlot newSlot = new MagicCircleSlot();
            newSlot.slotID = i;
            newSlot.isUnlocked = (i < 3);
            newSlot.currentAttribute = null;
            magicSlots.Add(newSlot);
        }

        for (int i = 0; i < 3; i++)
        {
            PlayerDice startDice = new PlayerDice();
            allOwnedDice.Add(startDice);
            magicSlots[i].currentDice = startDice;
        }

        Debug.Log("养成系统初始化完成：3个槽位，3个白板骰子");
    }

    public List<BattleDiceEntry> GetBattleDeck()
    {
        List<BattleDiceEntry> deck = new List<BattleDiceEntry>();

        foreach (var slot in magicSlots)
        {
            if (slot.isUnlocked && slot.currentDice != null)
            {
                RuntimeDiceData data = slot.BuildDiceData();

                if (data != null)
                {
                    BattleDiceEntry entry = new BattleDiceEntry();
                    entry.combatData = data;
                    entry.sourceRef = slot.currentDice;

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

        slot.currentAttribute = new RuntimeSlotAttribute(newAttributeData);
        Debug.Log($"槽位 {slotIndex} 属性已变更为：{newAttributeData.attributeName}");
    }

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

    public List<DiceAbilitySO> GetRandomAbilities(int count)
    {
        List<DiceAbilitySO> result = new List<DiceAbilitySO>();
        List<DiceAbilitySO> pool = new List<DiceAbilitySO>(allAttributesLibrary_Dice);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);

            pool.RemoveAt(randomIndex);
        }
        return result;
    }

    public void ImprintAbilityToDice(PlayerDice targetDice, DiceAbilitySO ability)
    {
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

        targetDice.boundAbility = ability;

        if (!string.IsNullOrEmpty(ability.abilityName))
        {
            targetDice.diceName = ability.abilityName;
        }

        Debug.Log($"<color=cyan>【养成成功】骰子 [{targetDice.uid}] 已获得能力：{ability.abilityName}</color>");
    }

    public List<SlotAttributeSO> GetRandomAttributes(int count)
    {
        List<SlotAttributeSO> result = new List<SlotAttributeSO>();
        List<SlotAttributeSO> pool = new List<SlotAttributeSO>(allAttributesLibrary_Slot);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;
            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);
            pool.RemoveAt(randomIndex);
        }
        return result;
    }
}
