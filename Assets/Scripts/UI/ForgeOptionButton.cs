using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ForgeOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image iconImage;
    public Button attachButton;
    private ForgeAffixSO _affix;

    public ForgeAffixSO Affix => _affix;

    public void Setup(ForgeAffixSO affix, bool showAttach)
    {
        _affix = affix;
        if (iconImage != null && affix.icon != null) iconImage.sprite = affix.icon;
        if (attachButton != null && attachButton.gameObject != gameObject)
            attachButton.gameObject.SetActive(showAttach);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_affix == null || TooltipSystem.Instance == null) return;

        string content = $"<color=#888888>标签: {_affix.tag}  |  品质: T{_affix.tier} / Q{_affix.quality}</color>";
        if (!string.IsNullOrEmpty(_affix.description))
            content += $"\n\n{_affix.description}";

        TooltipSystem.Instance.Show(content, _affix.affixName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_affix == null) return;
        ForgeUIManager.Instance?.OnOptionPressStart(_affix, transform as RectTransform);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ForgeUIManager.Instance?.OnOptionPressEnd();
    }
}
