using UnityEngine;

/// <summary>
/// 冥想材料配置。
/// 材料的属性、稀有度和描述会影响启迪抽取与背包展示。
/// </summary>
[CreateAssetMenu(menuName = "Forge/Resource")]
public class ForgeResourceSO : ScriptableObject
{
    public string resourceName;
    public ForgeResourceType resourceType = ForgeResourceType.Blank;
    [Range(1, 3)] public int rarity = 1;
    public Sprite icon;
    [TextArea] public string description;
}
