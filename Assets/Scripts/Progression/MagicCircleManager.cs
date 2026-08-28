using System.Collections.Generic;
using UnityEngine;

public class MagicCircleManager : MonoBehaviour
{
    public static MagicCircleManager Instance;

    [Header("Magic Circle")]
    public Sprite defaultDiceIcon;
    public Color defaultDiceColor = new Color(0.533276404f, 0.346704056f, 0.527115126f, 1f);
    public Sprite[] defaultFaceSprites = new Sprite[6];
    public List<MagicCircleSlot> magicSlots = new List<MagicCircleSlot>();

    public List<PlayerDice> allOwnedDice = new List<PlayerDice>();
    public List<DiceAbilitySO> allAbilitiesLibrary = new List<DiceAbilitySO>();

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
            magicSlots.Add(newSlot);
        }

        for (int i = 0; i < 3; i++)
        {
            PlayerDice startDice = new PlayerDice();
            startDice.forgeSlots = new List<ForgeSlot>
            {
                new ForgeSlot { tier = 1, optionIndex = -1 },
                new ForgeSlot { tier = 2, optionIndex = -1 },
                new ForgeSlot { tier = 3, optionIndex = -1 }
            };
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

    public List<DiceAbilitySO> GetRandomAbilities(int count)
    {
        List<DiceAbilitySO> result = new List<DiceAbilitySO>();
        List<DiceAbilitySO> pool = allAbilitiesLibrary.FindAll(ability => ability is DiceSpellSO);

        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            int randomIndex = Random.Range(0, pool.Count);
            result.Add(pool[randomIndex]);

            pool.RemoveAt(randomIndex);
        }
        return result;
    }

    public Sprite GetDefaultFaceSprite(int value)
    {
        int index = value - 1;
        return defaultFaceSprites != null && index >= 0 && index < defaultFaceSprites.Length
            ? defaultFaceSprites[index]
            : null;
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

}
