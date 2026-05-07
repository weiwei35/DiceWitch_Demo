using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ForgeResourceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;
    private ForgeResourceSO _resource;

    public ForgeResourceSO Resource => _resource;

    public void Setup(ForgeResourceSO res)
    {
        _resource = res;
        if (iconImage != null && res.icon != null)
            iconImage.sprite = res.icon;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_resource == null || TooltipSystem.Instance == null) return;

        string rarityStars = new string('★', _resource.rarity);
        string content = $"<color=#888888>{rarityStars} 稀有度: {_resource.rarity}/3  |  属性: {_resource.resourceType}</color>";
        if (!string.IsNullOrEmpty(_resource.description))
            content += $"\n\n{_resource.description}";

        TooltipSystem.Instance.Show(content, _resource.resourceName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }
}
