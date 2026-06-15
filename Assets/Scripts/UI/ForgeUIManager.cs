using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 冥想/锻造面板的主 UI 控制器。
/// 负责骰子选择、材料放置、背包刷新、启迪节点显示、长按刻印、星座连线绑定和面板状态刷新。
/// </summary>
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
        new Vector2(-260f, -40f),
        new Vector2(0f, 190f),
        new Vector2(260f, -40f)
    };
    public bool showCommittedOptionLines = true;

    [Header("Option Layout")]
    public float generatedOptionRadiusStep = 42f;
    public float optionCollisionRadius = 96f;
    public int optionCollisionIterations = 8;
    public float optionCollisionPushStrength = 0.65f;
    public float optionLayoutMaxRadius = 380f;

    [Header("Constellation Line")]
    [FormerlySerializedAs("committedOptionLineColor")]
    [FormerlySerializedAs("holdLineColor")]
    public Color constellationLineColor = new Color(1f, 1f, 1f, 1f);
    [FormerlySerializedAs("constellationWorldLineWidth")]
    public float constellationLineWidth = 0.014f;
    public float constellationBendOffset = 36f;
    public float constellationBendExaggeration = 1.8f;
    [Range(0.2f, 0.8f)] public float constellationBendMinT = 0.35f;
    [Range(0.2f, 0.8f)] public float constellationBendMaxT = 0.65f;
    public float constellationParticleSize = 0.018f;
    public float constellationParticleSpread = 3f;
    public float constellationIdleBendAmplitude = 4f;
    public float constellationIdleBendSpeed = 2.2f;
    public float constellationHdrIntensity = 2.2f;
    public Sprite constellationNodeSprite;
    [FormerlySerializedAs("constellationWorldNodeSize")]
    public float constellationNodeSize = 18f;

    [Header("Constellation Rendering")]
    public bool useWorldConstellationEffect = true;
    public Camera constellationEffectCamera;
    public Transform constellationEffectRoot;
    public float constellationEffectDepth = 2.85f;

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

    [Header("Hold Commit")]
    public float holdDuration = 3f;
    public float holdShakeIntensity = 6f;
    public float holdShakeFrequency = 0.03f;

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
    private readonly List<ForgeConstellationEffect> _activeWorldConstellationEffects = new List<ForgeConstellationEffect>();
    private readonly List<WorldConstellationBinding> _worldConstellationBindings = new List<WorldConstellationBinding>();
    private readonly Dictionary<ForgeInspiration, Vector2> _resolvedInspirationPositions = new Dictionary<ForgeInspiration, Vector2>();
    private readonly Dictionary<ForgeInspiration, Vector2> _previousInspirationPositions = new Dictionary<ForgeInspiration, Vector2>();
    private bool _isCommittingAffix;
    private ForgeInspiration _lastCreatedInspirationForAppear;

    // Hold commit state
    private bool _holdActive;
    private float _holdElapsed;
    private ForgeAffixSO _holdAffix;
    private RectTransform _holdOptionRect;
    private ForgeConstellationLine _holdLine;
    private ForgeConstellationEffect _holdWorldLine;
    private Vector2 _holdLineBasePos;
    private RectTransform _holdCenterIconRect;
    private Vector2 _holdCenterIconBasePos;
    private Vector3 _holdCenterIconBaseScale = Vector3.one;
    private Vector2 _holdOptionBasePos;
    private Vector3 _holdOptionBaseScale = Vector3.one;
    private Coroutine _holdCoroutine;

    /// <summary>
    /// 初始化 UI 管理器单例引用。
    /// </summary>
    void Awake() { Instance = this; }

    /// <summary>
    /// 绑定面板中的静态按钮事件。
    /// </summary>
    void Start()
    {
        BindStaticButtons();
    }

    /// <summary>
    /// 每帧刷新世界空间星座连线的实时参数和跟随位置。
    /// </summary>
    void Update()
    {
        RefreshLiveConstellationSettings();
    }

    /// <summary>
    /// 打开冥想面板并刷新骰子、材料、启迪和预览状态。
    /// </summary>
    /// <param name="onComplete">面板关闭后继续游戏流程的回调。</param>
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

    /// <summary>
    /// 关闭冥想面板，并清理呼吸动画、长按连线和星座特效。
    /// </summary>
    public void Hide()
    {
        StopPreviewBreath();
        DestroyHoldLines();
        ClearWorldConstellationEffects();
        if (panelRoot != null) panelRoot.SetActive(false);
        TooltipSystem.Instance?.Hide();
    }

    /// <summary>
    /// 为关闭、切换骰子、冥想确认和背包关闭按钮绑定运行时监听。
    /// </summary>
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

    /// <summary>
    /// 检查按钮是否已经在 Inspector 中配置了指定持久监听。
    /// </summary>
    /// <param name="button">要检查的按钮。</param>
    /// <param name="methodName">目标监听方法名。</param>
    /// <returns>已存在对应持久监听时返回 true。</returns>
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

    /// <summary>
    /// 处理面板关闭按钮，返还未使用材料并继续外部流程。
    /// </summary>
    private void OnCloseClicked()
    {
        CancelHold();
        TooltipSystem.Instance?.Hide();
        RefundSlotResources();
        ClearSlots(shouldRefund: false);
        Hide();
        _onComplete?.Invoke();
    }

    /// <summary>
    /// 关闭材料背包弹窗并清除当前编辑槽位。
    /// </summary>
    public void CloseBagPanel()
    {
        TooltipSystem.Instance?.Hide();
        _editingSlotIndex = -1;
        if (bagPanel != null) bagPanel.SetActive(false);
    }

    /// <summary>
    /// 刷新当前可锻造骰子列表，并在非轮播模式下重建骰子选择按钮。
    /// </summary>
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

    /// <summary>
    /// 处理列表模式下的骰子选择点击。
    /// </summary>
    /// <param name="dice">被点击的玩家骰子。</param>
    private void OnDiceClicked(PlayerDice dice)
    {
        if (HasPendingOptions() || _holdActive) return;
        if (_isCommittingAffix) return;

        int index = _availableDice.IndexOf(dice);
        if (index >= 0) SelectDiceByIndex(index);
    }

    /// <summary>
    /// 切换到上一颗可锻造骰子。
    /// </summary>
    private void OnPreviousDiceClicked()
    {
        if (HasPendingOptions() || _holdActive || _availableDice.Count == 0) return;
        if (_isCommittingAffix) return;
        SelectDiceByIndex((_selectedDiceIndex - 1 + _availableDice.Count) % _availableDice.Count);
    }

    /// <summary>
    /// 切换到下一颗可锻造骰子。
    /// </summary>
    private void OnNextDiceClicked()
    {
        if (HasPendingOptions() || _holdActive || _availableDice.Count == 0) return;
        if (_isCommittingAffix) return;
        SelectDiceByIndex((_selectedDiceIndex + 1) % _availableDice.Count);
    }

    /// <summary>
    /// 根据可锻造骰子列表索引选择当前骰子，并刷新相关 UI。
    /// </summary>
    /// <param name="index">目标骰子在可锻造列表中的索引。</param>
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

    /// <summary>
    /// 重新选择指定骰子；如果它已不在可锻造列表中，则回退到当前附近索引。
    /// </summary>
    /// <param name="dice">希望选中的玩家骰子。</param>
    private void SelectDice(PlayerDice dice)
    {
        int index = _availableDice.IndexOf(dice);
        if (index >= 0)
            SelectDiceByIndex(index);
        else
            SelectDiceByIndex(Mathf.Min(_selectedDiceIndex, Mathf.Max(0, _availableDice.Count - 1)));
    }

    /// <summary>
    /// 刷新中心法术图标和下方当前骰子图标。
    /// </summary>
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

    /// <summary>
    /// 设置单个预览 Image 的 sprite 和可见性。
    /// </summary>
    /// <param name="image">需要刷新的图片组件。</param>
    /// <param name="sprite">要显示的图标；为空时隐藏图片。</param>
    private void SetPreviewIcon(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    /// <summary>
    /// 启动中心法术图标的呼吸待机动画。
    /// </summary>
    private void StartPreviewBreath()
    {
        ForgeUIEffects.StartBreath(spellIconImage, iconBreathScale, iconBreathDuration);
    }

    /// <summary>
    /// 停止法术和骰子预览图标上的动画。
    /// </summary>
    private void StopPreviewBreath()
    {
        ForgeUIEffects.StopIconTween(spellIconImage);
        ForgeUIEffects.StopIconTween(currentDiceIcon);
    }

    /// <summary>
    /// 播放切换骰子时的图标切换反馈。
    /// </summary>
    private void PlayDiceSwitchAnimation()
    {
        ForgeUIEffects.PlayIconSwitch(spellIconImage, diceSwitchDuration, StartPreviewBreath);
        ForgeUIEffects.PlayIconSwitch(currentDiceIcon, diceSwitchDuration);
    }

    /// <summary>
    /// 为预览图标配置骰子 tooltip 目标。
    /// </summary>
    /// <param name="image">需要挂载或刷新 tooltip 的图标。</param>
    private void SetupTooltipTarget(Image image)
    {
        if (image == null) return;

        ForgeDiceTooltipTarget tooltipTarget = image.GetComponent<ForgeDiceTooltipTarget>();
        if (tooltipTarget != null)
            tooltipTarget.Setup(_selectedDice);
    }

    /// <summary>
    /// 处理材料槽点击。
    /// 空槽会进入连续填充模式，已填充槽再次点击会退还材料。
    /// </summary>
    /// <param name="index">被点击的材料槽索引。</param>
    private void OnMaterialSlotClicked(int index)
    {
        if (index < 0 || index >= MaterialSlotCount) return;
        if (_isCommittingAffix || _holdActive) return;
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

    /// <summary>
    /// 打开材料背包，并指定当前要填入或替换的材料槽。
    /// </summary>
    /// <param name="index">当前编辑的材料槽索引。</param>
    private void OpenBagForSlot(int index)
    {
        _editingSlotIndex = index;
        if (bagPanel != null) bagPanel.SetActive(true);
        RefreshBag();
    }

    /// <summary>
    /// 根据当前库存重建材料背包列表。
    /// 只显示背包内数量大于 0 的材料。
    /// </summary>
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

    /// <summary>
    /// 处理背包材料点击，将材料放入当前槽位并从库存扣除。
    /// </summary>
    /// <param name="resource">玩家点击的材料配置。</param>
    private void OnBagResourceClicked(ForgeResourceSO resource)
    {
        if (_editingSlotIndex < 0 || _editingSlotIndex >= MaterialSlotCount) return;
        if (resource == null || ForgeManager.Instance == null) return;
        if (_isCommittingAffix || _holdActive) return;
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

    /// <summary>
    /// 连续填充模式下推进到下一个空材料槽；没有空槽时关闭背包。
    /// </summary>
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

    /// <summary>
    /// 查找第一个还没有放入材料的槽位。
    /// </summary>
    /// <returns>第一个空槽索引；没有空槽时返回 -1。</returns>
    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < MaterialSlotCount; i++)
            if (_slotResources[i] == null) return i;
        return -1;
    }

    /// <summary>
    /// 退还指定材料槽中的材料，并清空槽位显示。
    /// </summary>
    /// <param name="index">需要退还的材料槽索引。</param>
    private void RefundSlot(int index)
    {
        if (index < 0 || index >= MaterialSlotCount) return;
        if (_slotResources[index] != null && ForgeManager.Instance != null)
            ForgeManager.Instance.RefundResource(_slotResources[index]);

        _slotResources[index] = null;
        AnimateMaterialSlotClear(index, 0f);
    }

    /// <summary>
    /// 处理冥想按钮点击。
    /// 三个材料槽放满后，生成启迪并分配一个未占用的位置。
    /// </summary>
    public void OnConfirmClicked()
    {
        TooltipSystem.Instance?.Hide();
        if (_isCommittingAffix || _holdActive) return;
        if (!CanMeditate()) return;

        var resources = new List<ForgeResourceSO>(_slotResources);
        int optionIndex = FindFreeInspirationOptionIndex(_selectedDice);
        ForgeInspiration inspiration = ForgeManager.Instance.MeditateWithResources(_selectedDice, resources, optionIndex);
        if (inspiration == null) return;

        _lastCreatedInspirationForAppear = inspiration;
        ClearSlots(shouldRefund: false);
        if (bagPanel != null) bagPanel.SetActive(false);
        RefreshBag();
        RefreshOptions();
        UpdateUI();
    }

    /// <summary>
    /// 重建启迪节点和已刻印连接线。
    /// </summary>
    private void RefreshOptions()
    {
        CaptureCurrentInspirationPositions();
        ClearOptions();
        try
        {
            if (optionsContainer == null || optionButtonPrefab == null || _selectedDice == null) return;

            RebuildInspirationLayout();
            RenderInspirationNodes();
            RenderLegacyCommittedAffixes();
        }
        finally
        {
            _lastCreatedInspirationForAppear = null;
            _previousInspirationPositions.Clear();
        }
    }

    /// <summary>
    /// 渲染当前骰子记录中的所有启迪节点。
    /// 当前会话启迪可长按刻印，历史未选启迪会变暗保留。
    /// </summary>
    private void RenderInspirationNodes()
    {
        if (_selectedDice.forgeInspirations == null) return;

        foreach (var inspiration in _selectedDice.forgeInspirations)
        {
            if (inspiration == null || inspiration.affix == null) continue;

            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var optionBtn = btnObj.GetComponent<ForgeOptionButton>();
            if (optionBtn != null)
            {
                bool isCurrentPending = IsCurrentSessionInspiration(inspiration);
                optionBtn.Setup(inspiration, showAttach: isCurrentPending);
                optionBtn.SetDimmed(!inspiration.isCommitted && !isCurrentPending);
                optionBtn.SetCommitInteractable(isCurrentPending);
            }

            bool playAppearAnimation = inspiration == _lastCreatedInspirationForAppear;
            Vector2? resolvedPosition = TryGetResolvedInspirationPosition(inspiration, out Vector2 position)
                ? position
                : null;
            Vector2? shiftStartPosition = !playAppearAnimation
                && _previousInspirationPositions.TryGetValue(inspiration, out Vector2 previousPosition)
                && resolvedPosition.HasValue
                && (previousPosition - resolvedPosition.Value).sqrMagnitude > 1f
                    ? previousPosition
                    : null;

            PositionOptionButton(btnObj, inspiration.optionIndex, optionOffsets, playAppearAnimation, resolvedPosition, shiftStartPosition);
            if (inspiration.isCommitted)
                DrawCommittedOptionLine(inspiration.optionIndex, inspiration.affix, btnObj.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// 渲染没有持久启迪记录的旧刻印数据。
    /// 用于兼容旧存档或旧运行数据。
    /// </summary>
    private void RenderLegacyCommittedAffixes()
    {
        if (_selectedDice.forgeSlots == null) return;

        for (int i = 0; i < _selectedDice.forgeSlots.Count; i++)
        {
            var slot = _selectedDice.forgeSlots[i];
            if (slot == null || !slot.isForged || slot.affix == null) continue;
            if (HasCommittedInspirationForSlot(i)) continue;

            GameObject btnObj = Instantiate(optionButtonPrefab, optionsContainer);
            var optionBtn = btnObj.GetComponent<ForgeOptionButton>();
            if (optionBtn != null) optionBtn.Setup(slot.affix, showAttach: false);

            int optionIndex = GetCommittedOptionIndex(slot);
            PositionOptionButton(btnObj, optionIndex, optionOffsets, playAppearAnimation: false);
            DrawCommittedOptionLine(optionIndex, slot.affix, btnObj.GetComponent<RectTransform>());
        }
    }

    /// <summary>
    /// 清空启迪按钮和已创建的星座连线效果。
    /// </summary>
    private void ClearOptions()
    {
        ClearWorldConstellationEffects();

        if (optionsContainer == null) return;
        foreach (Transform child in optionsContainer)
        {
            ForgeUIEffects.StopOptionTweens(child);
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// 在刷新前记录当前启迪按钮位置，用于刷新后播放被挤开的位移动画。
    /// </summary>
    private void CaptureCurrentInspirationPositions()
    {
        _previousInspirationPositions.Clear();
        if (optionsContainer == null) return;

        foreach (Transform child in optionsContainer)
        {
            var optionButton = child.GetComponent<ForgeOptionButton>();
            RectTransform rect = child as RectTransform;
            if (optionButton?.Inspiration == null || rect == null) continue;

            _previousInspirationPositions[optionButton.Inspiration] = rect.anchoredPosition;
        }
    }

    /// <summary>
    /// 根据启迪基础位置做一次简单碰撞松弛，得到最终显示位置。
    /// </summary>
    private void RebuildInspirationLayout()
    {
        _resolvedInspirationPositions.Clear();
        if (_selectedDice?.forgeInspirations == null || optionsContainer == null) return;

        RectTransform parentRect = optionsContainer as RectTransform;
        if (parentRect == null) return;

        Vector2 center = GetOptionCenterInOptionsContainer(parentRect);
        List<ForgeInspiration> inspirations = new List<ForgeInspiration>();
        List<Vector2> positions = new List<Vector2>();

        foreach (var inspiration in _selectedDice.forgeInspirations)
        {
            if (inspiration == null || inspiration.affix == null) continue;

            inspirations.Add(inspiration);
            positions.Add(center + GetOptionOffset(inspiration.optionIndex, optionOffsets));
        }

        ResolveInspirationCollisions(positions, center);

        for (int i = 0; i < inspirations.Count; i++)
            _resolvedInspirationPositions[inspirations[i]] = positions[i];
    }

    /// <summary>
    /// 对启迪位置做成对碰撞推开，让重叠节点产生互相挤开的布局效果。
    /// </summary>
    /// <param name="positions">待调整的启迪位置列表。</param>
    /// <param name="center">法术中心局部坐标。</param>
    private void ResolveInspirationCollisions(List<Vector2> positions, Vector2 center)
    {
        if (positions == null || positions.Count <= 1) return;

        float minDistance = Mathf.Max(12f, optionCollisionRadius);
        float maxRadius = Mathf.Max(GetBaseOptionRadius(optionOffsets), optionLayoutMaxRadius);
        float pushStrength = Mathf.Clamp01(optionCollisionPushStrength);
        int iterations = Mathf.Max(1, optionCollisionIterations);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    Vector2 delta = positions[j] - positions[i];
                    float distance = delta.magnitude;
                    if (distance >= minDistance) continue;

                    Vector2 direction = distance > 0.01f
                        ? delta / distance
                        : GetFallbackCollisionDirection(i, j);
                    Vector2 push = direction * ((minDistance - distance) * 0.5f * pushStrength);

                    positions[i] -= push;
                    positions[j] += push;
                    positions[i] = ClampOptionPositionToRadius(positions[i], center, maxRadius);
                    positions[j] = ClampOptionPositionToRadius(positions[j], center, maxRadius);
                }
            }
        }
    }

    /// <summary>
    /// 当两个启迪完全重合时，提供一个稳定的推开方向。
    /// </summary>
    /// <param name="firstIndex">第一个启迪的列表索引。</param>
    /// <param name="secondIndex">第二个启迪的列表索引。</param>
    /// <returns>归一化推开方向。</returns>
    private Vector2 GetFallbackCollisionDirection(int firstIndex, int secondIndex)
    {
        float angle = (firstIndex * 97f + secondIndex * 53f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
    }

    /// <summary>
    /// 限制启迪位置不要被碰撞解算推到离法术中心过远。
    /// </summary>
    /// <param name="position">待限制的位置。</param>
    /// <param name="center">法术中心局部坐标。</param>
    /// <param name="maxRadius">允许的最大半径。</param>
    /// <returns>限制后的启迪位置。</returns>
    private Vector2 ClampOptionPositionToRadius(Vector2 position, Vector2 center, float maxRadius)
    {
        Vector2 offset = position - center;
        if (offset.magnitude <= maxRadius) return position;
        return center + offset.normalized * maxRadius;
    }

    /// <summary>
    /// 获取碰撞解算后的启迪位置。
    /// </summary>
    /// <param name="inspiration">需要查询的启迪记录。</param>
    /// <param name="position">输出解算后的局部位置。</param>
    /// <returns>存在解算位置时返回 true。</returns>
    private bool TryGetResolvedInspirationPosition(ForgeInspiration inspiration, out Vector2 position)
    {
        return _resolvedInspirationPositions.TryGetValue(inspiration, out position);
    }

    /// <summary>
    /// 将启迪按钮放置到法术图标周围的指定位置。
    /// </summary>
    /// <param name="optionObject">启迪按钮对象。</param>
    /// <param name="index">启迪位置索引。</param>
    /// <param name="offsets">优先使用的 Inspector 配置偏移表。</param>
    /// <param name="playAppearAnimation">是否播放出现动画。</param>
    /// <param name="targetOverride">碰撞解算后的目标位置；为空时按 index 计算。</param>
    /// <param name="shiftStartPosition">已有启迪被挤开时的移动起点；为空时直接进入待机。</param>
    private void PositionOptionButton(
        GameObject optionObject,
        int index,
        List<Vector2> offsets,
        bool playAppearAnimation = true,
        Vector2? targetOverride = null,
        Vector2? shiftStartPosition = null)
    {
        if (!positionOptionsAroundSpellIcon || optionObject == null || optionsContainer == null) return;

        RectTransform optionRect = optionObject.GetComponent<RectTransform>();
        if (optionRect == null) return;

        var layoutGroup = optionsContainer.GetComponent<LayoutGroup>();
        if (layoutGroup != null) layoutGroup.enabled = false;

        if (!TryGetOptionPoints(index, offsets, out Vector2 center, out Vector2 targetPosition))
            return;
        if (targetOverride.HasValue)
            targetPosition = targetOverride.Value;

        optionRect.anchorMin = new Vector2(0.5f, 0.5f);
        optionRect.anchorMax = new Vector2(0.5f, 0.5f);
        optionRect.pivot = new Vector2(0.5f, 0.5f);
        optionRect.anchoredPosition = targetPosition;

        if (playAppearAnimation)
            ForgeUIEffects.PlayOptionAppearAndIdle(
                optionRect,
                center,
                targetPosition,
                index,
                optionAppearDuration,
                optionFloatDistance,
                optionFloatDuration);
        else if (shiftStartPosition.HasValue)
            ForgeUIEffects.PlayOptionShiftAndIdle(
                optionRect,
                shiftStartPosition.Value,
                targetPosition,
                index,
                optionAppearDuration,
                optionFloatDistance,
                optionFloatDuration);
        else
            ForgeUIEffects.StartOptionIdleFloat(optionRect, targetPosition, index, optionFloatDistance, optionFloatDuration);
    }

    /// <summary>
    /// 为已刻印启迪绘制从法术图标边缘到启迪图标边缘的星座连线。
    /// </summary>
    /// <param name="index">启迪位置索引，用于稳定随机种子。</param>
    /// <param name="affix">连线对应的词条配置。</param>
    /// <param name="optionRect">启迪按钮 RectTransform。</param>
    private void DrawCommittedOptionLine(int index, ForgeAffixSO affix, RectTransform optionRect)
    {
        if (!showCommittedOptionLines || optionsContainer == null) return;
        if (!TryGetOptionPoints(index, optionOffsets, out Vector2 center, out Vector2 targetPosition)) return;

        RectTransform parentRect = optionsContainer as RectTransform;
        RectTransform centerRect = GetCenterRectTransform();
        RectTransform optionAnchorRect = GetOptionAnchorRect(optionRect);
        Vector2 centerSize = centerRect != null ? centerRect.rect.size : Vector2.zero;
        if (centerSize.x < 20f && centerSize.y < 20f) centerSize = new Vector2(64f, 64f);
        if (parentRect != null && centerRect != null &&
            TryGetRectCenterInContainer(centerRect, parentRect, out Vector2 centerVisualPosition))
        {
            center = centerVisualPosition;
        }

        Vector2 optionSize = optionAnchorRect != null ? optionAnchorRect.rect.size : GetOptionPrefabSize();
        if (optionSize.x < 20f && optionSize.y < 20f) optionSize = new Vector2(64f, 64f);
        if (parentRect != null && optionAnchorRect != null &&
            TryGetRectCenterInContainer(optionAnchorRect, parentRect, out Vector2 optionCenter))
        {
            targetPosition = optionCenter;
        }

        Vector2 centerEdge = GetIconEdgePoint(center, centerSize, targetPosition);
        Vector2 optionEdge = GetIconEdgePoint(targetPosition, optionSize, center);
        Vector2 delta = optionEdge - centerEdge;
        float length = delta.magnitude;
        if (length <= 0.01f) return;

        int seed = GetConstellationSeed(affix, index);
        if (TryCreateWorldConstellationLine(
                parentRect: optionsContainer as RectTransform,
                centerRect: centerRect,
                optionRect: optionAnchorRect,
                start: centerEdge,
                end: optionEdge,
                seed: seed,
                color: constellationLineColor,
                progress: 1f,
                objectName: "CommittedAffixWorldLine",
                out _))
        {
            return;
        }

        CreateConstellationLine(
            optionsContainer,
            centerEdge,
            optionEdge,
            seed,
            constellationLineColor,
            GetFallbackLineThickness(),
            progress: 1f,
            "CommittedAffixLine");
    }

    /// <summary>
    /// 获取启迪按钮 prefab 的 RectTransform 尺寸。
    /// </summary>
    /// <returns>prefab 尺寸；没有 prefab 时返回 Vector2.zero。</returns>
    private Vector2 GetOptionPrefabSize()
    {
        if (optionButtonPrefab == null) return Vector2.zero;

        RectTransform prefabRect = optionButtonPrefab.GetComponent<RectTransform>();
        return prefabRect != null ? prefabRect.rect.size : Vector2.zero;
    }

    /// <summary>
    /// 计算法术中心点和指定启迪位置点。
    /// </summary>
    /// <param name="index">启迪位置索引。</param>
    /// <param name="offsets">优先使用的偏移表。</param>
    /// <param name="center">输出法术中心局部坐标。</param>
    /// <param name="targetPosition">输出启迪目标局部坐标。</param>
    /// <returns>计算成功返回 true。</returns>
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

    /// <summary>
    /// 将中心法术图标的位置转换到启迪容器局部坐标。
    /// </summary>
    /// <param name="parentRect">启迪容器 RectTransform。</param>
    /// <returns>中心点在启迪容器内的局部坐标。</returns>
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

    /// <summary>
    /// 获取指定启迪位置索引对应的偏移。
    /// 配置表不够用时自动生成额外环形位置。
    /// </summary>
    /// <param name="index">启迪位置索引。</param>
    /// <param name="offsets">Inspector 配置的基础位置偏移。</param>
    /// <returns>相对法术中心的偏移。</returns>
    private Vector2 GetOptionOffset(int index, List<Vector2> offsets)
    {
        if (index < 0) index = 0;
        if (offsets != null && index < offsets.Count)
            return offsets[index];

        int configuredCount = offsets != null ? offsets.Count : 0;
        int generatedIndex = Mathf.Max(0, index - configuredCount);
        int pointsPerRing = 8;
        int ring = generatedIndex / pointsPerRing;
        int slot = generatedIndex % pointsPerRing;
        float baseRadius = GetBaseOptionRadius(offsets);
        float radius = baseRadius + Mathf.Max(0f, generatedOptionRadiusStep) * ring;
        float angle = -90f + slot * (360f / pointsPerRing);
        float radians = angle * Mathf.Deg2Rad;

        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
    }

    /// <summary>
    /// 根据已配置偏移估算自动生成位置使用的基础半径。
    /// </summary>
    /// <param name="offsets">Inspector 配置的基础位置偏移。</param>
    /// <returns>自动位置生成的基础半径。</returns>
    private float GetBaseOptionRadius(List<Vector2> offsets)
    {
        float radius = 230f;
        if (offsets == null) return radius;

        foreach (Vector2 offset in offsets)
            radius = Mathf.Max(radius, offset.magnitude);

        return radius;
    }

    // ── Hold Commit ──

    /// <summary>
    /// 兼容入口：根据词条和 UI 对象查找当前启迪，并启动长按刻印。
    /// </summary>
    /// <param name="affix">被按下的词条配置。</param>
    /// <param name="optionRect">被按下启迪按钮的 RectTransform。</param>
    public void OnOptionPressStart(ForgeAffixSO affix, RectTransform optionRect)
    {
        OnOptionPressStart(FindPendingInspiration(affix, optionRect), optionRect);
    }

    /// <summary>
    /// 启动指定启迪的长按刻印流程。
    /// </summary>
    /// <param name="inspiration">被按下的当前会话启迪。</param>
    /// <param name="optionRect">被按下启迪按钮的 RectTransform。</param>
    public void OnOptionPressStart(ForgeInspiration inspiration, RectTransform optionRect)
    {
        if (_isCommittingAffix || _holdActive) return;
        if (inspiration == null || inspiration.affix == null || optionRect == null) return;
        if (!IsCurrentSessionInspiration(inspiration)) return;

        TooltipSystem.Instance?.Hide();
        _holdCoroutine = StartCoroutine(HoldCommitSequence(inspiration, optionRect));
    }

    /// <summary>
    /// 结束长按输入；如果尚未达到时长，会取消刻印。
    /// </summary>
    public void OnOptionPressEnd()
    {
        if (!_holdActive) return;
        _holdActive = false;
    }

    /// <summary>
    /// 长按刻印协程。
    /// 长按期间播放不稳定连线和抖动，完成后提交刻印并刷新 UI。
    /// </summary>
    /// <param name="inspiration">玩家正在长按的启迪记录。</param>
    /// <param name="optionRect">启迪按钮 RectTransform。</param>
    /// <returns>长按刻印流程协程。</returns>
    private System.Collections.IEnumerator HoldCommitSequence(ForgeInspiration inspiration, RectTransform optionRect)
    {
        ForgeAffixSO affix = inspiration != null ? inspiration.affix : null;
        if (affix == null) yield break;

        _holdActive = true;
        _holdElapsed = 0f;
        _holdAffix = affix;
        _holdOptionRect = optionRect;

        RectTransform container = optionsContainer as RectTransform;
        if (container == null) { _holdActive = false; yield break; }

        // Get center rect for edge calculation
        RectTransform centerRect = GetCenterRectTransform();
        Vector2 centerPos = GetOptionCenterInOptionsContainer(container);
        if (centerRect != null &&
            TryGetRectCenterInContainer(centerRect, container, out Vector2 centerVisualPosition))
        {
            centerPos = centerVisualPosition;
        }
        RectTransform optionAnchorRect = GetOptionAnchorRect(optionRect);
        Vector2 optionPos = optionRect.anchoredPosition;
        if (optionAnchorRect != null &&
            TryGetRectCenterInContainer(optionAnchorRect, container, out Vector2 optionCenter))
        {
            optionPos = optionCenter;
        }

        // Calculate edge positions with minimum size fallback for icons
        Vector2 centerSize = centerRect != null ? centerRect.rect.size : Vector2.zero;
        if (centerSize.x < 20f && centerSize.y < 20f) centerSize = new Vector2(64f, 64f);
        Vector2 centerEdge = GetIconEdgePoint(centerPos, centerSize, optionPos);
        Vector2 optionSize = optionAnchorRect != null ? optionAnchorRect.rect.size : optionRect.rect.size;
        Vector2 optionEdge = GetIconEdgePoint(optionPos, optionSize, centerPos);

        // Grow one generated constellation line from the spell icon toward the option icon.
        int holdIndex = inspiration.optionIndex >= 0 ? inspiration.optionIndex : GetPendingOptionIndex(_holdAffix);
        int holdSeed = GetConstellationSeed(_holdAffix, holdIndex);
        if (!TryCreateWorldConstellationLine(
                container,
                centerRect,
                optionAnchorRect,
                centerEdge,
                optionEdge,
                holdSeed,
                constellationLineColor,
                progress: 0f,
                objectName: "HoldConstellationWorldLine",
                out _holdWorldLine))
        {
            _holdLine = CreateConstellationLine(
                container,
                centerEdge,
                optionEdge,
                holdSeed,
                constellationLineColor,
                GetFallbackLineThickness(),
                progress: 0f,
                "HoldConstellationLine");
        }
        _holdLineBasePos = _holdLine != null ? _holdLine.RectTransform.anchoredPosition : Vector2.zero;

        _holdCenterIconRect = spellIconImage != null ? spellIconImage.rectTransform : GetCenterRectTransform();
        _holdCenterIconBaseScale = _holdCenterIconRect != null ? _holdCenterIconRect.localScale : Vector3.one;
        _holdCenterIconBasePos = _holdCenterIconRect != null ? _holdCenterIconRect.anchoredPosition : Vector2.zero;
        _holdOptionBaseScale = optionRect.localScale;
        _holdOptionBasePos = optionRect.anchoredPosition;

        ForgeUIEffects.StopTransformTween(optionRect);
        ForgeUIEffects.StopTransformTween(_holdCenterIconRect);

        float shakeTimer = 0f;
        Vector2 lineShakeOffset = Vector2.zero;

        while (_holdElapsed < holdDuration && _holdActive)
        {
            _holdElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_holdElapsed / holdDuration);

            // Update shake offset at frequency
            shakeTimer -= Time.deltaTime;
            if (shakeTimer <= 0f)
            {
                shakeTimer = holdShakeFrequency;
                float intensity = holdShakeIntensity * (1f - progress * 0.4f);
                Vector2 shakeOffset = new Vector2(
                    UnityEngine.Random.Range(-intensity, intensity),
                    UnityEngine.Random.Range(-intensity, intensity));
                lineShakeOffset = shakeOffset;
            }

            // Reveal lines via fill amount
            if (_holdWorldLine != null)
            {
                _holdWorldLine.SetProgress(progress);
                _holdWorldLine.SetScreenShake(lineShakeOffset);
            }
            else
            {
                if (_holdLine != null) _holdLine.SetProgress(progress);
                ApplyHoldLineShake(_holdLine, lineShakeOffset);
            }

            // Shake both ends of the pending imprint.
            float iconShake = holdShakeIntensity * (1f - progress * 0.45f);
            float scaleShake = holdShakeIntensity * 0.015f * (1f - progress * 0.4f);
            ForgeUIEffects.ApplyHoldIconShake(optionRect, _holdOptionBasePos, _holdOptionBaseScale, iconShake, scaleShake);
            ForgeUIEffects.ApplyHoldIconShake(_holdCenterIconRect, _holdCenterIconBasePos, _holdCenterIconBaseScale, iconShake, scaleShake);

            yield return null;
        }

        if (_holdActive && _holdElapsed >= holdDuration)
        {
            if (_holdWorldLine != null)
            {
                _holdWorldLine.SetProgress(1f);
                _holdWorldLine.SetScreenShake(Vector2.zero);
            }
            if (_holdLine != null) _holdLine.SetProgress(1f);
            yield return ForgeUIEffects.PlayHoldSuccessFeedback(
                _holdLine != null ? _holdLine.RectTransform : null,
                optionRect,
                _holdCenterIconRect);

            ResetHoldTransforms();
            _holdActive = false;
            _isCommittingAffix = true;

            PlayerDice committedDice = _selectedDice;
            GameObject selectedOption = FindOptionObject(inspiration);

            PlayAffixCommitAnimation(selectedOption, () =>
            {
                RefundSlotResources();
                ForgeManager.Instance.CommitAffix(inspiration);
                RefreshDiceList();
                SelectDice(committedDice);
                _holdWorldLine = null;
                _holdLine = null;
                RefreshBag();
                _isCommittingAffix = false;
                UpdateUI();
            });
        }
        else
        {
            // Canceled — destroy lines, reset option
            DestroyHoldLines();
            ResetHoldTransforms();
            _holdActive = false;
        }
    }

    /// <summary>
    /// 给 UI 备用线段应用长按期间的抖动偏移。
    /// </summary>
    /// <param name="line">需要抖动的 UI 线段。</param>
    /// <param name="shake">屏幕/局部空间抖动偏移。</param>
    private void ApplyHoldLineShake(ForgeConstellationLine line, Vector2 shake)
    {
        if (line == null) return;
        line.RectTransform.anchoredPosition = _holdLineBasePos + shake;
    }

    /// <summary>
    /// 创建 UI 层备用星座连线。
    /// </summary>
    /// <param name="parent">线段父节点。</param>
    /// <param name="start">起点局部坐标。</param>
    /// <param name="end">终点局部坐标。</param>
    /// <param name="seed">随机种子。</param>
    /// <param name="color">线段颜色。</param>
    /// <param name="thickness">线段厚度。</param>
    /// <param name="progress">初始显现进度。</param>
    /// <param name="objectName">生成对象名称。</param>
    /// <returns>创建出的 UI 星座线段。</returns>
    private ForgeConstellationLine CreateConstellationLine(Transform parent, Vector2 start, Vector2 end, int seed, Color color, float thickness, float progress, string objectName)
    {
        var line = ForgeConstellationLine.Create(
            parent,
            start,
            end,
            seed,
            color,
            thickness,
            constellationNodeSize,
            constellationBendOffset * constellationBendExaggeration,
            constellationBendMinT,
            constellationBendMaxT,
            objectName);
        if (line != null) line.SetProgress(progress);
        return line;
    }

    /// <summary>
    /// 尝试创建世界空间星座连线效果。
    /// </summary>
    /// <param name="parentRect">启迪容器 RectTransform。</param>
    /// <param name="centerRect">中心法术图标 RectTransform。</param>
    /// <param name="optionRect">启迪图标 RectTransform。</param>
    /// <param name="start">起点局部坐标。</param>
    /// <param name="end">终点局部坐标。</param>
    /// <param name="seed">随机种子。</param>
    /// <param name="color">线段颜色。</param>
    /// <param name="progress">初始显现进度。</param>
    /// <param name="objectName">生成对象名称。</param>
    /// <param name="effect">输出创建成功的世界空间连线效果。</param>
    /// <returns>创建成功返回 true。</returns>
    private bool TryCreateWorldConstellationLine(
        RectTransform parentRect,
        RectTransform centerRect,
        RectTransform optionRect,
        Vector2 start,
        Vector2 end,
        int seed,
        Color color,
        float progress,
        string objectName,
        out ForgeConstellationEffect effect)
    {
        effect = null;
        if (!useWorldConstellationEffect || parentRect == null) return false;

        Camera effectCamera = GetConstellationEffectCamera();
        if (effectCamera == null) return false;
        Vector2 bend = GetConstellationBendPoint(start, end, seed);
        if (!TryLocalPointToScreenPoint(parentRect, start, out Vector2 startScreen)) return false;
        if (!TryLocalPointToScreenPoint(parentRect, bend, out Vector2 bendScreen)) return false;
        if (!TryLocalPointToScreenPoint(parentRect, end, out Vector2 endScreen)) return false;

        effect = ForgeConstellationEffect.Create(
            effectCamera,
            constellationEffectRoot,
            startScreen,
            bendScreen,
            endScreen,
            seed,
            color,
            constellationLineWidth,
            constellationParticleSize,
            constellationParticleSpread,
            constellationHdrIntensity,
            constellationNodeSprite,
            constellationNodeSize,
            constellationEffectDepth,
            objectName);
        if (effect == null) return false;

        effect.SetProgress(progress);
        effect.SetIdleMotion(constellationIdleBendAmplitude, constellationIdleBendSpeed, (seed & 1023) * 0.017f);
        _activeWorldConstellationEffects.Add(effect);
        _worldConstellationBindings.Add(new WorldConstellationBinding
        {
            Effect = effect,
            ParentRect = parentRect,
            CenterRect = centerRect,
            OptionRect = optionRect,
            Seed = seed
        });
        return true;
    }

    /// <summary>
    /// 将世界空间线宽换算为 UI 备用线段的大致厚度。
    /// </summary>
    /// <returns>UI 备用线段厚度。</returns>
    private float GetFallbackLineThickness()
    {
        return Mathf.Max(2f, constellationLineWidth * 220f);
    }

    /// <summary>
    /// 根据起终点和随机种子计算星座折点。
    /// </summary>
    /// <param name="start">起点局部坐标。</param>
    /// <param name="end">终点局部坐标。</param>
    /// <param name="seed">随机种子。</param>
    /// <returns>折点局部坐标。</returns>
    private Vector2 GetConstellationBendPoint(Vector2 start, Vector2 end, int seed)
    {
        Vector2 delta = end - start;
        if (delta.sqrMagnitude <= 0.01f) return start;

        Vector2 dir = delta.normalized;
        Vector2 perpendicular = new Vector2(-dir.y, dir.x);

        float minT = Mathf.Clamp01(Mathf.Min(constellationBendMinT, constellationBendMaxT));
        float maxT = Mathf.Clamp01(Mathf.Max(constellationBendMinT, constellationBendMaxT));
        if (Mathf.Approximately(minT, maxT)) maxT = Mathf.Clamp01(minT + 0.01f);

        System.Random random = new System.Random(seed & int.MaxValue);
        float t = Mathf.Lerp(minT, maxT, (float)random.NextDouble());
        float sign = random.Next(0, 2) == 0 ? -1f : 1f;
        float offsetScale = Mathf.Lerp(0.35f, 1f, (float)random.NextDouble());
        return Vector2.Lerp(start, end, t) + perpendicular * constellationBendOffset * constellationBendExaggeration * offsetScale * sign;
    }

    /// <summary>
    /// 获取星座世界空间效果使用的相机。
    /// </summary>
    /// <returns>显式配置的相机；未配置时返回 Camera.main。</returns>
    private Camera GetConstellationEffectCamera()
    {
        if (constellationEffectCamera != null) return constellationEffectCamera;
        return Camera.main;
    }

    /// <summary>
    /// 将启迪容器局部坐标转换为屏幕坐标。
    /// </summary>
    /// <param name="parentRect">局部坐标所属的 RectTransform。</param>
    /// <param name="localPoint">局部坐标点。</param>
    /// <param name="screenPoint">输出屏幕坐标。</param>
    /// <returns>转换成功返回 true。</returns>
    private bool TryLocalPointToScreenPoint(RectTransform parentRect, Vector2 localPoint, out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (parentRect == null) return false;

        Canvas canvas = parentRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3 worldPoint = parentRect.TransformPoint(localPoint);
        screenPoint = RectTransformUtility.WorldToScreenPoint(camera, worldPoint);
        return true;
    }

    /// <summary>
    /// 为某个启迪连线生成稳定随机种子。
    /// </summary>
    /// <param name="affix">连线对应词条。</param>
    /// <param name="index">启迪位置索引。</param>
    /// <returns>稳定随机种子。</returns>
    private int GetConstellationSeed(ForgeAffixSO affix, int index)
    {
        unchecked
        {
            int seed = 17;
            seed = seed * 31 + index;
            seed = seed * 31 + (affix != null ? affix.GetInstanceID() : 0);
            seed = seed * 31 + GetDiceSeed(_selectedDice);
            return seed;
        }
    }

    /// <summary>
    /// 根据骰子 uid 或名称生成稳定种子片段。
    /// </summary>
    /// <param name="dice">当前玩家骰子。</param>
    /// <returns>骰子对应的种子值。</returns>
    private static int GetDiceSeed(PlayerDice dice)
    {
        if (dice == null) return 0;
        if (!string.IsNullOrEmpty(dice.uid)) return dice.uid.GetHashCode();
        return !string.IsNullOrEmpty(dice.diceName) ? dice.diceName.GetHashCode() : 0;
    }

    /// <summary>
    /// 在当前会话的临时词条列表中查找指定词条索引。
    /// </summary>
    /// <param name="affix">要查找的词条。</param>
    /// <returns>词条索引；未找到时返回当前已刻印数量作为回退。</returns>
    private int GetPendingOptionIndex(ForgeAffixSO affix)
    {
        if (ForgeManager.Instance == null || affix == null) return GetForgedCount(_selectedDice);

        var options = ForgeManager.Instance.GetCurrentOptions();
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == affix) return i;
        }

        return GetForgedCount(_selectedDice);
    }

    /// <summary>
    /// 根据启迪按钮当前位置推断最近的启迪位置索引。
    /// </summary>
    /// <param name="optionRect">启迪按钮 RectTransform。</param>
    /// <param name="affix">用于回退查找的词条配置。</param>
    /// <returns>推断出的启迪位置索引。</returns>
    private int GetOptionIndexFromRect(RectTransform optionRect, ForgeAffixSO affix)
    {
        RectTransform parentRect = optionsContainer as RectTransform;
        if (optionRect == null || parentRect == null || optionOffsets == null || optionOffsets.Count == 0)
            return GetPendingOptionIndex(affix);

        Vector2 center = GetOptionCenterInOptionsContainer(parentRect);
        Vector2 currentPosition = optionRect.anchoredPosition;
        int nearestIndex = 0;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < optionOffsets.Count; i++)
        {
            Vector2 targetPosition = center + optionOffsets[i];
            float distance = (currentPosition - targetPosition).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    /// <summary>
    /// 获取已刻印槽位对应的启迪位置索引。
    /// </summary>
    /// <param name="slot">已刻印槽位。</param>
    /// <returns>启迪位置索引；旧数据没有记录时回退到槽位 tier。</returns>
    private int GetCommittedOptionIndex(ForgeSlot slot)
    {
        if (slot?.affix == null) return 0;
        if (slot.optionIndex >= 0)
            return slot.optionIndex;

        return Mathf.Max(0, slot.tier - 1);
    }

    /// <summary>
    /// 获取星座连线和启迪排布使用的中心图标 RectTransform。
    /// </summary>
    /// <returns>优先返回法术图标，其次骰子图标，再次手动配置的中心点。</returns>
    private RectTransform GetCenterRectTransform()
    {
        if (spellIconImage != null) return spellIconImage.rectTransform;
        if (currentDiceIcon != null) return currentDiceIcon.rectTransform;
        if (optionPlacementCenter != null) return optionPlacementCenter;
        return null;
    }

    /// <summary>
    /// 获取启迪按钮中实际用于连线的图标 RectTransform。
    /// </summary>
    /// <param name="optionRect">启迪按钮根 RectTransform。</param>
    /// <returns>启迪图标 RectTransform；没有图标时返回根 RectTransform。</returns>
    private static RectTransform GetOptionAnchorRect(RectTransform optionRect)
    {
        if (optionRect == null) return null;

        ForgeOptionButton optionButton = optionRect.GetComponent<ForgeOptionButton>();
        if (optionButton != null && optionButton.iconImage != null)
            return optionButton.iconImage.rectTransform;

        return optionRect;
    }

    /// <summary>
    /// 将某个 RectTransform 的中心点转换到指定容器的局部坐标。
    /// </summary>
    /// <param name="source">源 RectTransform。</param>
    /// <param name="container">目标容器 RectTransform。</param>
    /// <param name="localPoint">输出局部坐标。</param>
    /// <returns>转换成功返回 true。</returns>
    private static bool TryGetRectCenterInContainer(RectTransform source, RectTransform container, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (source == null || container == null) return false;

        Canvas canvas = container.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, source.position);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screenPoint, camera, out localPoint);
    }

    /// <summary>
    /// 计算从图标中心朝目标方向延伸到图标边缘的点。
    /// </summary>
    /// <param name="center">图标中心点。</param>
    /// <param name="size">图标尺寸。</param>
    /// <param name="target">目标点。</param>
    /// <returns>图标边缘点。</returns>
    private static Vector2 GetIconEdgePoint(Vector2 center, Vector2 size, Vector2 target)
    {
        Vector2 dir = (target - center).normalized;
        if (dir == Vector2.zero) return center;

        float radius = Mathf.Max(1f, Mathf.Min(size.x, size.y) * 0.5f + 3f);
        return center + dir * radius;
    }

    /// <summary>
    /// 销毁长按过程中临时生成的星座连线。
    /// </summary>
    private void DestroyHoldLines()
    {
        if (_holdWorldLine != null)
        {
            _activeWorldConstellationEffects.Remove(_holdWorldLine);
            RemoveWorldConstellationBinding(_holdWorldLine);
            Destroy(_holdWorldLine.gameObject);
            _holdWorldLine = null;
        }
        if (_holdLine != null) { Destroy(_holdLine.gameObject); _holdLine = null; }
    }

    /// <summary>
    /// 清理所有世界空间星座连线效果和绑定记录。
    /// </summary>
    private void ClearWorldConstellationEffects()
    {
        for (int i = _activeWorldConstellationEffects.Count - 1; i >= 0; i--)
        {
            ForgeConstellationEffect effect = _activeWorldConstellationEffects[i];
            if (effect != null) Destroy(effect.gameObject);
        }

        _activeWorldConstellationEffects.Clear();
        _worldConstellationBindings.Clear();
        _holdWorldLine = null;
    }

    /// <summary>
    /// 刷新世界空间连线的实时参数，并让连线跟随 UI 图标位置。
    /// </summary>
    private void RefreshLiveConstellationSettings()
    {
        for (int i = _worldConstellationBindings.Count - 1; i >= 0; i--)
        {
            WorldConstellationBinding binding = _worldConstellationBindings[i];
            ForgeConstellationEffect effect = binding?.Effect;
            if (effect == null)
            {
                _worldConstellationBindings.RemoveAt(i);
                continue;
            }

            if (TryGetLiveConstellationPoints(binding, out Vector2 startScreen, out Vector2 bendScreen, out Vector2 endScreen))
                effect.SetScreenPoints(startScreen, bendScreen, endScreen);

            effect.SetNodeSizePixels(constellationNodeSize);
            effect.SetParticleSpread(constellationParticleSpread);
            effect.SetIdleMotion(constellationIdleBendAmplitude, constellationIdleBendSpeed, (binding.Seed & 1023) * 0.017f);
        }

        for (int i = _activeWorldConstellationEffects.Count - 1; i >= 0; i--)
        {
            if (_activeWorldConstellationEffects[i] == null)
                _activeWorldConstellationEffects.RemoveAt(i);
        }
    }

    /// <summary>
    /// 根据当前 UI 位置计算某条世界空间连线的三个屏幕点。
    /// </summary>
    /// <param name="binding">连线与 UI 锚点的绑定数据。</param>
    /// <param name="startScreen">输出起点屏幕坐标。</param>
    /// <param name="bendScreen">输出折点屏幕坐标。</param>
    /// <param name="endScreen">输出终点屏幕坐标。</param>
    /// <returns>计算成功返回 true。</returns>
    private bool TryGetLiveConstellationPoints(WorldConstellationBinding binding, out Vector2 startScreen, out Vector2 bendScreen, out Vector2 endScreen)
    {
        startScreen = Vector2.zero;
        bendScreen = Vector2.zero;
        endScreen = Vector2.zero;
        if (binding == null || binding.ParentRect == null || binding.CenterRect == null || binding.OptionRect == null)
            return false;

        if (!TryGetConstellationLocalEdges(
                binding.ParentRect,
                binding.CenterRect,
                binding.OptionRect,
                out Vector2 start,
                out Vector2 end))
        {
            return false;
        }

        Vector2 bend = GetConstellationBendPoint(start, end, binding.Seed);
        return TryLocalPointToScreenPoint(binding.ParentRect, start, out startScreen)
            && TryLocalPointToScreenPoint(binding.ParentRect, bend, out bendScreen)
            && TryLocalPointToScreenPoint(binding.ParentRect, end, out endScreen);
    }

    /// <summary>
    /// 计算法术图标和启迪图标边缘上的连线端点。
    /// </summary>
    /// <param name="parentRect">启迪容器 RectTransform。</param>
    /// <param name="centerRect">中心法术图标 RectTransform。</param>
    /// <param name="optionRect">启迪图标 RectTransform。</param>
    /// <param name="centerEdge">输出法术图标边缘点。</param>
    /// <param name="optionEdge">输出启迪图标边缘点。</param>
    /// <returns>端点计算成功返回 true。</returns>
    private bool TryGetConstellationLocalEdges(
        RectTransform parentRect,
        RectTransform centerRect,
        RectTransform optionRect,
        out Vector2 centerEdge,
        out Vector2 optionEdge)
    {
        centerEdge = Vector2.zero;
        optionEdge = Vector2.zero;
        if (parentRect == null || centerRect == null || optionRect == null) return false;
        if (!TryGetRectCenterInContainer(centerRect, parentRect, out Vector2 center)) return false;
        if (!TryGetRectCenterInContainer(optionRect, parentRect, out Vector2 option)) return false;

        Vector2 centerSize = centerRect.rect.size;
        if (centerSize.x < 20f && centerSize.y < 20f) centerSize = new Vector2(64f, 64f);
        Vector2 optionSize = optionRect.rect.size;
        if (optionSize.x < 20f && optionSize.y < 20f) optionSize = new Vector2(64f, 64f);

        centerEdge = GetIconEdgePoint(center, centerSize, option);
        optionEdge = GetIconEdgePoint(option, optionSize, center);
        return (optionEdge - centerEdge).sqrMagnitude > 0.01f;
    }

    /// <summary>
    /// 移除指定世界空间连线对应的绑定记录。
    /// </summary>
    /// <param name="effect">需要移除绑定的星座连线效果。</param>
    private void RemoveWorldConstellationBinding(ForgeConstellationEffect effect)
    {
        for (int i = _worldConstellationBindings.Count - 1; i >= 0; i--)
        {
            if (_worldConstellationBindings[i]?.Effect == effect)
                _worldConstellationBindings.RemoveAt(i);
        }
    }

    /// <summary>
    /// 重置长按期间被抖动的启迪和法术图标变换。
    /// </summary>
    private void ResetHoldTransforms()
    {
        if (_holdOptionRect != null)
        {
            ForgeUIEffects.StopTransformTween(_holdOptionRect);
            _holdOptionRect.localScale = _holdOptionBaseScale;
            _holdOptionRect.anchoredPosition = _holdOptionBasePos;
        }

        if (_holdCenterIconRect != null)
        {
            ForgeUIEffects.StopTransformTween(_holdCenterIconRect);
            _holdCenterIconRect.localScale = _holdCenterIconBaseScale;
            _holdCenterIconRect.anchoredPosition = _holdCenterIconBasePos;

            if (spellIconImage != null && _holdCenterIconRect == spellIconImage.rectTransform)
                StartPreviewBreath();
        }

        _holdCenterIconRect = null;
    }

    /// <summary>
    /// 取消正在进行的长按刻印，并清理临时连线和抖动状态。
    /// </summary>
    private void CancelHold()
    {
        if (!_holdActive) return;
        _holdActive = false;
        DestroyHoldLines();
        ResetHoldTransforms();
    }

    /// <summary>
    /// 根据当前骰子、材料槽和启迪状态刷新按钮、标题和状态文本。
    /// </summary>
    private void UpdateUI()
    {
        bool pendingOptions = HasPendingOptions();
        int generatedCount = pendingOptions ? ForgeManager.Instance.CurrentSession.ForgeCount : 0;
        bool canMeditate = !_isCommittingAffix && !_holdActive && CanMeditate();

        if (confirmButton != null)
            confirmButton.interactable = canMeditate;

        if (confirmButtonLabel != null)
            confirmButtonLabel.text = pendingOptions && generatedCount >= 3 ? "已达上限" : "冥想";

        if (previousDiceButton != null)
            previousDiceButton.interactable = !_isCommittingAffix && !_holdActive && !pendingOptions && _availableDice.Count > 1;
        if (nextDiceButton != null)
            nextDiceButton.interactable = !_isCommittingAffix && !_holdActive && !pendingOptions && _availableDice.Count > 1;

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

    /// <summary>
    /// 判断当前是否满足点击冥想按钮的条件。
    /// </summary>
    /// <returns>目标骰子存在、材料槽放满且启迪未超上限时返回 true。</returns>
    private bool CanMeditate()
    {
        if (_selectedDice == null || ForgeManager.Instance == null) return false;
        if (!AllSlotsFilled()) return false;
        if (HasPendingOptions() && !ForgeManager.Instance.CanForgeMore) return false;
        return true;
    }

    /// <summary>
    /// 检查三个材料槽是否全部放入材料。
    /// </summary>
    /// <returns>全部填满返回 true。</returns>
    private bool AllSlotsFilled()
    {
        for (int i = 0; i < MaterialSlotCount; i++)
            if (_slotResources[i] == null) return false;
        return true;
    }

    /// <summary>
    /// 判断当前骰子是否存在本轮尚未刻印的启迪。
    /// </summary>
    /// <returns>当前会话属于所选骰子且已生成启迪时返回 true。</returns>
    private bool HasPendingOptions()
    {
        return ForgeManager.Instance != null
            && ForgeManager.Instance.CurrentSession != null
            && ForgeManager.Instance.CurrentSession.targetDice == _selectedDice
            && ForgeManager.Instance.CurrentSession.generatedOptions.Count > 0;
    }

    /// <summary>
    /// 统计指定骰子已经刻印的槽位数量。
    /// </summary>
    /// <param name="dice">需要统计的玩家骰子。</param>
    /// <returns>已刻印槽位数量。</returns>
    private int GetForgedCount(PlayerDice dice)
    {
        if (dice?.forgeSlots == null) return 0;

        int count = 0;
        foreach (var slot in dice.forgeSlots)
            if (slot != null && slot.isForged) count++;
        return count;
    }

    /// <summary>
    /// 刷新指定材料槽的图标和颜色。
    /// </summary>
    /// <param name="index">要刷新的材料槽索引。</param>
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

    /// <summary>
    /// 清空所有材料槽，并可选择是否返还材料。
    /// </summary>
    /// <param name="shouldRefund">为 true 时将槽内材料返还到背包。</param>
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

    /// <summary>
    /// 将所有材料槽中的材料返还到背包并清空槽位。
    /// </summary>
    private void RefundSlotResources()
    {
        ClearSlots(shouldRefund: true);
    }

    /// <summary>
    /// 切换骰子前清理材料槽，并播放材料退回动画。
    /// </summary>
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

    /// <summary>
    /// 播放指定材料槽的清空动画。
    /// </summary>
    /// <param name="index">材料槽索引。</param>
    /// <param name="delay">动画开始前的延迟。</param>
    private void AnimateMaterialSlotClear(int index, float delay)
    {
        if (index < 0 || index >= _materialSlotImages.Count) return;

        Image image = _materialSlotImages[index];
        ForgeUIEffects.AnimateMaterialSlotClear(image, delay, () => RefreshSlot(index));
    }

    /// <summary>
    /// 播放材料放入或替换反馈动画。
    /// </summary>
    /// <param name="index">材料槽索引。</param>
    /// <param name="isReplace">为 true 时播放替换动画。</param>
    private void PlayMaterialSlotChangedAnimation(int index, bool isReplace)
    {
        if (index < 0 || index >= _materialSlotImages.Count) return;

        Image image = _materialSlotImages[index];
        ForgeUIEffects.PlayMaterialSlotChanged(image, isReplace, materialPopDuration, materialReplaceDuration);
    }

    /// <summary>
    /// 根据词条配置查找当前界面中的启迪按钮对象。
    /// </summary>
    /// <param name="affix">要查找的词条配置。</param>
    /// <returns>匹配的启迪按钮对象；未找到时返回 null。</returns>
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

    /// <summary>
    /// 根据持久启迪记录查找当前界面中的启迪按钮对象。
    /// </summary>
    /// <param name="inspiration">要查找的启迪记录。</param>
    /// <returns>匹配的启迪按钮对象；未找到时返回 null。</returns>
    private GameObject FindOptionObject(ForgeInspiration inspiration)
    {
        if (inspiration == null || optionsContainer == null) return null;

        foreach (Transform child in optionsContainer)
        {
            var optionButton = child.GetComponent<ForgeOptionButton>();
            if (optionButton != null && optionButton.Inspiration == inspiration)
                return child.gameObject;
        }

        return null;
    }

    /// <summary>
    /// 从启迪按钮对象上读取它绑定的持久启迪记录。
    /// </summary>
    /// <param name="optionObject">启迪按钮对象。</param>
    /// <returns>绑定的启迪记录；没有绑定时返回 null。</returns>
    private ForgeInspiration GetOptionInspiration(GameObject optionObject)
    {
        if (optionObject == null) return null;

        var optionButton = optionObject.GetComponent<ForgeOptionButton>();
        return optionButton != null ? optionButton.Inspiration : null;
    }

    /// <summary>
    /// 从当前会话中查找可刻印的启迪记录。
    /// </summary>
    /// <param name="affix">用于兼容查找的词条配置。</param>
    /// <param name="optionRect">优先读取的启迪按钮 RectTransform。</param>
    /// <returns>当前会话中的启迪记录；未找到时返回 null。</returns>
    private ForgeInspiration FindPendingInspiration(ForgeAffixSO affix, RectTransform optionRect)
    {
        if (optionRect != null)
        {
            var optionButton = optionRect.GetComponent<ForgeOptionButton>();
            if (optionButton != null && optionButton.Inspiration != null)
                return optionButton.Inspiration;
        }

        if (ForgeManager.Instance == null || ForgeManager.Instance.CurrentSession == null || affix == null)
            return null;

        foreach (var inspiration in ForgeManager.Instance.GetCurrentInspirations())
        {
            if (inspiration != null && inspiration.affix == affix)
                return inspiration;
        }

        return null;
    }

    /// <summary>
    /// 判断某个启迪是否属于当前会话且尚未刻印。
    /// </summary>
    /// <param name="inspiration">要检查的启迪记录。</param>
    /// <returns>属于当前会话并可刻印时返回 true。</returns>
    private bool IsCurrentSessionInspiration(ForgeInspiration inspiration)
    {
        if (inspiration == null || inspiration.isCommitted) return false;
        if (ForgeManager.Instance == null || ForgeManager.Instance.CurrentSession == null) return false;
        if (ForgeManager.Instance.CurrentSession.targetDice != _selectedDice) return false;

        return ForgeManager.Instance.CurrentSession.generatedInspirations != null
            && ForgeManager.Instance.CurrentSession.generatedInspirations.Contains(inspiration);
    }

    /// <summary>
    /// 判断指定刻印槽位是否已经有持久启迪记录负责渲染。
    /// </summary>
    /// <param name="slotIndex">刻印槽位索引。</param>
    /// <returns>已有已刻印启迪记录时返回 true。</returns>
    private bool HasCommittedInspirationForSlot(int slotIndex)
    {
        if (_selectedDice == null || _selectedDice.forgeInspirations == null) return false;

        foreach (var inspiration in _selectedDice.forgeInspirations)
        {
            if (inspiration != null && inspiration.isCommitted && inspiration.slotIndex == slotIndex)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 查找指定骰子周围第一个未被启迪占用的位置索引。
    /// </summary>
    /// <param name="dice">要分配启迪位置的玩家骰子。</param>
    /// <returns>可用的启迪位置索引。</returns>
    private int FindFreeInspirationOptionIndex(PlayerDice dice)
    {
        if (dice?.forgeInspirations == null || dice.forgeInspirations.Count == 0) return 0;

        HashSet<int> occupied = new HashSet<int>();
        foreach (var inspiration in dice.forgeInspirations)
        {
            if (inspiration != null && inspiration.optionIndex >= 0)
                occupied.Add(inspiration.optionIndex);
        }

        for (int i = 0; i <= occupied.Count + 8; i++)
        {
            if (!occupied.Contains(i))
                return i;
        }

        return occupied.Count;
    }

    /// <summary>
    /// 播放刻印确认动画，并在结束后执行回调。
    /// </summary>
    /// <param name="optionObject">被刻印的启迪按钮对象。</param>
    /// <param name="onComplete">动画完成后的回调。</param>
    private void PlayAffixCommitAnimation(GameObject optionObject, Action onComplete)
    {
        ForgeUIEffects.PlayAffixCommit(optionObject, affixCommitDuration, onComplete);
    }

    /// <summary>
    /// 缓存材料槽 Image、默认图标和默认颜色，供刷新和退回动画使用。
    /// </summary>
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

    /// <summary>
    /// 世界空间星座连线和 UI 锚点之间的绑定关系。
    /// 用于每帧根据 UI 位置刷新连线屏幕点。
    /// </summary>
    private class WorldConstellationBinding
    {
        public ForgeConstellationEffect Effect;
        public RectTransform ParentRect;
        public RectTransform CenterRect;
        public RectTransform OptionRect;
        public int Seed;
    }
}
