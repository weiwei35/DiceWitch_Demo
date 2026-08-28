using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plain C# runtime dice data. Replaces ScriptableObject.CreateInstance&lt;DiceDataSO&gt;()
/// to avoid native-object memory leaks. Use RuntimeDiceData.FromSO() to convert static SO assets.
/// </summary>
public class RuntimeDiceData
{
    public string diceName = "基础攻击骰";
    public Color bodyColor = Color.white;
    public DiceFaceData[] faces = new DiceFaceData[6];
    public List<DiceAbilitySO> abilities = new List<DiceAbilitySO>();
    public DiceSpellSO spell;

    public static RuntimeDiceData FromSO(DiceDataSO so)
    {
        if (so == null) return null;
        return new RuntimeDiceData
        {
            diceName = so.diceName,
            bodyColor = so.bodyColor,
            faces = so.faces,
            abilities = so.abilities
        };
    }
}
