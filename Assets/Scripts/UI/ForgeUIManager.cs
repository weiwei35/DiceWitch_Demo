using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 冥想/锻造面板的主 UI 控制器。
/// 负责冥想面板的主流程编排，并协调骰子选择、材料输入、启迪显示和长按刻印模块。
/// </summary>
public class ForgeUIManager : MonoBehaviour
{
    public static ForgeUIManager Instance;

    [Header("Panel")]
    public GameObject panelRoot;
    public Button closeButton;

    [Header("Modules")]
    public ForgeDiceSelectionPanel diceSelection;
    public ForgeMaterialInputPanel materialInput;
    public ForgeInspirationPanel inspirationPanel;
    public ForgeConstellationRenderer constellationRenderer;

    [SerializeField, HideInInspector] private float affixCommitDuration = 0.45f;

    [Header("Hold Commit")]
    public float holdDuration = 3f;
    public float holdShakeIntensity = 6f;
    public float holdShakeFrequency = 0.03f;

    [Header("Actions")]
    public Button confirmButton;
    public TextMeshProUGUI confirmButtonLabel;
    public TextMeshProUGUI stepText;
    public TextMeshProUGUI statusText;

    private Action _onComplete;
    private PlayerDice _selectedDice;
    private bool _isCommittingAffix;
    private ForgeInspiration _lastCreatedInspirationForAppear;
    private ForgeResourceSO _guidedBagResource;

    private Image spellIconImage => diceSelection != null ? diceSelection.SpellIconImage : null;
    private Image currentDiceIcon => diceSelection != null ? diceSelection.CurrentDiceIcon : null;

    // Hold commit state
    private bool _holdActive;
    private float _holdElapsed;
    private ForgeAffixSO _holdAffix;
    private RectTransform _holdOptionRect;
    private ForgeConstellationRenderer.LineHandle _holdLine;
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
        InitializeModules();
    }

    /// <summary>
    /// 每帧刷新世界空间星座连线的实时参数和跟随位置。
    /// </summary>
    void Update()
    {
        constellationRenderer?.RefreshLiveSettings();
    }

    /// <summary>
    /// 打开冥想面板并刷新骰子、材料、启迪和预览状态。
    /// </summary>
    /// <param name="onComplete">面板关闭后继续游戏流程的回调。</param>
    public void ShowForge(Action onComplete)
    {
        _onComplete = onComplete;
        BindStaticButtons();
        InitializeModules();
        materialInput?.ResetPanel();
        diceSelection?.RefreshDiceList();
        diceSelection?.SelectByIndex(0);
        RefreshOptions();

        if (panelRoot != null) panelRoot.SetActive(true);
        WeakGuideService.Instance?.ActivateScreen(this);
        diceSelection?.StartBreath();
        UpdateUI();
    }

    /// <summary>
    /// 关闭冥想面板，并清理呼吸动画、长按连线和星座特效。
    /// </summary>
    public void Hide()
    {
        WeakGuideService.Instance?.DeactivateScreen(this);
        diceSelection?.StopBreath();
        DestroyHoldLines();
        constellationRenderer?.ClearAll();
        if (panelRoot != null) panelRoot.SetActive(false);
        TooltipSystem.Instance?.Hide();
    }

    private void InitializeModules()
    {
        if (diceSelection != null)
            diceSelection.Initialize(CanSwitchDice, OnSelectedDiceChanged);

        if (materialInput != null)
            materialInput.Initialize(
                CanEditMaterials,
                UpdateUI,
                OnMaterialBagVisibilityChanged,
                OnMaterialSlotBarOpened,
                OnMaterialResourceSelected);
    }

    private void OnMaterialBagVisibilityChanged(bool isVisible)
    {
        constellationRenderer?.SetVisible(!isVisible);
        if (isVisible)
            WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.ForgeMaterialSlot);
        else
            _guidedBagResource = null;

        RefreshWeakGuide();
    }

    private void OnMaterialSlotBarOpened()
    {
        WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.ForgeMaterialEntry);
        RefreshWeakGuide();
    }

    private void OnMaterialResourceSelected(ForgeResourceSO resource)
    {
        if (resource != null && resource == _guidedBagResource)
            WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.ForgeFirstResource);

        RefreshWeakGuide();
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

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
            if (!HasPersistentListener(confirmButton, nameof(OnConfirmClicked)))
                confirmButton.onClick.AddListener(OnConfirmClicked);
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
        materialInput?.RefundAllSlots();
        Hide();
        _onComplete?.Invoke();
    }

    private bool CanSwitchDice()
    {
        return !_isCommittingAffix && !_holdActive && !HasPendingOptions();
    }

    private bool CanEditMaterials()
    {
        if (_isCommittingAffix || _holdActive) return false;
        return !HasPendingOptions() || (ForgeManager.Instance != null && ForgeManager.Instance.CanForgeMore);
    }

    private void OnSelectedDiceChanged(PlayerDice dice)
    {
        _selectedDice = dice;
        materialInput?.ClearForDiceSwitch();
        RefreshOptions();
        UpdateUI();
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

        var resources = materialInput.GetSelectedResources();
        int optionIndex = inspirationPanel != null ? inspirationPanel.FindFreeOptionIndex(_selectedDice) : 0;
        ForgeInspiration inspiration = ForgeManager.Instance.MeditateWithResources(_selectedDice, resources, optionIndex);
        if (inspiration == null) return;

        WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.ForgeMeditate);
        _lastCreatedInspirationForAppear = inspiration;
        materialInput.ClearAfterMeditation();
        materialInput.CloseBagPanel();
        materialInput.RefreshBag();
        RefreshOptions();
        UpdateUI();
    }

    /// <summary>
    /// 重建启迪节点和已刻印连接线。
    /// </summary>
    private void RefreshOptions()
    {
        constellationRenderer?.ClearAll();
        inspirationPanel?.Refresh(
            _selectedDice,
            _lastCreatedInspirationForAppear,
            IsCurrentSessionInspiration,
            DrawCommittedOptionLine,
            GetCenterRectTransform());
        _lastCreatedInspirationForAppear = null;
    }

    /// <summary>
    /// 为已刻印启迪绘制从法术图标边缘到启迪图标边缘的星座连线。
    /// </summary>
    /// <param name="index">启迪位置索引，用于稳定随机种子。</param>
    /// <param name="affix">连线对应的词条配置。</param>
    /// <param name="optionRect">启迪按钮 RectTransform。</param>
    private void DrawCommittedOptionLine(int index, ForgeAffixSO affix, RectTransform optionRect)
    {
        constellationRenderer?.DrawCommittedLine(
            inspirationPanel != null ? inspirationPanel.OptionsContainer : null,
            GetCenterRectTransform(),
            GetOptionAnchorRect(optionRect),
            affix,
            index,
            _selectedDice);
    }

    // ── Hold Commit ──

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

        RectTransform container = inspirationPanel != null ? inspirationPanel.OptionsContainer as RectTransform : null;
        if (container == null) { _holdActive = false; yield break; }

        RectTransform centerRect = GetCenterRectTransform();
        RectTransform optionAnchorRect = GetOptionAnchorRect(optionRect);
        RectTransform holdVisualRect = optionAnchorRect != null ? optionAnchorRect : optionRect;
        _holdOptionRect = holdVisualRect;

        int holdIndex = Mathf.Max(0, inspiration.optionIndex);
        _holdLine = constellationRenderer != null
            ? constellationRenderer.CreateHoldLine(container, centerRect, optionAnchorRect, _holdAffix, holdIndex, _selectedDice)
            : null;

        _holdCenterIconRect = spellIconImage != null ? spellIconImage.rectTransform : GetCenterRectTransform();
        _holdCenterIconBaseScale = _holdCenterIconRect != null ? _holdCenterIconRect.localScale : Vector3.one;
        _holdCenterIconBasePos = _holdCenterIconRect != null ? _holdCenterIconRect.anchoredPosition : Vector2.zero;
        _holdOptionBaseScale = holdVisualRect.localScale;
        _holdOptionBasePos = holdVisualRect.anchoredPosition;

        // 根节点由按钮反馈和弱引导共同管理缩放。长按抖动只写入子图标，
        // 避免短按取消时把已叠加的呼吸倍率再次保存并乘回根节点。
        ForgeUIEffects.StopTransformTween(optionRect);
        if (holdVisualRect != optionRect)
            ForgeUIEffects.StopTransformTween(holdVisualRect);
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

            constellationRenderer?.SetProgress(_holdLine, progress);
            constellationRenderer?.SetShake(_holdLine, lineShakeOffset);

            // Shake both ends of the pending imprint.
            float iconShake = holdShakeIntensity * (1f - progress * 0.45f);
            float scaleShake = holdShakeIntensity * 0.015f * (1f - progress * 0.4f);
            ForgeUIEffects.ApplyHoldIconShake(holdVisualRect, _holdOptionBasePos, _holdOptionBaseScale, iconShake, scaleShake);
            ForgeUIEffects.ApplyHoldIconShake(_holdCenterIconRect, _holdCenterIconBasePos, _holdCenterIconBaseScale, iconShake, scaleShake);

            yield return null;
        }

        if (_holdActive && _holdElapsed >= holdDuration)
        {
            constellationRenderer?.SetProgress(_holdLine, 1f);
            constellationRenderer?.SetShake(_holdLine, Vector2.zero);
            yield return ForgeUIEffects.PlayHoldSuccessFeedback(
                constellationRenderer != null ? constellationRenderer.GetUiRect(_holdLine) : null,
                holdVisualRect,
                _holdCenterIconRect);

            ResetHoldTransforms();
            _holdActive = false;
            _isCommittingAffix = true;

            PlayerDice committedDice = _selectedDice;
            GameObject selectedOption = FindOptionObject(inspiration);

            PlayAffixCommitAnimation(selectedOption, () =>
            {
                materialInput?.RefundAllSlots();
                ForgeManager.Instance.CommitAffix(inspiration);
                if (inspiration.isCommitted)
                    WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.ForgeCommitInspiration);
                diceSelection?.RefreshDiceList();
                diceSelection?.SelectDice(committedDice);
                _holdLine = null;
                materialInput?.RefreshBag();
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
    /// 获取星座连线和启迪排布使用的中心图标 RectTransform。
    /// </summary>
    /// <returns>优先返回法术图标，其次骰子图标，再次手动配置的中心点。</returns>
    private RectTransform GetCenterRectTransform()
    {
        if (spellIconImage != null) return spellIconImage.rectTransform;
        if (currentDiceIcon != null) return currentDiceIcon.rectTransform;
        if (inspirationPanel != null && inspirationPanel.OptionPlacementCenter != null)
            return inspirationPanel.OptionPlacementCenter;
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
    /// 销毁长按过程中临时生成的星座连线。
    /// </summary>
    private void DestroyHoldLines()
    {
        constellationRenderer?.DestroyLine(_holdLine);
        _holdLine = null;
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
                diceSelection?.StartBreath();
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

        diceSelection?.SetNavigationInteractable(!_isCommittingAffix && !_holdActive && !pendingOptions);
        materialInput?.SetToggleInteractable(!_isCommittingAffix && !_holdActive);

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
            else if (materialInput == null || !materialInput.AllSlotsFilled)
                statusText.text = "放满 3 个材料后可以冥想";
            else
                statusText.text = $"将为 {_selectedDice.diceName} 进行冥想";
        }

        RefreshWeakGuide();
    }

    private void RefreshWeakGuide()
    {
        WeakGuideService guideService = WeakGuideService.Instance;
        if (guideService == null || panelRoot == null || !panelRoot.activeInHierarchy)
            return;

        if (materialInput != null && materialInput.IsBagVisible)
        {
            if (!guideService.IsCompleted(WeakGuideIds.ForgeFirstResource)
                && materialInput.TryGetFirstAvailableResourceGuideTarget(
                    out Button resourceButton,
                    out ForgeResourceSO resource)
                && TryGetButtonGuideTarget(
                    resourceButton,
                    out RectTransform resourceTarget,
                    out Graphic resourceGraphic))
            {
                _guidedBagResource = resource;
                guideService.SetScreenSuspended(this, false);
                guideService.ShowGuide(
                    this,
                    WeakGuideIds.ForgeFirstResource,
                    resourceTarget,
                    resourceGraphic);
            }
            else
            {
                _guidedBagResource = null;
                guideService.ClearGuide(this);
                guideService.SetScreenSuspended(this, true);
            }
            return;
        }

        _guidedBagResource = null;
        guideService.SetScreenSuspended(this, false);

        if (HasPendingOptions())
        {
            if (!guideService.IsCompleted(WeakGuideIds.ForgeCommitInspiration)
                && TryGetPreferredInspirationGuideTarget(out RectTransform optionTarget, out Graphic optionGraphic))
            {
                guideService.ShowGuide(
                    this,
                    WeakGuideIds.ForgeCommitInspiration,
                    optionTarget,
                    optionGraphic,
                    visualMode: WeakGuideVisualMode.HoldCharge);
            }
            else
            {
                guideService.ClearGuide(this);
            }
            return;
        }

        if (CanMeditate())
        {
            if (!guideService.IsCompleted(WeakGuideIds.ForgeMeditate)
                && TryGetButtonGuideTarget(confirmButton, out RectTransform confirmTarget, out Graphic confirmGraphic))
            {
                guideService.ShowGuide(
                    this,
                    WeakGuideIds.ForgeMeditate,
                    confirmTarget,
                    confirmGraphic);
            }
            else
            {
                guideService.ClearGuide(this);
            }
            return;
        }

        if (materialInput != null
            && !materialInput.AllSlotsFilled
            && materialInput.IsSlotBarVisible
            && !guideService.IsCompleted(WeakGuideIds.ForgeMaterialSlot)
            && materialInput.TryGetFirstMaterialSlotGuideTarget(out Button slotButton)
            && TryGetButtonGuideTarget(
                slotButton,
                out RectTransform slotTarget,
                out Graphic slotGraphic))
        {
            guideService.ShowGuide(
                this,
                WeakGuideIds.ForgeMaterialSlot,
                slotTarget,
                slotGraphic);
            return;
        }

        if (materialInput != null
            && !materialInput.AllSlotsFilled
            && !guideService.IsCompleted(WeakGuideIds.ForgeMaterialEntry)
            && TryGetButtonGuideTarget(
                materialInput.materialSlotBarToggleButton,
                out RectTransform materialTarget,
                out Graphic materialGraphic))
        {
            guideService.ShowGuide(
                this,
                WeakGuideIds.ForgeMaterialEntry,
                materialTarget,
                materialGraphic);
            return;
        }

        guideService.ClearGuide(this);
    }

    private bool TryGetPreferredInspirationGuideTarget(
        out RectTransform target,
        out Graphic graphic)
    {
        target = null;
        graphic = null;

        ForgeInspiration inspiration = GetPreferredPendingInspiration();
        GameObject optionObject = FindOptionObject(inspiration);
        if (optionObject == null) return false;

        ForgeOptionButton optionButton = optionObject.GetComponent<ForgeOptionButton>();
        if (optionButton != null && optionButton.attachButton != null)
            return TryGetButtonGuideTarget(optionButton.attachButton, out target, out graphic);

        if (optionButton != null && optionButton.iconImage != null)
        {
            target = optionButton.iconImage.rectTransform;
            graphic = optionButton.iconImage;
            return true;
        }

        target = optionObject.transform as RectTransform;
        graphic = optionObject.GetComponent<Graphic>();
        return target != null;
    }

    private ForgeInspiration GetPreferredPendingInspiration()
    {
        ForgeSession session = ForgeManager.Instance != null ? ForgeManager.Instance.CurrentSession : null;
        if (session?.generatedInspirations == null || session.targetDice != _selectedDice)
            return null;

        for (int i = session.generatedInspirations.Count - 1; i >= 0; i--)
        {
            ForgeInspiration inspiration = session.generatedInspirations[i];
            if (IsCurrentSessionInspiration(inspiration))
                return inspiration;
        }

        foreach (ForgeInspiration inspiration in session.generatedInspirations)
        {
            if (IsCurrentSessionInspiration(inspiration))
                return inspiration;
        }

        return null;
    }

    private static bool TryGetButtonGuideTarget(
        Button button,
        out RectTransform target,
        out Graphic graphic)
    {
        target = null;
        graphic = null;
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
            return false;

        target = button.transform as RectTransform;
        graphic = button.targetGraphic;
        if (graphic == null)
            graphic = button.GetComponent<Graphic>();
        return target != null;
    }

    /// <summary>
    /// 判断当前是否满足点击冥想按钮的条件。
    /// </summary>
    /// <returns>目标骰子存在、材料槽放满且启迪未超上限时返回 true。</returns>
    private bool CanMeditate()
    {
        if (_selectedDice == null || ForgeManager.Instance == null || materialInput == null) return false;
        if (!materialInput.AllSlotsFilled) return false;
        if (HasPendingOptions() && !ForgeManager.Instance.CanForgeMore) return false;
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
    /// 根据持久启迪记录查找当前界面中的启迪按钮对象。
    /// </summary>
    /// <param name="inspiration">要查找的启迪记录。</param>
    /// <returns>匹配的启迪按钮对象；未找到时返回 null。</returns>
    private GameObject FindOptionObject(ForgeInspiration inspiration)
    {
        return inspirationPanel != null ? inspirationPanel.FindOptionObject(inspiration) : null;
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
    /// 播放刻印确认动画，并在结束后执行回调。
    /// </summary>
    /// <param name="optionObject">被刻印的启迪按钮对象。</param>
    /// <param name="onComplete">动画完成后的回调。</param>
    private void PlayAffixCommitAnimation(GameObject optionObject, Action onComplete)
    {
        ForgeUIEffects.PlayAffixCommit(optionObject, affixCommitDuration, onComplete);
    }

}
