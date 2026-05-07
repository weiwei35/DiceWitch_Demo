using UnityEngine;

[CreateAssetMenu(menuName = "Forge/Resource")]
public class ForgeResourceSO : ScriptableObject
{
    public string resourceName;
    public ForgeResourceType resourceType = ForgeResourceType.Blank;
    [Range(1, 3)] public int rarity = 1;
    public Sprite icon;
    [TextArea] public string description;
}
