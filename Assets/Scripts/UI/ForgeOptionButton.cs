using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 冥想界面中的启迪按钮。
/// 负责展示启迪图标、tooltip、长按刻印入口，以及已废弃启迪的变暗状态。
/// </summary>
public class ForgeOptionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image iconImage;
    public Button attachButton;
    private ForgeAffixSO _affix;
    private ForgeInspiration _inspiration;
    private bool _commitInteractable = true;

    public ForgeAffixSO Affix => _affix;
    public ForgeInspiration Inspiration => _inspiration;

    /// <summary>
    /// 使用单个词条配置初始化按钮，主要用于旧数据或兼容显示。
    /// </summary>
    /// <param name="affix">该按钮展示的词条配置。</param>
    /// <param name="showAttach">是否显示/启用刻印入口。</param>
    public void Setup(ForgeAffixSO affix, bool showAttach)
    {
        _affix = affix;
        _inspiration = null;
        if (iconImage != null && affix.icon != null) iconImage.sprite = affix.icon;
        if (attachButton != null && attachButton.gameObject != gameObject)
            attachButton.gameObject.SetActive(showAttach);
        SetCommitInteractable(showAttach);
    }

    /// <summary>
    /// 使用持久启迪记录初始化按钮。
    /// </summary>
    /// <param name="inspiration">该按钮对应的启迪记录。</param>
    /// <param name="showAttach">是否显示/启用刻印入口。</param>
    public void Setup(ForgeInspiration inspiration, bool showAttach)
    {
        _inspiration = inspiration;
        _affix = inspiration != null ? inspiration.affix : null;
        if (iconImage != null && _affix != null && _affix.icon != null) iconImage.sprite = _affix.icon;
        if (attachButton != null && attachButton.gameObject != gameObject)
            attachButton.gameObject.SetActive(showAttach);
        SetCommitInteractable(showAttach);
    }

    /// <summary>
    /// 设置按钮是否以变暗状态显示，用于未被刻印且不再可选的启迪。
    /// </summary>
    /// <param name="dimmed">为 true 时降低整体透明度。</param>
    public void SetDimmed(bool dimmed)
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = dimmed ? 0.42f : 1f;
    }

    /// <summary>
    /// 控制该启迪是否可通过长按触发刻印。
    /// </summary>
    /// <param name="canCommit">为 true 时允许长按刻印。</param>
    public void SetCommitInteractable(bool canCommit)
    {
        _commitInteractable = canCommit;
        if (attachButton != null)
            attachButton.interactable = canCommit;
    }

    /// <summary>
    /// 鼠标悬停时显示启迪词条提示。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_affix == null || TooltipSystem.Instance == null) return;

        string content = $"<color=#888888>标签: {_affix.tag}  |  品质: T{_affix.tier} / Q{_affix.quality}</color>";
        if (!string.IsNullOrEmpty(_affix.description))
            content += $"\n\n{_affix.description}";

        TooltipSystem.Instance.Show(content, _affix.affixName);
    }

    /// <summary>
    /// 鼠标离开时隐藏启迪提示。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    /// <summary>
    /// 点击启迪时隐藏提示；刻印由长按流程处理。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        TooltipSystem.Instance?.Hide();
    }

    /// <summary>
    /// 按下启迪按钮时启动长按刻印流程。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_affix == null || !_commitInteractable) return;
        if (_inspiration != null)
            ForgeUIManager.Instance?.OnOptionPressStart(_inspiration, transform as RectTransform);
        else
            ForgeUIManager.Instance?.OnOptionPressStart(_affix, transform as RectTransform);
    }

    /// <summary>
    /// 松开启迪按钮时通知锻造 UI 结束长按。
    /// </summary>
    /// <param name="eventData">Unity UI 指针事件数据。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        ForgeUIManager.Instance?.OnOptionPressEnd();
    }
}
