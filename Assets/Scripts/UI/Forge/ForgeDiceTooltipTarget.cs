using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 锻造面板中骰子/法术图标的 tooltip 目标。
/// 用统一格式展示骰子能力和已刻印词条。
/// </summary>
public class ForgeDiceTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerDice _dice;

    /// <summary>
    /// 绑定需要展示提示的骰子。
    /// </summary>
    /// <param name="dice">当前 UI 图标对应的玩家骰子。</param>
    public void Setup(PlayerDice dice)
    {
        _dice = dice;
    }

    /// <summary>
    /// 鼠标悬停时构建并显示骰子完整提示。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_dice == null || TooltipSystem.Instance == null) return;
        TooltipSystem.Instance.Show(BuildTooltipDescription(_dice), _dice.diceName);
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
    /// 拼接骰子在锻造界面中使用的提示文本。
    /// </summary>
    /// <param name="dice">需要描述的玩家骰子。</param>
    /// <returns>可直接传给 TooltipSystem 的富文本描述。</returns>
    private string BuildTooltipDescription(PlayerDice dice)
    {
        StringBuilder sb = new StringBuilder();
        bool hasProperty = false;

        if (dice.boundAbility != null)
        {
            hasProperty = true;
            sb.AppendLine($"<color=yellow>★ {dice.boundAbility.abilityName}</color>");
            if (!string.IsNullOrEmpty(dice.boundAbility.description))
                sb.AppendLine(dice.boundAbility.description);
            sb.AppendLine();
        }

        if (dice.forgeSlots != null)
        {
            foreach (var slot in dice.forgeSlots)
            {
                if (slot == null || !slot.isForged || slot.affix == null) continue;

                hasProperty = true;
                sb.AppendLine($"<color=#FF8800>◆ {slot.affix.affixName}</color>");
                if (!string.IsNullOrEmpty(slot.affix.description))
                    sb.AppendLine(slot.affix.description);
                sb.AppendLine();
            }
        }

        if (!hasProperty)
            sb.Append("<i>没有任何特殊属性</i>");

        return sb.ToString();
    }

}
