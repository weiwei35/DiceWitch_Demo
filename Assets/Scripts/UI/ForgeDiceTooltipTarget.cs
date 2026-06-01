using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;

public class ForgeDiceTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private PlayerDice _dice;

    public void Setup(PlayerDice dice)
    {
        _dice = dice;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_dice == null || TooltipSystem.Instance == null) return;
        TooltipSystem.Instance.Show(BuildTooltipDescription(_dice), _dice.diceName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

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

        RuntimeSlotAttribute slotAttribute = FindRuntimeSlotAttribute(dice);
        if (slotAttribute != null && slotAttribute.data != null)
        {
            hasProperty = true;
            sb.AppendLine($"{slotAttribute.data.attributeName} Lv.{slotAttribute.level}");
            sb.AppendLine($"效果: +{slotAttribute.GetCurrentValue()}");
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

    private RuntimeSlotAttribute FindRuntimeSlotAttribute(PlayerDice dice)
    {
        if (dice == null || MagicCircleManager.Instance == null) return null;

        foreach (var slot in MagicCircleManager.Instance.magicSlots)
        {
            if (slot != null && slot.currentDice == dice)
                return slot.currentAttribute;
        }

        return null;
    }
}
