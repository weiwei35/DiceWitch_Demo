using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冥想界面的骰子选择与预览模块。
/// 负责可锻造骰子列表、左右切换、中心/当前图标、tooltip 和切换动画。
/// </summary>
public class ForgeDiceSelectionPanel : MonoBehaviour
{
    [Header("Selection")]
    public Button previousDiceButton;
    public Button nextDiceButton;

    [Header("Preview")]
    public Image spellIconImage;
    public Image currentDiceIcon;

    [SerializeField, HideInInspector] private float iconBreathScale = 1.06f;
    [SerializeField, HideInInspector] private float iconBreathDuration = 1.8f;
    [SerializeField, HideInInspector] private float diceSwitchDuration = 0.22f;

    private readonly List<PlayerDice> _availableDice = new List<PlayerDice>();
    private Func<bool> _canSwitch;
    private Action<PlayerDice> _onSelectedDiceChanged;
    private PlayerDice _selectedDice;
    private int _selectedDiceIndex;

    public PlayerDice SelectedDice => _selectedDice;
    public Image SpellIconImage => spellIconImage;
    public Image CurrentDiceIcon => currentDiceIcon;
    public int AvailableDiceCount => _availableDice.Count;

    public void Initialize(Func<bool> canSwitch, Action<PlayerDice> onSelectedDiceChanged)
    {
        _canSwitch = canSwitch;
        _onSelectedDiceChanged = onSelectedDiceChanged;
        BindButtons();
    }

    public void RefreshDiceList()
    {
        _availableDice.Clear();

        List<PlayerDice> allDice = MagicCircleManager.Instance != null
            ? MagicCircleManager.Instance.allOwnedDice
            : null;

        if (allDice != null)
        {
            foreach (PlayerDice dice in allDice)
            {
                if (dice == null || GetForgedCount(dice) >= 3) continue;
                _availableDice.Add(dice);
            }
        }

    }

    public void SelectByIndex(int index)
    {
        if (_availableDice.Count == 0)
        {
            _selectedDice = null;
            _selectedDiceIndex = 0;
        }
        else
        {
            _selectedDiceIndex = Mathf.Clamp(index, 0, _availableDice.Count - 1);
            _selectedDice = _availableDice[_selectedDiceIndex];
        }

        UpdatePreview();
        PlaySwitchAnimation();
        _onSelectedDiceChanged?.Invoke(_selectedDice);
    }

    public void SelectDice(PlayerDice dice)
    {
        int index = _availableDice.IndexOf(dice);
        if (index >= 0)
            SelectByIndex(index);
        else
            SelectByIndex(Mathf.Min(_selectedDiceIndex, Mathf.Max(0, _availableDice.Count - 1)));
    }

    public void StartBreath()
    {
        ForgeUIEffects.StartBreath(spellIconImage, iconBreathScale, iconBreathDuration);
    }

    public void StopBreath()
    {
        ForgeUIEffects.StopIconTween(spellIconImage);
        ForgeUIEffects.StopIconTween(currentDiceIcon);
    }

    public void SetNavigationInteractable(bool interactable)
    {
        bool canNavigate = interactable && _availableDice.Count > 1;
        if (previousDiceButton != null) previousDiceButton.interactable = canNavigate;
        if (nextDiceButton != null) nextDiceButton.interactable = canNavigate;
    }

    private void BindButtons()
    {
        if (previousDiceButton != null)
        {
            previousDiceButton.onClick.RemoveListener(OnPreviousDiceClicked);
            previousDiceButton.onClick.AddListener(OnPreviousDiceClicked);
        }

        if (nextDiceButton != null)
        {
            nextDiceButton.onClick.RemoveListener(OnNextDiceClicked);
            nextDiceButton.onClick.AddListener(OnNextDiceClicked);
        }
    }

    private void OnPreviousDiceClicked()
    {
        if (!CanSwitch() || _availableDice.Count == 0) return;
        SelectByIndex((_selectedDiceIndex - 1 + _availableDice.Count) % _availableDice.Count);
    }

    private void OnNextDiceClicked()
    {
        if (!CanSwitch() || _availableDice.Count == 0) return;
        SelectByIndex((_selectedDiceIndex + 1) % _availableDice.Count);
    }

    private void UpdatePreview()
    {
        Sprite sprite = null;
        if (_selectedDice != null)
            sprite = _selectedDice.icon != null ? _selectedDice.icon : _selectedDice.boundAbility?.icon;
        if (sprite == null && MagicCircleManager.Instance != null)
            sprite = MagicCircleManager.Instance.defaultDiceIcon;

        SetPreviewIcon(spellIconImage, sprite);
        SetPreviewIcon(currentDiceIcon, sprite);
        SetupTooltipTarget(spellIconImage);
        SetupTooltipTarget(currentDiceIcon);
    }

    private void SetPreviewIcon(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void PlaySwitchAnimation()
    {
        ForgeUIEffects.PlayIconSwitch(spellIconImage, diceSwitchDuration, StartBreath);
        ForgeUIEffects.PlayIconSwitch(currentDiceIcon, diceSwitchDuration);
    }

    private void SetupTooltipTarget(Image image)
    {
        if (image == null) return;

        ForgeDiceTooltipTarget tooltipTarget = image.GetComponent<ForgeDiceTooltipTarget>();
        if (tooltipTarget != null)
            tooltipTarget.Setup(_selectedDice);
    }

    private bool CanSwitch()
    {
        return _canSwitch == null || _canSwitch();
    }

    private static int GetForgedCount(PlayerDice dice)
    {
        if (dice?.forgeSlots == null) return 0;

        int count = 0;
        foreach (ForgeSlot slot in dice.forgeSlots)
            if (slot != null && slot.isForged) count++;
        return count;
    }
}
