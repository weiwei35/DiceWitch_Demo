using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 锻造面板中骰子选择按钮的显示与提示逻辑。
/// 负责展示骰子图标，并在悬停时显示该骰子的能力与已刻印进度。
/// </summary>
public class ForgeDiceSelector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image iconImage;
    private PlayerDice _dice;

    /// <summary>
    /// 绑定要展示的骰子，并刷新按钮图标。
    /// </summary>
    /// <param name="dice">该按钮代表的玩家骰子。</param>
    /// <param name="fallbackIcon">骰子没有自身图标时使用的默认图标。</param>
    public void Setup(PlayerDice dice, Sprite fallbackIcon = null)
    {
        _dice = dice;
        if (iconImage != null)
            iconImage.sprite = dice.icon != null ? dice.icon : dice.boundAbility?.icon ?? fallbackIcon;
    }

    /// <summary>
    /// 鼠标悬停时显示骰子的能力、已刻印词条和锻造进度。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
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

    /// <summary>
    /// 鼠标离开时隐藏骰子提示。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    /// <summary>
    /// 点击骰子按钮时关闭悬停提示，真正的选择行为由外层按钮事件处理。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }
}
