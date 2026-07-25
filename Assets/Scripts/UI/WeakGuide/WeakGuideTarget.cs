using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 普通界面的 Inspector 弱引导配置项。
/// 简单按钮可以在点击时自动完成；复杂业务应关闭自动完成并在成功后调用 Complete。
/// </summary>
public sealed class WeakGuideTarget : MonoBehaviour
{
    [Tooltip("稳定引导 ID。修改它会让该引导对所有玩家重新触发。")]
    public string guideId;

    [Tooltip("播放缩放呼吸的目标。为空时使用当前物体。")]
    public RectTransform scaleTarget;

    [Tooltip("添加外发光的 UI Graphic。为空时优先使用 Button.targetGraphic。")]
    public Graphic glowGraphic;

    [Tooltip("普通按钮可启用；需要等待业务成功的引导应关闭并手动调用 Complete。")]
    public bool completeOnButtonClick = true;

    public Button button;

    private WeakGuideScreen _screen;

    public bool IsAvailable
    {
        get
        {
            RectTransform target = ResolveScaleTarget();
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            return button == null || button.interactable;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindButton();
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    public bool Show(UnityEngine.Object owner)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(guideId)) return false;

        return WeakGuideService.Instance != null
            && WeakGuideService.Instance.ShowGuide(
                owner,
                guideId,
                ResolveScaleTarget(),
                ResolveGlowGraphic());
    }

    public void Complete()
    {
        if (WeakGuideService.Instance == null || string.IsNullOrWhiteSpace(guideId))
            return;

        WeakGuideService.Instance.CompleteGuide(guideId);
        _screen?.RefreshGuide();
    }

    private void HandleButtonClicked()
    {
        if (completeOnButtonClick)
            Complete();
    }

    private void ResolveReferences()
    {
        if (scaleTarget == null)
            scaleTarget = transform as RectTransform;
        if (button == null)
            button = GetComponent<Button>();
        if (_screen == null)
            _screen = GetComponentInParent<WeakGuideScreen>(true);
    }

    private RectTransform ResolveScaleTarget()
    {
        if (scaleTarget == null)
            scaleTarget = transform as RectTransform;
        return scaleTarget;
    }

    private Graphic ResolveGlowGraphic()
    {
        if (glowGraphic != null) return glowGraphic;
        if (button != null && button.targetGraphic != null)
            return button.targetGraphic;
        return ResolveScaleTarget() != null ? ResolveScaleTarget().GetComponent<Graphic>() : null;
    }

    private void BindButton()
    {
        if (button == null) return;
        button.onClick.RemoveListener(HandleButtonClicked);
        button.onClick.AddListener(HandleButtonClicked);
    }

    private void UnbindButton()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleButtonClicked);
    }
}
