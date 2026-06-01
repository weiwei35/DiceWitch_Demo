using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForgeUIManager : MonoBehaviour
{
    public static ForgeUIManager Instance;

    [Header("Panel")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Dice Selection")]
    public Transform diceSelectContainer;
    public GameObject diceSelectButtonPrefab;
    public Image spellIconImage;
    public Image currentDiceIcon;
    public Button previousDiceButton;
    public Button nextDiceButton;

    [Header("Resource Input")]
    public Transform resourceContainer;
    public GameObject resourceButtonPrefab;
    public List<Button> materialSlotButtons = new List<Button>();
    public GameObject bagPanel;
    public Transform bagItemContainer;
    public Button bagPreviousButton;
    public Button bagNextButton;
    public Button bagCloseButton;

    [Header("Options")]
    public Transform optionsContainer;
    public GameObject optionButtonPrefab;
    public RectTransform optionPlacementCenter;
    public bool positionOptionsAroundSpellIcon = true;
    public List<Vector2> optionOffsets = new List<Vector2>
    {
        new Vector2(-170f, 0f),
        new Vector2(0f, 120f),
        new Vector2(170f, 0f)
    };
    public List<Vector2> committedOptionOffsets = new List<Vector2>
    {
        new Vector2(-210f, -120f),
        new Vector2(0f, 145f),
        new Vector2(210f, -120f)
    };
    public bool showCommittedOptionLines = true;
    public Color committedOptionLineColor = new Color(0.42f, 0.58f, 1f, 0.8f);
    public float committedOptionLineThickness = 4f;

    [Header("Option Animation")]
    public float optionAppearDuration = 0.35f;
    public float optionFloatDistance = 8f;
    public float optionFloatDuration = 1.6f;
    public float iconBreathScale = 1.06f;
    public float iconBreathDuration = 1.8f;
    public float diceSwitchDuration = 0.22f;
    public float materialClearInterval = 0.08f;
    public float materialPopDuration = 0.18f;
    public float materialReplaceDuration = 0.24f;
    public float affixCommitDuration = 0.45f;

    [Header("Actions")]
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonLabel;
    public TextMeshProUGUI stepText;
    public TextMeshProUGUI statusText;

    private const int MaterialSlotCount = 3;
    private Action _onComplete;
    private PlayerDice _selectedDice;
    private int _selectedDiceIndex;
    private int _editingSlotIndex = -1;
    private readonly List<PlayerDice> _availableDice = new List<PlayerDice>();
    private readonly List<Image> _materialSlotImages = new List<Image>();
    private readonly List<Sprite> _materialSlotDefaultSprites = new List<Sprite>();
    private readonly List<Color> _materialSlotDefaultColors = new List<Color>();
    private readonly ForgeResourceSO[] _slotResources = new ForgeResourceSO[MaterialSlotCount];
    private bool _isCommittingAffix;

    void Awake() { Instance = this; }

    void Start()
    {
        BindStaticButtons();
    }

    public void ShowForge(Action onComplete)
    {
        _onComplete = onComplete;
        BindStaticButtons();
        CacheMaterialSlots();
        RefundSlotResources();
        ClearSlots(shouldRefund: false);
        RefreshDiceList();
        SelectDiceByIndex(0);
        RefreshOptions();
        RefreshBag();

        if (bagPanel != null) bagPanel.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(true);
        StartPreviewBreath();
        UpdateUI();
    }

    public void Hide()
    {
        StopPreviewBreath();
        if (panelRoot != null) panelRoot.SetActive(false);
        TooltipSystem.Instance?.Hide();
    }

    private void BindStaticButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            if (!HasPersistentListener(closeButton, nameof(OnCloseClicked)))
                closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (previousDiceButton != null)
        {
            previousDiceButton.onClick.RemoveListener(OnPreviousDiceClicked);
            if (!HasPersistentListener(previousDiceButton, nameof(OnPreviousDiceClicked)))
                previousDiceButton.onClick.AddListener(OnPreviousDiceClicked);
        }

        if (nextDiceButton != null)
        {
            nextDiceButton.onClick.RemoveListener(OnNextDiceClicked);
            if (!HasPersistentListener(nextDiceButton, nameof(OnNextDiceClicked)))
                nextDiceButton.onClick.AddListener(OnNextDiceClicked);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (!HasPersistentListener(confirmButton, nameof(OnConfirmClicked)))
                confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (bagCloseButton != null)
        {
            bagCloseButton.onClick.RemoveListener(CloseBagPanel);
            if (!HasPersistentListener(bagCloseButton, nameof(CloseBagPanel)))
                bagCloseButton.onClick.AddListener(CloseBagPanel);
        }
    }

    private bool HasPersistentListener(Button button, string methodName)
    {
        if (button == null) return false;

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == this
                && button.onClick.GetPersistentMethodName(i) == methodName)
                return true;
        }

        return false;
    }

    private void OnCloseClicked()
    {
        TooltipSystem.Instance?.Hide();
        RefundSlotResources();
        ClearSlots(shouldRefund: false);
        Hide();
        _onComplete?.Invoke();
    }

    public void CloseBagPanel()
    {
        TooltipSystem.Instance?.Hide();
        _editingSlotIndex = -1;
        if (bagPanel != null) bagPanel.SetActive(false);
    }

    private void RefreshDiceList()
    {
        _availableDice.Clear();

        var allDice = MagicCircleManager.Instance != null
            ? MagicCircleManager.Instance.allOwnedDice
            : null;

        if (allDice != null)
        {
            foreach (var dice in allDice)
            {
                if (dice == null || GetForgedCount(dice) >= 3) continue;
                _availableDice.Add(dice);
            }
        }

        bool useCarouselDiceView = currentDiceIcon != null || previousDiceButton != null || nextDiceButton != null;
        if (!useCarouselDiceView && diceSelectContainer != null && diceSelectButtonPrefab != null)
        {
            foreach (Transform child in diceSelectContainer) Destroy(child.gameObject);

            foreach (var dice in _availableDice)
            {
                GameObject btnObj = Instantiate(diceSelectButtonPrefab, diceSelectContainer);
                var selector = btnObj.GetComponent<ForgeDiceSelector>();
                if (selector != null)
                    selector.Setup(dice, MagicCircleManager.Instance != null ? MagicCircleManager.Instance.defaultDiceIcon : null);

                var captured = dice;
                var button = btnObj.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => OnDiceClicked(captured));
            }
        }
    }

    private void OnDiceClicked(PlayerDice dice)
    {
        if (HasPendingOptions()) return;
        if (_isCommittingAffix) return;

        int index = _availableDice.IndexOf(dice);
        if (index >= 0) SelectDiceByIndex(index);
    }

    private void OnPreviousDiceClicked()
    {
        if (HasPendingOptions() || _availableDice.Count == 0) return;
        if (_isCommittingAffix) return;
        SelectDiceByIndex((_selectedDiceIndex - 1 + _availableDice.Count) % _availableDice.Count);
    }

    private void OnNextDiceClicked()
    {
        if (HasPendingOptions() || _availableDice.Count == 0) return;
        if (_isCommittingAffix) return;
        SelectDiceByIndex((_selectedDiceIndex + 1) % _availableDice.Count);
    }

    private void SelectDiceByIndex(int index)
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

        UpdateDicePreview();
        PlayDiceSwitchAnimation();
        ClearSlotsForDiceSwitch();
        RefreshOptions();
        UpdateUI();
    }

    private void SelectDice(PlayerDice dice)
    {
        int index = _availableDice.IndexOf(dice);
        if (index >= 0)
            SelectDiceByIndex(index);
        else
            SelectDiceByIndex(Mathf.Min(_selectedDiceIndex, Mathf.Max(0, _availableDice.Count - 1)));
    }

    private void UpdateDicePreview()
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

    private void StartPreviewBreath()
    {
        StartBreath(spellIconImage);
    }

    private void StopPreviewBreath()
    {
        StopIconTween(spellIconImage);
        StopIconTween(currentDiceIcon);
    }

    private void StartBreath(Image image)
    {
        if (image == null || iconBreathScale <= 1f || iconBreathDuration <= 0f) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOScale(iconBreathScale, iconBreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopIconTween(Image image)
    {
        if (image == null) return;
        image.rectTransform.DOKill();
        image.rectTransform.localScale = Vector3.one;
    }

    private void PlayDiceSwitchAnimation()
    {
        PlayIconSwitch(spellIconImage);
        PlayIconSwitch(currentDiceIcon);
    }

    private void PlayIconSwitch(Image image)
    {
        if (image == null || diceSwitchDuration <= 0f) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one * 0.75f;
        rect.DOScale(1f, diceSwitchDuration).SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (image == spellIconImage)
                    StartBreath(image);
            });
    }

    private void SetupTooltipTarget(Image image)
    {
        if (image == null) return;

        ForgeDiceTooltipTarget tooltipTarget = image.GetComponent<ForgeDiceTooltipTarget>();
        if (tooltipTarget != null)
            tooltipTarget.Setup(_selectedDice);
    }

    private void OnMaterialSlotClicked(int index)
    {
        if (index < 0 || index >= MaterialSlotCount) return;
        if (_isCommittingAffix) return;
        if (HasPendingOptions() && !ForgeManager.Instance.CanForgeMore) return;

        if (_slotResources[index] != null && _editingSlotIndex == index && bagPanel != null && bagPanel.activeSelf)
        {
            RefundSlot(index);
            _editingSlotIndex = FindFirstEmptySlot();
            if (_editingSlotIndex < 0) CloseBagPanel();
            else RefreshBag();
            UpdateUI();
            return;
        }

        OpenBagForSlot(index);
    }

    private void OpenBagForSlot(int index)
    {
        _editingSlotIndex = index;
        if (bagPanel != null) bagPanel.SetActive(true);
        RefreshBag();
    }

    private void RefreshBag()
    {
        Transform container = bagItemContainer != null ? bagItemContainer : resourceContainer;
        if (container == null || resourceButtonPrefab == null || ForgeManager.Instance == null) return;

        foreach (Transform child in container) Destroy(child.gameObject);

        foreach (var res in ForgeManager.Instance.allResources)
        {
            if (res == null) continue;

            int count = ForgeManager.Instance.GetResourceCount(res);
            if (count <= 0) continue;

            GameObject btnObj = Instantiate(resourceButtonPrefab, container);

            var resBtn = btnObj.GetComponent<ForgeResourceButton>();
            if (resBtn != null) resBtn.Setup(res, count);

            var button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = true;
                var captured = res;
                button.onClick.AddListener(() => OnBagResourceClicked(captured));
            }
        }
    }

    private void OnBagResourceClicked(ForgeResourceSO resource)
    {
        if (_editingSlotIndex < 0 || _editingSlotIndex >= MaterialSlotCount) return;
        if (resource == null || ForgeManager.Instance == null) return;
        if (HasPendingOptions() && !ForgeManager.Instance.CanForgeMore) return;
        if (ForgeManager.Instance.GetResourceCount(resource) <= 0) return;

        ForgeResourceSO previous = _slotResources[_editingSlotIndex];
        if (previous != null) ForgeManager.Instance.RefundResource(previous);

        if (!ForgeManager.Instance.TryConsumeResource(resource))
        {
            if (previous != null) ForgeManager.Instance.TryConsumeResource(previous);
            return;
        }

        _slotResources[_editingSlotIndex] = resource;
        RefreshSlot(_editingSlotIndex);
        PlayMaterialSlotChangedAnimation(_editingSlotIndex, previous != null);

        AdvanceMaterialSelection();
        RefreshBag();
        UpdateUI();
    }

    private void AdvanceMaterialSelection()
    {
        int nextEmpty = FindFirstEmptySlot();
        if (nextEmpty >= 0)
        {
            _editingSlotIndex = nextEmpty;
            return;
        }

        _editingSlotIndex = -1;
        if (bagPanel != null) bagPanel.SetActive(false);
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < MaterialSlotCount; i++)
            if (_slotResources[i] == null) return i;
        return -1;
    }

    private void RefundSlot(int index)
    {
        if (index < 0 || index >= MaterialSlotCount) return;
        if (_slotResources[index] != null && ForgeManager.Instance != null)
            ForgeManager.Instance.RefundResource(_slotResources[index]);

        _slotResources[index] = null;
        AnimateMaterialSlotClear(index, 0f);
    }

    public void OnConfirmClicked()
    {
        TooltipSystem.Instance?.Hide();
        if (_isCommittingAffix) return;
        if (!CanMeditate()) return;

        var resources = new List<ForgeResourceSO>(_slotResources);
        ForgeAffixSO affix = ForgeManager.Instance.MeditateWithResources(_selectedDice, resources);
        if (affix == null) return;

        ClearSlots(shouldRefund: false);
        if (bagPanel != null) bagPanel.SetActive(false);
        RefreshBag();
        RefreshOptions();
        UpdateUI();
    }

    private void RefreshOptions()
    {
        ClearOptions();
        if (optionsContainer == null || optionButtonPrefab == null || _selectedDice == null) return;

        RenderCommittedAffixes();

        if (HasPendingOptions())
        {
            RenderPendingAffixes();
        }
    }

    private void RenderCommittedAffixes()
    {
        if (_selectedDice.forgeSlots == null) return;

        foreach (var slot in _selectedDice.forgeSlots)
        {
            if (slot == null || !slot.isForged || slot.affix == null) continue;

            DrawCommittedOptionLine(slot.tier - 1);

            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var optionBtn = btnObj.GetComponent<ForgeOptionButton>();
            if (optionBtn != null)
                optionBtn.Setup(slot.affix, showAttach: false);

            PositionOptionButton(btnObj, slot.tier - 1, committedOptionOffsets);
        }
    }

    private void RenderPendingAffixes()
    {
        int optionIndex = 0;
        foreach (var affix in ForgeManager.Instance.GetCurrentOptions())
        {
            if (affix == null) continue;
            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var optionBtn = btnObj.GetComponent<ForgeOptionButton>();
            if (optionBtn != null)
            {
                optionBtn.Setup(affix, showAttach: true);
                var captured = affix;
                if (optionBtn.attachButton != null)
                    optionBtn.attachButton.onClick.AddListener(() => OnAttachAffix(captured));
            }

            var rootButton = btnObj.GetComponent<Button>();
            if (rootButton != null)
            {
                var captured = affix;
                rootButton.onClick.AddListener(() => OnAttachAffix(captured));
            }

            PositionOptionButton(btnObj, optionIndex, optionOffsets);
            optionIndex++;
        }
    }

    private void ClearOptions()
    {
        if (optionsContainer == null) return;
        foreach (Transform child in optionsContainer)
        {
            child.DOKill();
            var rect = child as RectTransform;
            if (rect != null) rect.DOKill();
            var canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvasGroup != null) canvasGroup.DOKill();
            Destroy(child.gameObject);
        }
    }

    private void PositionOptionButton(GameObject optionObject, int index, List<Vector2> offsets)
    {
        if (!positionOptionsAroundSpellIcon || optionObject == null || optionsContainer == null) return;

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        if (optionRect == null) return;

        var layoutGroup = optionsContainer.GetComponent<LayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        if (!TryGetOptionPoints(index, offsets, out Vector2 center, out Vector2 targetPosition))
            return;

        optionRect.anchorMin = new Vector2(0.5f, 0.5f);
        optionRect.anchorMax = new Vector2(0.5f, 0.5f);
        optionRect.pivot = new Vector2(0.5f, 0.5f);
        optionRect.anchoredPosition = targetPosition;

        PlayOptionAppearAndIdle(optionRect, center, targetPosition, index);
    }

    private void DrawCommittedOptionLine(int index)
    {
        if (!showCommittedOptionLines || optionsContainer == null) return;
        if (!TryGetOptionPoints(index, committedOptionOffsets, out Vector2 center, out Vector2 targetPosition)) return;

        Vector2 delta = targetPosition - center;
        float length = delta.magnitude;
        if (length <= 0.01f) return;

        GameObject lineObject = new GameObject("CommittedAffixLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(optionsContainer, false);
        lineObject.transform.SetAsFirstSibling();

        Image lineImage = lineObject.GetComponent<Image>();
        lineImage.color = committedOptionLineColor;
        lineImage.raycastTarget = false;

        RectTransform lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = center + delta * 0.5f;
        lineRect.sizeDelta = new Vector2(length, committedOptionLineThickness);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private bool TryGetOptionPoints(int index, List<Vector2> offsets, out Vector2 center, out Vector2 targetPosition)
    {
        center = Vector2.zero;
        targetPosition = Vector2.zero;

        RectTransform parentRect = optionsContainer as RectTransform;
        if (parentRect == null) return false;

        center = GetOptionCenterInOptionsContainer(parentRect);
        targetPosition = center + GetOptionOffset(index, offsets);
        return true;
    }

    private void PlayOptionAppearAndIdle(RectTransform optionRect, Vector2 startPosition, Vector2 targetPosition, int index)
    {
        if (optionRect == null) return;

        optionRect.DOKill();
        CanvasGroup canvasGroup = optionRect.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = optionRect.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.DOKill();

        optionRect.anchoredPosition = startPosition;
        optionRect.localScale = Vector3.one * 0.8f;
        canvasGroup.alpha = 0f;

        Sequence appearSequence = DOTween.Sequence().SetTarget(optionRect);
        appearSequence.AppendInterval(index * 0.08f);
        appearSequence.Append(canvasGroup.DOFade(1f, optionAppearDuration * 0.45f));
        appearSequence.Join(optionRect.DOAnchorPos(targetPosition, optionAppearDuration).SetEase(Ease.OutCubic));
        appearSequence.Join(optionRect.DOScale(1f, optionAppearDuration).SetEase(Ease.OutBack));
        appearSequence.AppendCallback(() => StartOptionIdleFloat(optionRect, targetPosition, index));
    }

    private void StartOptionIdleFloat(RectTransform optionRect, Vector2 basePosition, int index)
    {
        if (optionRect == null || optionFloatDistance <= 0f || optionFloatDuration <= 0f) return;

        optionRect.DOKill();
        optionRect.anchoredPosition = basePosition;
        optionRect.DOAnchorPosY(basePosition.y + optionFloatDistance, optionFloatDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(index * 0.12f);
    }

    private Vector2 GetOptionCenterInOptionsContainer(RectTransform parentRect)
    {
        RectTransform centerRect = optionPlacementCenter;
        if (centerRect == null && spellIconImage != null)
            centerRect = spellIconImage.rectTransform;
        if (centerRect == null && currentDiceIcon != null)
            centerRect = currentDiceIcon.rectTransform;
        if (centerRect == null) return Vector2.zero;

        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, centerRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, camera, out Vector2 localPoint);
        return localPoint;
    }

    private Vector2 GetOptionOffset(int index, List<Vector2> offsets)
    {
        if (offsets != null && offsets.Count > 0)
            return offsets[Mathf.Clamp(index, 0, offsets.Count - 1)];

        return Vector2.zero;
    }

    private void OnAttachAffix(ForgeAffixSO affix)
    {
        TooltipSystem.Instance?.Hide();
        if (_isCommittingAffix) return;
        _isCommittingAffix = true;
        PlayerDice committedDice = _selectedDice;
        GameObject selectedOption = FindOptionObject(affix);

        PlayAffixCommitAnimation(selectedOption, () =>
        {
            RefundSlotResources();
            ForgeManager.Instance.CommitAffix(affix);
            RefreshDiceList();

            SelectDice(committedDice);
            RefreshBag();
            _isCommittingAffix = false;
            UpdateUI();
        });
    }

    private void UpdateUI()
    {
        bool pendingOptions = HasPendingOptions();
        int generatedCount = pendingOptions ? ForgeManager.Instance.CurrentSession.ForgeCount : 0;
        bool canMeditate = !_isCommittingAffix && CanMeditate();

        if (confirmButton != null)
            confirmButton.interactable = canMeditate;

        if (confirmButtonLabel != null)
            confirmButtonLabel.text = pendingOptions && generatedCount >= 3 ? "已达上限" : "冥想";

        if (previousDiceButton != null)
            previousDiceButton.interactable = !_isCommittingAffix && !pendingOptions && _availableDice.Count > 1;
        if (nextDiceButton != null)
            nextDiceButton.interactable = !_isCommittingAffix && !pendingOptions && _availableDice.Count > 1;

        if (stepText != null)
        {
            if (_selectedDice == null)
                stepText.text = "没有可锻造的骰子";
            else
                stepText.text = $"第 {GetForgedCount(_selectedDice) + 1} 个词条槽位 (T{GetForgedCount(_selectedDice) + 1})";
        }

        if (statusText != null)
        {
            if (_selectedDice == null)
                statusText.text = "没有可锻造的骰子";
            else if (pendingOptions)
                statusText.text = generatedCount >= 3 ? "请选择一个启迪刻印到骰子上" : $"已启迪 {generatedCount}/3 次，可继续放入 3 个材料再次冥想";
            else if (!AllSlotsFilled())
                statusText.text = "放满 3 个材料后可以冥想";
            else
                statusText.text = $"将为 {_selectedDice.diceName} 进行冥想";
        }
    }

    private bool CanMeditate()
    {
        if (_selectedDice == null || ForgeManager.Instance == null) return false;
        if (!AllSlotsFilled()) return false;
        if (HasPendingOptions() && !ForgeManager.Instance.CanForgeMore) return false;
        return true;
    }

    private bool AllSlotsFilled()
    {
        for (int i = 0; i < MaterialSlotCount; i++)
            if (_slotResources[i] == null) return false;
        return true;
    }

    private bool HasPendingOptions()
    {
        return ForgeManager.Instance != null
            && ForgeManager.Instance.CurrentSession != null
            && ForgeManager.Instance.CurrentSession.targetDice == _selectedDice
            && ForgeManager.Instance.CurrentSession.generatedOptions.Count > 0;
    }

    private int GetForgedCount(PlayerDice dice)
    {
        if (dice?.forgeSlots == null) return 0;

        int count = 0;
        foreach (var slot in dice.forgeSlots)
            if (slot != null && slot.isForged) count++;
        return count;
    }

    private void RefreshSlot(int index)
    {
        if (index < 0 || index >= _materialSlotImages.Count) return;

        Image image = _materialSlotImages[index];
        ForgeResourceSO resource = _slotResources[index];

        if (resource != null && resource.icon != null)
        {
            image.sprite = resource.icon;
            image.color = Color.white;
        }
        else
        {
            image.sprite = _materialSlotDefaultSprites[index];
            image.color = _materialSlotDefaultColors[index];
        }
    }

    private void ClearSlots(bool shouldRefund)
    {
        for (int i = 0; i < MaterialSlotCount; i++)
        {
            if (shouldRefund && _slotResources[i] != null && ForgeManager.Instance != null)
                ForgeManager.Instance.RefundResource(_slotResources[i]);

            _slotResources[i] = null;
            RefreshSlot(i);
        }
        _editingSlotIndex = -1;
    }

    private void RefundSlotResources()
    {
        ClearSlots(shouldRefund: true);
    }

    private void ClearSlotsForDiceSwitch()
    {
        if (_slotResources == null) return;

        for (int i = 0; i < MaterialSlotCount; i++)
        {
            if (_slotResources[i] != null && ForgeManager.Instance != null)
                ForgeManager.Instance.RefundResource(_slotResources[i]);

            bool hadResource = _slotResources[i] != null;
            _slotResources[i] = null;

            if (hadResource)
                AnimateMaterialSlotClear(i, i * materialClearInterval);
            else
                RefreshSlot(i);
        }

        _editingSlotIndex = -1;
        CloseBagPanel();
    }

    private void AnimateMaterialSlotClear(int index, float delay)
    {
        if (index < 0 || index >= _materialSlotImages.Count) return;

        Image image = _materialSlotImages[index];
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();
        rect.localScale = Vector3.one;
        rect.DOScale(0.78f, 0.14f)
            .SetDelay(delay)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                rect.localScale = Vector3.one;
                RefreshSlot(index);
            });
    }

    private void PlayMaterialSlotChangedAnimation(int index, bool isReplace)
    {
        if (index < 0 || index >= _materialSlotImages.Count) return;

        Image image = _materialSlotImages[index];
        if (image == null) return;

        RectTransform rect = image.rectTransform;
        rect.DOKill();

        if (isReplace)
        {
            Sequence sequence = DOTween.Sequence().SetTarget(rect);
            sequence.Append(rect.DOScale(0.72f, materialReplaceDuration * 0.35f).SetEase(Ease.InBack));
            sequence.Append(rect.DOScale(1.14f, materialReplaceDuration * 0.35f).SetEase(Ease.OutBack));
            sequence.Append(rect.DOScale(1f, materialReplaceDuration * 0.3f).SetEase(Ease.OutCubic));
        }
        else
        {
            rect.localScale = Vector3.one * 0.65f;
            rect.DOScale(1f, materialPopDuration).SetEase(Ease.OutBack);
        }
    }

    private GameObject FindOptionObject(ForgeAffixSO affix)
    {
        if (affix == null || optionsContainer == null) return null;

        foreach (Transform child in optionsContainer)
        {
            var optionButton = child.GetComponent<ForgeOptionButton>();
            if (optionButton != null && optionButton.Affix == affix)
                return child.gameObject;
        }

        return null;
    }

    private void PlayAffixCommitAnimation(GameObject optionObject, Action onComplete)
    {
        if (optionObject == null)
        {
            onComplete?.Invoke();
            return;
        }

        RectTransform rect = optionObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            onComplete?.Invoke();
            return;
        }

        rect.DOKill();
        CanvasGroup canvasGroup = optionObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = optionObject.AddComponent<CanvasGroup>();
        canvasGroup.DOKill();

        Vector3 startScale = rect.localScale;
        Sequence sequence = DOTween.Sequence().SetTarget(rect);
        sequence.Append(rect.DOScale(startScale * 0.88f, affixCommitDuration * 0.22f).SetEase(Ease.InQuad));
        sequence.Append(rect.DOScale(startScale * 1.18f, affixCommitDuration * 0.3f).SetEase(Ease.OutBack));
        sequence.Join(canvasGroup.DOFade(0.75f, affixCommitDuration * 0.15f).SetLoops(2, LoopType.Yoyo));
        sequence.Append(rect.DOScale(startScale, affixCommitDuration * 0.28f).SetEase(Ease.OutCubic));
        sequence.OnComplete(() => onComplete?.Invoke());
    }

    private void CacheMaterialSlots()
    {
        _materialSlotImages.Clear();
        _materialSlotDefaultSprites.Clear();
        _materialSlotDefaultColors.Clear();

        if (materialSlotButtons == null) return;

        for (int i = 0; i < materialSlotButtons.Count && _materialSlotImages.Count < MaterialSlotCount; i++)
        {
            Button button = materialSlotButtons[i];
            if (button == null) continue;

            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null) continue;

            int index = _materialSlotImages.Count;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnMaterialSlotClicked(index));

            _materialSlotImages.Add(image);
            _materialSlotDefaultSprites.Add(image.sprite);
            _materialSlotDefaultColors.Add(image.color);
        }
    }
}
