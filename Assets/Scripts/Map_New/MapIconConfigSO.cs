using UnityEngine;

[CreateAssetMenu(menuName = "Map/Icon Config")]
public class MapIconConfigSO : ScriptableObject
{
    [Header("Effect Icons")]
    public Sprite hpHealIcon;
    public Sprite hpDamageIcon;
    public Sprite resourceIcon;
    public Sprite roomEventIcon;
    public Sprite nextBattleArmorIcon;
    public Sprite nextBattleFixedDiceIcon;
    public Sprite blockNextDamageIcon;
    public Sprite nextBattleDamageUpIcon;
    public Sprite relicIcon;
    public Sprite forgeIcon;

    [Header("State Colors")]
    public Color passedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color currentColor = new Color(0.5f, 1f, 1f, 1f);
    public Color futureColor = Color.white;
    public Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
}
