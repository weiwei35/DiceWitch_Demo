using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 冥想背包中的材料按钮。
/// 负责显示材料图标、库存数量，并在悬停时显示材料说明。
/// </summary>
public class ForgeResourceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;
    public TMPro.TextMeshProUGUI countText;
    private ForgeResourceSO _resource;

    public ForgeResourceSO Resource => _resource;

    /// <summary>
    /// 绑定材料数据并刷新图标和数量。
    /// </summary>
    /// <param name="res">按钮代表的材料配置。</param>
    /// <param name="count">当前背包内该材料数量。</param>
    public void Setup(ForgeResourceSO res, int count)
    {
        _resource = res;
        if (iconImage != null && res.icon != null)
            iconImage.sprite = res.icon;
        RefreshCount(count);
    }

    /// <summary>
    /// 刷新材料数量文本。
    /// </summary>
    /// <param name="count">当前背包内该材料数量。</param>
    public void RefreshCount(int count)
    {
        if (countText != null)
            countText.text = count > 0 ? $"x{count}" : "";
    }

    /// <summary>
    /// 鼠标悬停时显示材料稀有度、属性和描述。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_resource == null || TooltipSystem.Instance == null) return;

        string rarityStars = new string('★', _resource.rarity);
        string content = $"<color=#888888>{rarityStars} 稀有度: {_resource.rarity}/3  |  属性: {_resource.resourceType}</color>";
        if (!string.IsNullOrEmpty(_resource.description))
            content += $"\n\n{_resource.description}";

        TooltipSystem.Instance.Show(content, _resource.resourceName);
    }

    /// <summary>
    /// 鼠标离开时隐藏材料提示。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    /// <summary>
    /// 点击材料按钮时关闭悬停提示，材料填入逻辑由外层按钮事件处理。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }
}
