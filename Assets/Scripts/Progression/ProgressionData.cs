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
    public List<ForgeSlot> forgeSlots = new List<ForgeSlot>();
    public List<ForgeInspiration> forgeInspirations = new List<ForgeInspiration>();

    /// <summary>
    /// 创建骰子时生成稳定 uid，用于区分同名或同配置的骰子实例。
    /// </summary>
    public PlayerDice() { uid = System.Guid.NewGuid().ToString(); }
}

/// <summary>
/// 法阵中的一个骰子槽位。
/// 负责保存当前骰子和槽位解锁状态，并能构建战斗运行时数据。
/// </summary>
[System.Serializable]
public class MagicCircleSlot
{
    public int slotID;
    public bool isUnlocked = false;
    public PlayerDice currentDice;

    /// <summary>
    /// 根据槽位中的骰子和能力构建战斗使用的运行时骰子数据。
    /// </summary>
    /// <returns>可进入战斗的运行时骰子数据；槽位未解锁或没有骰子时返回 null。</returns>
    public RuntimeDiceData BuildDiceData()
    {
        if (!isUnlocked || currentDice == null) return null;

        RuntimeDiceData runtimeDice = new RuntimeDiceData();

        if (currentDice.boundAbility != null)
        {
            runtimeDice.diceName = currentDice.boundAbility.abilityName;
            runtimeDice.bodyColor = currentDice.boundAbility.diceColor;

            runtimeDice.spell = currentDice.boundAbility as DiceSpellSO;
            if (runtimeDice.spell == null)
                runtimeDice.abilities.Add(currentDice.boundAbility);
        }
        else
        {
            runtimeDice.diceName = "普通骰子";
            if (MagicCircleManager.Instance != null)
                runtimeDice.bodyColor = MagicCircleManager.Instance.defaultDiceColor;
        }

        for (int i = 0; i < 6; i++)
        {
            runtimeDice.faces[i] = new DiceFaceData();
            runtimeDice.faces[i].value = i + 1;
            runtimeDice.faces[i].color = Color.black;
        }

        DiceSpellRules.ConfigurePhysicalFaces(runtimeDice.spell, runtimeDice.faces);

        for (int i = 0; i < runtimeDice.faces.Length; i++)
        {
            int value = runtimeDice.faces[i].value;
            runtimeDice.faces[i].icon = runtimeDice.spell != null
                ? runtimeDice.spell.GetFaceSprite(value)
                : MagicCircleManager.Instance?.GetDefaultFaceSprite(value);
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
