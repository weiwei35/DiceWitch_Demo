using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bootstrap: ensures the split progression managers are attached to the persistent GameObject.
/// Keeps the Inspector ability library reference and forwards it to MagicCircleManager.
/// All progression logic has been moved to ResourceManager and MagicCircleManager.
/// </summary>
public class PlayerProgressionManager : MonoBehaviour
{
    public static PlayerProgressionManager Instance;

    [Header("Dice Visuals (forwarded to MagicCircleManager)")]
    public Sprite defaultDiceIcon;
    public Color defaultDiceColor = new Color(0.533276404f, 0.346704056f, 0.527115126f, 1f);
    public Sprite[] defaultFaceSprites = new Sprite[6];

    [Header("Library Reference (forwarded to MagicCircleManager)")]
    public List<DiceAbilitySO> allAbilitiesLibrary = new List<DiceAbilitySO>();

    [Header("Development")]
    [Tooltip("进入 Play Mode 时，用七种法术骰替换初始骰子并解锁全部槽位。")]
    public bool testAllSpellDice;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GetComponent<ResourceManager>() == null)
                gameObject.AddComponent<ResourceManager>();

            MagicCircleManager mcm = GetComponent<MagicCircleManager>();
            if (mcm == null)
                mcm = gameObject.AddComponent<MagicCircleManager>();

            mcm.defaultDiceIcon = defaultDiceIcon;
            mcm.defaultDiceColor = defaultDiceColor;
            mcm.defaultFaceSprites = defaultFaceSprites;
            mcm.allAbilitiesLibrary = allAbilitiesLibrary;

            if (testAllSpellDice)
                EquipAllSpellDice(mcm);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EquipAllSpellDice(MagicCircleManager mcm)
    {
        mcm.allOwnedDice.Clear();
        foreach (MagicCircleSlot slot in mcm.magicSlots)
        {
            slot.isUnlocked = false;
            slot.currentDice = null;
        }

        int slotIndex = 0;
        foreach (DiceAbilitySO ability in allAbilitiesLibrary)
        {
            if (!(ability is DiceSpellSO spell) || slotIndex >= mcm.magicSlots.Count)
                continue;

            PlayerDice dice = new PlayerDice { diceName = spell.abilityName, boundAbility = spell };
            MagicCircleSlot slot = mcm.magicSlots[slotIndex++];
            slot.isUnlocked = true;
            slot.currentDice = dice;
            mcm.allOwnedDice.Add(dice);
        }

        Debug.Log($"测试模式：已装备 {slotIndex} 颗法术骰。");
    }
}
