using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 冥想界面的材料输入模块。
/// 负责材料槽、背包列表、连续填充、替换、退还和材料槽动画。
/// </summary>
public class ForgeMaterialInputPanel : MonoBehaviour
{
    private const int MaterialSlotCount = 3;
    private const int BagPageSize = 9;

    [Header("Panel")]
    public GameObject materialSlotBar;
    public Button materialSlotBarToggleButton;
    public GameObject bagPanel;
    public Button bagCloseButton;

    [Header("Slots")]
    public List<Button> materialSlotButtons = new List<Button>();

    [Header("Bag")]
    public Transform bagItemContainer;
    public GameObject resourceButtonPrefab;
    public Button bagPreviousButton;
    public Button bagNextButton;

    [SerializeField, HideInInspector] private float materialClearInterval = 0.08f;
    [SerializeField, HideInInspector] private float materialPopDuration = 0.18f;
    [SerializeField, HideInInspector] private float materialReplaceDuration = 0.24f;

    private readonly List<Image> _slotImages = new List<Image>();
    private readonly List<Sprite> _slotDefaultSprites = new List<Sprite>();
    private readonly List<Color> _slotDefaultColors = new List<Color>();
    private readonly ForgeResourceSO[] _slotResources = new ForgeResourceSO[MaterialSlotCount];

    private Func<bool> _canEdit;
    private Action _onChanged;
    private Action<bool> _onBagVisibilityChanged;
    private Action _onSlotBarOpened;
    private Action<ForgeResourceSO> _onResourceSelected;
    private int _editingSlotIndex = -1;
    private int _bagPageIndex;
    private bool _slotBarVisible;
    private bool _bagVisible;
    private Button _firstAvailableResourceButton;
    private ForgeResourceSO _firstAvailableResource;

    public bool IsSlotBarVisible => _slotBarVisible;
    public bool IsBagVisible => _bagVisible;

    public bool AllSlotsFilled
    {
        get
        {
            for (int i = 0; i < MaterialSlotCount; i++)
                if (_slotResources[i] == null) return false;
            return true;
        }
    }

    public void Initialize(
        Func<bool> canEdit,
        Action onChanged,
        Action<bool> onBagVisibilityChanged = null,
        Action onSlotBarOpened = null,
        Action<ForgeResourceSO> onResourceSelected = null)
    {
        _canEdit = canEdit;
        _onChanged = onChanged;
        _onBagVisibilityChanged = onBagVisibilityChanged;
        _onSlotBarOpened = onSlotBarOpened;
        _onResourceSelected = onResourceSelected;
        BindButtons();
        CacheSlots();
    }

    public void ResetPanel()
    {
        RefundAllSlots();
        ClearSlots(shouldRefund: false);
        SetSlotBarVisible(false, immediate: true);
        HidePopup(bagPanel, immediate: true);
        _onBagVisibilityChanged?.Invoke(false);
        RefreshBag();
    }

    public List<ForgeResourceSO> GetSelectedResources()
    {
        return new List<ForgeResourceSO>(_slotResources);
    }

    public void SetToggleInteractable(bool interactable)
    {
        if (materialSlotBarToggleButton != null)
            materialSlotBarToggleButton.interactable = interactable;
    }

    public void RefreshBag()
    {
        _firstAvailableResourceButton = null;
        _firstAvailableResource = null;
        if (bagItemContainer == null || resourceButtonPrefab == null || ForgeManager.Instance == null) return;

        foreach (Transform child in bagItemContainer) Destroy(child.gameObject);

        List<ForgeResourceSO> availableResources = GetAvailableResources();
        int pageCount = GetBagPageCount(availableResources.Count);
        _bagPageIndex = Mathf.Clamp(_bagPageIndex, 0, Mathf.Max(0, pageCount - 1));

        int startIndex = _bagPageIndex * BagPageSize;
        for (int slotIndex = 0; slotIndex < BagPageSize; slotIndex++)
        {
            int resourceIndex = startIndex + slotIndex;
            ForgeResourceSO resource = resourceIndex < availableResources.Count ? availableResources[resourceIndex] : null;
            CreateBagSlot(resource);
        }

        RefreshBagPagingButtons(pageCount);
    }

    private List<ForgeResourceSO> GetAvailableResources()
    {
        List<ForgeResourceSO> resources = new List<ForgeResourceSO>();
        if (ForgeManager.Instance == null) return resources;

        foreach (ForgeResourceSO resource in ForgeManager.Instance.allResources)
        {
            if (resource == null) continue;

            int count = ForgeManager.Instance.GetResourceCount(resource);
            if (count <= 0) continue;

            resources.Add(resource);
        }

        return resources;
    }

    private void CreateBagSlot(ForgeResourceSO resource)
    {
        GameObject buttonObject = Instantiate(resourceButtonPrefab, bagItemContainer);
        ForgeResourceButton resourceButton = buttonObject.GetComponent<ForgeResourceButton>();
        Button button = buttonObject.GetComponent<Button>();

        if (resource == null || ForgeManager.Instance == null)
        {
            resourceButton?.SetupEmpty();
            if (button != null) button.interactable = false;
            return;
        }

        int count = ForgeManager.Instance.GetResourceCount(resource);
        if (resourceButton != null)
            resourceButton.Setup(resource, count);

        if (button != null)
        {
            ForgeResourceSO capturedResource = resource;
            button.interactable = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnBagResourceClicked(capturedResource));

            if (_firstAvailableResourceButton == null)
            {
                _firstAvailableResourceButton = button;
                _firstAvailableResource = resource;
            }
        }
    }

    public bool TryGetFirstMaterialSlotGuideTarget(out Button button)
    {
        button = null;
        if (!_slotBarVisible || materialSlotButtons == null) return false;

        for (int i = 0; i < materialSlotButtons.Count; i++)
        {
            Button candidate = materialSlotButtons[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.interactable)
                continue;

            button = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetFirstAvailableResourceGuideTarget(
        out Button button,
        out ForgeResourceSO resource)
    {
        button = _firstAvailableResourceButton;
        resource = _firstAvailableResource;
        return _bagVisible
            && button != null
            && resource != null
            && button.gameObject.activeInHierarchy
            && button.interactable;
    }

    private int GetBagPageCount(int resourceCount)
    {
        return Mathf.Max(1, Mathf.CeilToInt(resourceCount / (float)BagPageSize));
    }

    private void RefreshBagPagingButtons(int pageCount)
    {
        bool hasMultiplePages = pageCount > 1;

        if (bagPreviousButton != null)
        {
            bagPreviousButton.gameObject.SetActive(hasMultiplePages);
            bagPreviousButton.interactable = hasMultiplePages && _bagPageIndex > 0;
        }

        if (bagNextButton != null)
        {
            bagNextButton.gameObject.SetActive(hasMultiplePages);
            bagNextButton.interactable = hasMultiplePages && _bagPageIndex < pageCount - 1;
        }
    }

    public void CloseBagPanel()
    {
        TooltipSystem.Instance?.Hide();
        _editingSlotIndex = -1;
        _bagVisible = false;
        HidePopup(bagPanel);
        _onBagVisibilityChanged?.Invoke(false);
    }

    public void RefundAllSlots()
    {
        ClearSlots(shouldRefund: true);
    }

    public void ClearAfterMeditation()
    {
        ClearSlots(shouldRefund: false);
    }

    public void ClearForDiceSwitch()
    {
        for (int i = 0; i < MaterialSlotCount; i++)
        {
            if (_slotResources[i] != null && ForgeManager.Instance != null)
                ForgeManager.Instance.RefundResource(_slotResources[i]);

            bool hadResource = _slotResources[i] != null;
            _slotResources[i] = null;

            if (hadResource)
                AnimateSlotClear(i, i * materialClearInterval);
            else
                RefreshSlot(i);
        }

        _editingSlotIndex = -1;
        CloseBagPanel();
    }

    private void BindButtons()
    {
        if (materialSlotBarToggleButton != null)
        {
            materialSlotBarToggleButton.onClick.RemoveListener(OnSlotBarToggleClicked);
            materialSlotBarToggleButton.onClick.AddListener(OnSlotBarToggleClicked);
        }

        if (bagCloseButton != null)
        {
            bagCloseButton.onClick.RemoveListener(CloseBagPanel);
            bagCloseButton.onClick.AddListener(CloseBagPanel);
        }

        if (bagPreviousButton != null)
        {
            bagPreviousButton.onClick.RemoveListener(OnPreviousBagPageClicked);
            bagPreviousButton.onClick.AddListener(OnPreviousBagPageClicked);
        }

        if (bagNextButton != null)
        {
            bagNextButton.onClick.RemoveListener(OnNextBagPageClicked);
            bagNextButton.onClick.AddListener(OnNextBagPageClicked);
        }
    }

    private void CacheSlots()
    {
        _slotImages.Clear();
        _slotDefaultSprites.Clear();
        _slotDefaultColors.Clear();

        if (materialSlotButtons == null) return;

        for (int i = 0; i < materialSlotButtons.Count && _slotImages.Count < MaterialSlotCount; i++)
        {
            Button button = materialSlotButtons[i];
            if (button == null) continue;

            Image image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            if (image == null) continue;

            int index = _slotImages.Count;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnMaterialSlotClicked(index));

            _slotImages.Add(image);
            _slotDefaultSprites.Add(image.sprite);
            _slotDefaultColors.Add(image.color);
        }
    }

    private void OnSlotBarToggleClicked()
    {
        if (!CanEdit()) return;
        SetSlotBarVisible(!_slotBarVisible);
    }

    private void SetSlotBarVisible(bool visible, bool immediate = false)
    {
        bool wasVisible = _slotBarVisible;
        _slotBarVisible = visible;

        if (visible)
            ShowPopup(materialSlotBar, immediate);
        else
            HidePopup(materialSlotBar, immediate);

        if (!visible)
        {
            TooltipSystem.Instance?.Hide();
            _editingSlotIndex = -1;
            _bagVisible = false;
            HidePopup(bagPanel, immediate);
            _onBagVisibilityChanged?.Invoke(false);
        }
        else if (!wasVisible)
        {
            _onSlotBarOpened?.Invoke();
        }
    }

    private void OnMaterialSlotClicked(int index)
    {
        if (index < 0 || index >= MaterialSlotCount) return;
        if (!CanEdit()) return;

        if (_slotResources[index] != null && _editingSlotIndex == index && bagPanel != null && bagPanel.activeSelf)
        {
            RefundSlot(index);
            _editingSlotIndex = FindFirstEmptySlot();
            if (_editingSlotIndex < 0) CloseBagPanel();
            else RefreshBag();
            _onChanged?.Invoke();
            return;
        }

        OpenBagForSlot(index);
    }

    private void OpenBagForSlot(int index)
    {
        _editingSlotIndex = index;
        _bagPageIndex = 0;
        _bagVisible = true;
        ShowPopup(bagPanel);
        RefreshBag();
        _onBagVisibilityChanged?.Invoke(true);
    }

    private void OnPreviousBagPageClicked()
    {
        if (_bagPageIndex <= 0) return;
        _bagPageIndex--;
        RefreshBag();
    }

    private void OnNextBagPageClicked()
    {
        int pageCount = GetBagPageCount(GetAvailableResources().Count);
        if (_bagPageIndex >= pageCount - 1) return;
        _bagPageIndex++;
        RefreshBag();
    }

    private void OnBagResourceClicked(ForgeResourceSO resource)
    {
        if (_editingSlotIndex < 0 || _editingSlotIndex >= MaterialSlotCount) return;
        if (resource == null || ForgeManager.Instance == null) return;
        if (!CanEdit()) return;
        if (ForgeManager.Instance.GetResourceCount(resource) <= 0) return;

        ForgeResourceSO previousResource = _slotResources[_editingSlotIndex];
        if (previousResource != null)
            ForgeManager.Instance.RefundResource(previousResource);

        if (!ForgeManager.Instance.TryConsumeResource(resource))
        {
            if (previousResource != null)
                ForgeManager.Instance.TryConsumeResource(previousResource);
            return;
        }

        _slotResources[_editingSlotIndex] = resource;
        RefreshSlot(_editingSlotIndex);
        PlaySlotChangedAnimation(_editingSlotIndex, previousResource != null);
        _onResourceSelected?.Invoke(resource);

        AdvanceMaterialSelection();
        RefreshBag();
        _onChanged?.Invoke();
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
        _bagVisible = false;
        HidePopup(bagPanel);
        _onBagVisibilityChanged?.Invoke(false);
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
        AnimateSlotClear(index, 0f);
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

    private void RefreshSlot(int index)
    {
        if (index < 0 || index >= _slotImages.Count) return;

        Image image = _slotImages[index];
        ForgeResourceSO resource = _slotResources[index];

        if (resource != null && resource.icon != null)
        {
            image.sprite = resource.icon;
            image.color = Color.white;
        }
        else
        {
            image.sprite = _slotDefaultSprites[index];
            image.color = _slotDefaultColors[index];
        }
    }

    private void AnimateSlotClear(int index, float delay)
    {
        if (index < 0 || index >= _slotImages.Count) return;
        ForgeUIEffects.AnimateMaterialSlotClear(_slotImages[index], delay, () => RefreshSlot(index));
    }

    private void PlaySlotChangedAnimation(int index, bool isReplace)
    {
        if (index < 0 || index >= _slotImages.Count) return;
        ForgeUIEffects.PlayMaterialSlotChanged(_slotImages[index], isReplace, materialPopDuration, materialReplaceDuration);
    }

    private bool CanEdit()
    {
        return _canEdit == null || _canEdit();
    }

    private static void ShowPopup(GameObject popup, bool immediate = false)
    {
        if (popup == null) return;

        PopupAnimatorUI animator = popup.GetComponent<PopupAnimatorUI>();
        if (animator != null)
        {
            if (immediate) animator.ShowImmediate();
            else animator.Show();
            return;
        }

        popup.SetActive(true);
    }

    private static void HidePopup(GameObject popup, bool immediate = false)
    {
        if (popup == null) return;

        PopupAnimatorUI animator = popup.GetComponent<PopupAnimatorUI>();
        if (animator != null)
        {
            if (immediate) animator.HideImmediate();
            else animator.Hide();
            return;
        }

        popup.SetActive(false);
    }
}
