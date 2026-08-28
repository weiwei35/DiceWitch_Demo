using UnityEngine;

public enum DiceSpellType
{
    Elemental,
    Alchemy,
    BlackCoffin,
    Nature,
    SaintMath,
    PostSaintMath,
    Arcane
}

[CreateAssetMenu(fileName = "NewDiceSpell", menuName = "Game/Dice Spell")]
public class DiceSpellSO : DiceAbilitySO
{
    [Header("Spell Rule")]
    public string spellId;
    public DiceSpellType spellType;

    [Header("Optional Visual")]
    [Tooltip("按点数 1~6 配置的骰面贴图。")]
    public Sprite[] faceSprites = new Sprite[6];

    [Tooltip("法术响应时生成在骰子位置的可选特效；不配置也会播放基础动画。")]
    public GameObject triggerVfx;

    public Sprite GetFaceSprite(int value)
    {
        int index = value - 1;
        return faceSprites != null && index >= 0 && index < faceSprites.Length
            ? faceSprites[index]
            : null;
    }
}

public static class DiceSpellRules
{
    private static readonly int[] SaintFaces = { 1, 1, 3, 3, 5, 5 };
    private static readonly int[] PostSaintFaces = { 2, 2, 4, 4, 6, 6 };

    public static int GetTargetValue(DiceSpellType type, int currentPhysicalValue, int usedPhysicalValue, int remainingDiceCount)
    {
        switch (type)
        {
            case DiceSpellType.Nature:
                return Mathf.Min(6, currentPhysicalValue + 1);
            case DiceSpellType.Alchemy:
                return Mathf.Clamp(usedPhysicalValue, 1, 6);
            case DiceSpellType.BlackCoffin:
                return Mathf.Clamp(remainingDiceCount, 1, 6);
            case DiceSpellType.Arcane:
                return Random.Range(1, 7);
            default:
                return currentPhysicalValue;
        }
    }

    public static void ConfigurePhysicalFaces(DiceSpellSO spell, DiceFaceData[] faces)
    {
        if (spell == null || faces == null || faces.Length < 6) return;

        int[] values = spell.spellType == DiceSpellType.SaintMath
            ? SaintFaces
            : spell.spellType == DiceSpellType.PostSaintMath ? PostSaintFaces : null;
        if (values == null) return;

        for (int i = 0; i < 6; i++)
            faces[i].value = values[i];
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Tools/DiceWitch/Validate Dice Spell Rules")]
    private static void Validate()
    {
        Debug.Assert(GetTargetValue(DiceSpellType.Nature, 6, 1, 3) == 6);
        Debug.Assert(GetTargetValue(DiceSpellType.Nature, 2, 1, 3) == 3);
        Debug.Assert(GetTargetValue(DiceSpellType.Alchemy, 1, 5, 3) == 5);
        Debug.Assert(GetTargetValue(DiceSpellType.BlackCoffin, 1, 2, 9) == 6);
        Debug.Log("Dice spell rule validation passed.");
    }
#endif
}
