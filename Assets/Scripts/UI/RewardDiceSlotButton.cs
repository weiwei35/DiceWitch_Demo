using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RewardDiceSlotButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Refs")]
    public Image iconImage;
    public Image borderImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI slotText;
    public Button button;

    private MagicCircleSlot _slot;
    private Action<MagicCircleSlot> _onSelected;

    public void Setup(MagicCircleSlot slot, Action<MagicCircleSlot> onSelected)
    {
        _slot = slot;
        _onSelected = onSelected;

        if (button == null || iconImage == null)
        {
            Debug.LogError($"{nameof(RewardDiceSlotButton)} 引用未配置完整。请在奖励骰子槽位 prefab 上配置 button 和 iconImage。骰子名称、槽位信息会通过 tooltip 显示，nameText/slotText 可不配置。", this);
            return;
        }

        bool canSelect = slot != null && slot.isUnlocked && slot.currentDice != null;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.interactable = canSelect;
            if (canSelect)
                button.onClick.AddListener(() => _onSelected?.Invoke(_slot));
        }

        if (borderImage != null)
            borderImage.color = canSelect ? Color.white : Color.gray;

        if (slotText != null)
            slotText.text = slot != null ? $"槽位 {slot.slotID + 1}" : "槽位";

        if (nameText != null)
            nameText.text = GetDiceName();

        if (iconImage != null)
        {
            iconImage.sprite = GetDiceIcon();
            iconImage.color = canSelect ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            iconImage.gameObject.SetActive(iconImage.sprite != null);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_slot == null || !_slot.isUnlocked || _slot.currentDice == null || TooltipSystem.Instance == null) return;
        TooltipSystem.Instance.Show(BuildTooltipDescription(_slot), GetDiceName());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    private void OnDisable()
    {
        TooltipSystem.Instance?.Hide();
    }

    private string GetDiceName()
    {
        if (_slot == null) return "未知槽位";
        if (!_slot.isUnlocked) return "未解锁";
        if (_slot.currentDice == null) return "空槽位";
        return string.IsNullOrEmpty(_slot.currentDice.diceName) ? "普通骰子" : _slot.currentDice.diceName;
    }

    private Sprite GetDiceIcon()
    {
        if (_slot == null || _slot.currentDice == null) return null;

        PlayerDice dice = _slot.currentDice;
        if (dice.icon != null) return dice.icon;
        if (dice.boundAbility != null && dice.boundAbility.icon != null) return dice.boundAbility.icon;
        return MagicCircleManager.Instance != null ? MagicCircleManager.Instance.defaultDiceIcon : null;
    }

    private string BuildTooltipDescription(MagicCircleSlot slot)
    {
        if (slot.currentDice == null) return "空槽位";

        StringBuilder sb = new StringBuilder();
        PlayerDice dice = slot.currentDice;
        bool hasProperty = false;

        if (dice.boundAbility != null)
        {
            hasProperty = true;
            sb.AppendLine($"<color=yellow>★ {dice.boundAbility.abilityName}</color>");
            if (!string.IsNullOrEmpty(dice.boundAbility.description))
                sb.AppendLine(dice.boundAbility.description);
            sb.AppendLine();
        }

        if (slot.currentAttribute != null && slot.currentAttribute.data != null)
        {
            hasProperty = true;
            sb.AppendLine($"{slot.currentAttribute.data.attributeName} Lv.{slot.currentAttribute.level}");
            sb.AppendLine($"效果: +{slot.currentAttribute.GetCurrentValue()}");
            sb.AppendLine();
        }

        if (dice.forgeSlots != null)
        {
            foreach (ForgeSlot forgeSlot in dice.forgeSlots)
            {
                if (forgeSlot == null || !forgeSlot.isForged || forgeSlot.affix == null) continue;

                hasProperty = true;
                sb.AppendLine($"<color=#FF8800>◆ {forgeSlot.affix.affixName}</color>");
                if (!string.IsNullOrEmpty(forgeSlot.affix.description))
                    sb.AppendLine(forgeSlot.affix.description);
                sb.AppendLine();
            }
        }

        if (!hasProperty)
            sb.Append("<i>没有任何特殊属性</i>");

        return sb.ToString();
    }
}
