using UnityEngine;

[CreateAssetMenu(menuName = "Progression/Slot Attribute")]
public class SlotAttributeSO : ScriptableObject
{
    public string attributeName; 
    public string description; 
    public GameEnums.SlotAttributeType type;
    
    public int baseValue = 1;      // Lv1 的数值
    public int upgradeValue = 1;   // 每升一级增加的数值

    public int GetValue(int level)
    {
        return baseValue + (level - 1) * upgradeValue;
    }
}