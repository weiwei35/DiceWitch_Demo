using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerDice
{
    public string uid;
    public string diceName = "普通骰子";
    public Sprite icon;
    public DiceAbilitySO boundAbility;
    public RuntimeSlotAttribute currentAttribute;
    public List<ForgeSlot> forgeSlots = new List<ForgeSlot>();

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
    public PlayerDice currentDice;
    public RuntimeSlotAttribute currentAttribute;

    public RuntimeDiceData BuildDiceData()
    {
        if (!isUnlocked || currentDice == null) return null;

        RuntimeDiceData runtimeDice = new RuntimeDiceData();

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

        int valueAdd = 0;
        if (currentAttribute != null && currentAttribute.data != null)
        {
            if (currentAttribute.data.type == GameEnums.SlotAttributeType.BaseValueAdd)
            {
                valueAdd += currentAttribute.GetCurrentValue();
            }
        }

        for (int i = 0; i < 6; i++)
        {
            runtimeDice.faces[i] = new DiceFaceData();
            runtimeDice.faces[i].value = i + 1;
            runtimeDice.faces[i].bonusValue = valueAdd;
            runtimeDice.faces[i].color = Color.black;
            runtimeDice.faces[i].type = GameEnums.DiceActionType.Attack;
        }

        return runtimeDice;
    }
}

public class BattleDiceEntry
{
    public RuntimeDiceData combatData;
    public PlayerDice sourceRef;
}
