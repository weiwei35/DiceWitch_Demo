using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家拥有的一颗骰子。
/// 冥想系统会把刻印槽位和启迪历史记录在这里，战斗系统再由它构建运行时骰子数据。
/// </summary>
[System.Serializable]
public class PlayerDice
{
    public string uid;
    public string diceName = "普通骰子";
    public Sprite icon;
    public DiceAbilitySO boundAbility;
    public RuntimeSlotAttribute currentAttribute;
    public List<ForgeSlot> forgeSlots = new List<ForgeSlot>();
    public List<ForgeInspiration> forgeInspirations = new List<ForgeInspiration>();

    /// <summary>
    /// 创建骰子时生成稳定 uid，用于区分同名或同配置的骰子实例。
    /// </summary>
    public PlayerDice() { uid = System.Guid.NewGuid().ToString(); }
}

/// <summary>
/// 法阵槽位给骰子提供的运行时属性。
/// </summary>
[System.Serializable]
public class RuntimeSlotAttribute
{
    public SlotAttributeSO data;
    public int level = 1;
    /// <summary>
    /// 使用属性配置创建运行时属性实例。
    /// </summary>
    /// <param name="so">属性配置。</param>
    public RuntimeSlotAttribute(SlotAttributeSO so) { data = so; }

    /// <summary>
    /// 获取当前等级对应的属性数值。
    /// </summary>
    /// <returns>属性配置按当前等级计算出的值。</returns>
    public int GetCurrentValue() => data.GetValue(level);
}

/// <summary>
/// 法阵中的一个骰子槽位。
/// 负责保存当前骰子、槽位解锁状态和槽位属性，并能构建战斗运行时数据。
/// </summary>
[System.Serializable]
public class MagicCircleSlot
{
    public int slotID;
    public bool isUnlocked = false;
    public PlayerDice currentDice;
    public RuntimeSlotAttribute currentAttribute;

    /// <summary>
    /// 根据槽位中的骰子、能力和属性构建战斗使用的运行时骰子数据。
    /// </summary>
    /// <returns>可进入战斗的运行时骰子数据；槽位未解锁或没有骰子时返回 null。</returns>
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

/// <summary>
/// 战斗骰子条目。
/// 用于保留运行时骰子数据与来源玩家骰子之间的引用关系。
/// </summary>
public class BattleDiceEntry
{
    public RuntimeDiceData combatData;
    public PlayerDice sourceRef;
    public int forcedResultValue;
}
