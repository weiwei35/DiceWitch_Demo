using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardDiceSelectionPanel : MonoBehaviour
{
    public static RewardDiceSelectionPanel Instance;

    [Header("Panel")]
    public GameObject panelRoot;
    public Transform slotsContainer;
    public GameObject slotButtonPrefab;

    [Header("Spell Preview")]
    public Image spellIconImage;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI spellNameText;
    public TextMeshProUGUI spellDescriptionText;

    private DiceAbilitySO _pendingSpell;
    private Action<MagicCircleSlot> _onSlotSelected;
    private MagicCircleSlot _pendingOverwriteSlot;

    private void Awake()
    {
        Instance = this;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public static RewardDiceSelectionPanel GetConfigured()
    {
        if (Instance == null)
        {
            Debug.LogError("RewardDiceSelectionPanel 未配置。请在场景或奖励 UI 预制体中放置 RewardDiceSelectionPanel，并配置 panelRoot、slotsContainer、slotButtonPrefab。");
        }
        return Instance;
    }

    public void Show(DiceAbilitySO spell, Action<MagicCircleSlot> onSlotSelected)
    {
        if (!ValidateReferences()) return;

        _pendingSpell = spell;
        _onSlotSelected = onSlotSelected;
        _pendingOverwriteSlot = null;

        RefreshSpellPreview();
        GenerateSlots();

        if (panelRoot != null)
        {
            panelRoot.transform.SetAsLastSibling();
            panelRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        TooltipSystem.Instance?.Hide();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void GenerateSlots()
    {
        if (slotsContainer == null || MagicCircleManager.Instance == null) return;
        if (slotButtonPrefab == null)
        {
            Debug.LogError("RewardDiceSelectionPanel.slotButtonPrefab 未配置，无法生成奖励骰子选择按钮。");
            return;
        }

        foreach (Transform child in slotsContainer)
            Destroy(child.gameObject);

        foreach (MagicCircleSlot slot in MagicCircleManager.Instance.magicSlots)
        {
            GameObject slotObject = Instantiate(slotButtonPrefab, slotsContainer);

            RewardDiceSlotButton slotButton = slotObject.GetComponent<RewardDiceSlotButton>();
            if (slotButton == null)
            {
                Debug.LogError("RewardDiceSelectionPanel.slotButtonPrefab 缺少 RewardDiceSlotButton 组件。");
                Destroy(slotObject);
                continue;
            }

            slotButton.Setup(slot, HandleSlotSelected);
        }
    }

    private void HandleSlotSelected(MagicCircleSlot slot)
    {
        if (slot == null || !slot.isUnlocked || slot.currentDice == null) return;

        if (slot.currentDice.boundAbility != null)
        {
            if (_pendingOverwriteSlot != slot)
            {
                _pendingOverwriteSlot = slot;
                if (titleText != null)
                    titleText.text = $"将覆盖“{slot.currentDice.boundAbility.abilityName}”\n再次点击该骰子确认；点击其他骰子取消";
                return;
            }
        }

        _pendingOverwriteSlot = null;
        _onSlotSelected?.Invoke(slot);
    }

    private void RefreshSpellPreview()
    {
        if (titleText != null)
            titleText.text = "选择要附加法术的骰子";

        if (spellNameText != null)
            spellNameText.text = _pendingSpell != null ? _pendingSpell.abilityName : "未知法术";

        if (spellDescriptionText != null)
            spellDescriptionText.text = _pendingSpell != null ? _pendingSpell.description : "";

        if (spellIconImage != null)
        {
            spellIconImage.sprite = _pendingSpell != null ? _pendingSpell.icon : null;
            spellIconImage.gameObject.SetActive(spellIconImage.sprite != null);
        }
    }

    private bool ValidateReferences()
    {
        if (panelRoot != null && slotsContainer != null && slotButtonPrefab != null) return true;

        Debug.LogError("RewardDiceSelectionPanel 引用未配置完整。必须配置 panelRoot、slotsContainer、slotButtonPrefab。");
        return false;
    }
}
