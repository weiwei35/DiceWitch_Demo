using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/Presentation Catalog")]
public class MapPresentationCatalogSO : ScriptableObject
{
    [System.Serializable]
    public class RoomIconEntry
    {
        public GameEnums.RoomType roomType;
        public Sprite icon;
        public string displayName;
    }

    [System.Serializable]
    public class NodeEffectEntry
    {
        public GameEnums.BoardNodeType nodeType;
        public Sprite positiveIcon;
        public Sprite negativeIcon;
        public Sprite neutralIcon;
        public bool showValue = true;
        public bool showPlusForPositiveValue = true;
        public string tooltipHeader;
        [TextArea] public string positiveTooltipTemplate;
        [TextArea] public string negativeTooltipTemplate;
        [TextArea] public string neutralTooltipTemplate;
        public string floatingTextTemplate;
        public Color floatingTextColor = Color.white;
    }

    [Header("Room Icons")]
    public Sprite unknownRoomIcon;
    public string unknownRoomDisplayName = "未知";
    public List<RoomIconEntry> roomIcons = new List<RoomIconEntry>();

    [Header("Node Effects")]
    public Sprite forgeIcon;
    public List<NodeEffectEntry> nodeEffects = new List<NodeEffectEntry>();

    [Header("Node State Colors")]
    public Color passedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color currentColor = new Color(0.5f, 1f, 1f, 1f);
    public Color futureColor = Color.white;
    public Color disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);

    [ContextMenu("Fill Missing Default Entries")]
    public void FillDefaultEntries()
    {
        AddRoom(GameEnums.RoomType.Start, "起点");
        AddRoom(GameEnums.RoomType.Battle, "普通战斗");
        AddRoom(GameEnums.RoomType.Elite, "精英战斗");
        AddRoom(GameEnums.RoomType.Boss, "Boss");
        AddRoom(GameEnums.RoomType.Rest, "休息点");
        AddRoom(GameEnums.RoomType.Shop, "商店");
        AddRoom(GameEnums.RoomType.Treasure, "宝箱");
        AddRoom(GameEnums.RoomType.Event, "事件");
        AddRoom(GameEnums.RoomType.Unknown, "未知");

        AddEffect(GameEnums.BoardNodeType.加减Hp, "生命变化", true, true,
            "<color=#008800>恢复 {value} 点生命值</color>",
            "<color=#FF0000>失去 {abs} 点生命值</color>",
            "",
            "{signed} HP",
            Color.green);

        AddEffect(GameEnums.BoardNodeType.加减资源, "粉尘变化", true, true,
            "<color=#0000FF>获得 {value} 点粉尘</color>",
            "<color=#FF0000>失去 {abs} 点粉尘</color>",
            "",
            "{signed} 粉尘",
            new Color(1f, 0.8f, 0f));

        AddEffect(GameEnums.BoardNodeType.事件, "未知的挑战", false, true,
            "",
            "",
            "触发该房间的主事件或战斗",
            "",
            Color.white);

        AddEffect(GameEnums.BoardNodeType.一次护甲, "坚固防线", true, true,
            "<color=#3333FF>下场战斗开局获得 {value} 点护甲</color>",
            "",
            "",
            "开局护甲 +{value}",
            new Color(0.2f, 0.6f, 1f));

        AddEffect(GameEnums.BoardNodeType.骰子点数必中, "命运干预", true, false,
            "<color=#AA00AA>下场战斗第一回合必定有一枚骰子掷出 {value} 点</color>",
            "",
            "",
            "必定掷出 {value}",
            new Color(0.8f, 0.2f, 1f));

        AddEffect(GameEnums.BoardNodeType.抵消下一次伤害, "神圣护盾", false, true,
            "",
            "",
            "<color=#0088FF>抵消下一次受到的任何伤害\n(地图陷阱或战斗通用)</color>",
            "获得圣盾",
            Color.cyan);

        AddEffect(GameEnums.BoardNodeType.一次伤害增加, "磨刀石", true, true,
            "<color=#FF8800>下场战斗期间，所有伤害增加 {value} 点</color>",
            "",
            "",
            "伤害 +{value}",
            new Color(1f, 0.5f, 0f));

        AddEffect(GameEnums.BoardNodeType.遗物, "远古遗物", false, true,
            "",
            "",
            "<color=#FFD700>获得一件随机遗物</color>",
            "获得遗物",
            Color.yellow);
    }

    public Sprite GetRoomIcon(RoomDataSO roomData)
    {
        if (roomData == null) return unknownRoomIcon;

        return GetRoomIcon(roomData.roomType);
    }

    public Sprite GetRoomIcon(GameEnums.RoomType roomType)
    {
        RoomIconEntry entry = FindRoomIconEntry(roomType);
        return entry != null && entry.icon != null ? entry.icon : unknownRoomIcon;
    }

    public string GetRoomDisplayName(GameEnums.RoomType roomType)
    {
        RoomIconEntry entry = FindRoomIconEntry(roomType);
        if (entry != null && !string.IsNullOrEmpty(entry.displayName))
            return entry.displayName;

        return string.IsNullOrEmpty(unknownRoomDisplayName) ? roomType.ToString() : unknownRoomDisplayName;
    }

    public NodeEffectEntry FindNodeEffectEntry(GameEnums.BoardNodeType nodeType)
    {
        foreach (NodeEffectEntry entry in nodeEffects)
        {
            if (entry != null && entry.nodeType == nodeType)
                return entry;
        }

        return null;
    }

    public Sprite GetNodeEffectIcon(GameEnums.BoardNodeType nodeType, int value)
    {
        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        if (entry == null) return null;

        if (value > 0 && entry.positiveIcon != null) return entry.positiveIcon;
        if (value < 0 && entry.negativeIcon != null) return entry.negativeIcon;
        return entry.neutralIcon;
    }

    public bool ShouldShowNodeValue(GameEnums.BoardNodeType nodeType, int value)
    {
        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        return entry != null && entry.showValue && value != 0;
    }

    public string FormatNodeValue(GameEnums.BoardNodeType nodeType, int value)
    {
        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        if (entry != null && value > 0 && entry.showPlusForPositiveValue)
            return $"+{value}";

        return value.ToString();
    }

    public string BuildNodeTooltip(GameEnums.BoardNodeType nodeType, int value)
    {
        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        if (entry == null) return "无事发生";

        string template = entry.neutralTooltipTemplate;
        if (value > 0 && !string.IsNullOrEmpty(entry.positiveTooltipTemplate))
            template = entry.positiveTooltipTemplate;
        else if (value < 0 && !string.IsNullOrEmpty(entry.negativeTooltipTemplate))
            template = entry.negativeTooltipTemplate;

        return FormatTemplate(template, value);
    }

    public string GetNodeTooltipHeader(GameEnums.BoardNodeType nodeType)
    {
        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        return entry != null && !string.IsNullOrEmpty(entry.tooltipHeader) ? entry.tooltipHeader : nodeType.ToString();
    }

    public bool TryBuildFloatingText(GameEnums.BoardNodeType nodeType, int value, out string text, out Color color)
    {
        text = "";
        color = Color.white;

        NodeEffectEntry entry = FindNodeEffectEntry(nodeType);
        if (entry == null || string.IsNullOrEmpty(entry.floatingTextTemplate))
            return false;

        text = FormatTemplate(entry.floatingTextTemplate, value);
        color = entry.floatingTextColor;
        return !string.IsNullOrEmpty(text);
    }

    private RoomIconEntry FindRoomIconEntry(GameEnums.RoomType roomType)
    {
        foreach (RoomIconEntry entry in roomIcons)
        {
            if (entry != null && entry.roomType == roomType)
                return entry;
        }

        return null;
    }

    private void AddRoom(GameEnums.RoomType roomType, string displayName)
    {
        RoomIconEntry existingEntry = FindRoomIconEntry(roomType);
        if (existingEntry != null)
        {
            if (string.IsNullOrEmpty(existingEntry.displayName))
                existingEntry.displayName = displayName;

            return;
        }

        roomIcons.Add(new RoomIconEntry
        {
            roomType = roomType,
            displayName = displayName
        });
    }

    private void AddEffect(
        GameEnums.BoardNodeType nodeType,
        string tooltipHeader,
        bool showValue,
        bool showPlusForPositiveValue,
        string positiveTooltip,
        string negativeTooltip,
        string neutralTooltip,
        string floatingText,
        Color floatingColor)
    {
        NodeEffectEntry existingEntry = FindNodeEffectEntry(nodeType);
        if (existingEntry != null)
        {
            FillMissingEffectFields(existingEntry, tooltipHeader, showValue, showPlusForPositiveValue, positiveTooltip, negativeTooltip, neutralTooltip, floatingText, floatingColor);
            return;
        }

        nodeEffects.Add(new NodeEffectEntry
        {
            nodeType = nodeType,
            tooltipHeader = tooltipHeader,
            showValue = showValue,
            showPlusForPositiveValue = showPlusForPositiveValue,
            positiveTooltipTemplate = positiveTooltip,
            negativeTooltipTemplate = negativeTooltip,
            neutralTooltipTemplate = neutralTooltip,
            floatingTextTemplate = floatingText,
            floatingTextColor = floatingColor
        });
    }

    private void FillMissingEffectFields(
        NodeEffectEntry entry,
        string tooltipHeader,
        bool showValue,
        bool showPlusForPositiveValue,
        string positiveTooltip,
        string negativeTooltip,
        string neutralTooltip,
        string floatingText,
        Color floatingColor)
    {
        if (string.IsNullOrEmpty(entry.tooltipHeader))
            entry.tooltipHeader = tooltipHeader;
        if (string.IsNullOrEmpty(entry.positiveTooltipTemplate))
            entry.positiveTooltipTemplate = positiveTooltip;
        if (string.IsNullOrEmpty(entry.negativeTooltipTemplate))
            entry.negativeTooltipTemplate = negativeTooltip;
        if (string.IsNullOrEmpty(entry.neutralTooltipTemplate))
            entry.neutralTooltipTemplate = neutralTooltip;
        if (string.IsNullOrEmpty(entry.floatingTextTemplate))
            entry.floatingTextTemplate = floatingText;
        if (entry.floatingTextColor == default)
            entry.floatingTextColor = floatingColor;
    }

    private static string FormatTemplate(string template, int value)
    {
        if (string.IsNullOrEmpty(template)) return "";

        return template
            .Replace("{value}", value.ToString())
            .Replace("{abs}", Mathf.Abs(value).ToString())
            .Replace("{signed}", value > 0 ? $"+{value}" : value.ToString());
    }
}
