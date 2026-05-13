using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ForgeDiceSelector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;
    private PlayerDice _dice;

    public void Setup(PlayerDice dice, Sprite fallbackIcon = null)
    {
        _dice = dice;
        if (iconImage != null)
            iconImage.sprite = dice.icon != null ? dice.icon : dice.boundAbility?.icon ?? fallbackIcon;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_dice == null || TooltipSystem.Instance == null) return;

        string content = "";
        if (_dice.boundAbility != null)
        {
            content += $"<color=yellow>★ {_dice.boundAbility.abilityName}</color>";
            if (!string.IsNullOrEmpty(_dice.boundAbility.description))
                content += $"\n{_dice.boundAbility.description}";
        }
        else
        {
            content += "<i>无特殊能力</i>";
        }

        int forged = 0;
        if (_dice.forgeSlots != null)
        {
            bool hasForged = false;
            foreach (var s in _dice.forgeSlots)
            {
                if (s.isForged && s.affix != null)
                {
                    if (!hasForged) { content += "\n\n<color=#FF8800>已刻印词条:</color>"; hasForged = true; }
                    content += $"\n  T{s.tier}: {s.affix.affixName}";
                    forged++;
                }
            }
        }
        content += $"\n\n锻造进度: {forged}/3";

        TooltipSystem.Instance.Show(content, _dice.diceName);
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
