using DG.Tweening;
using UnityEngine;

/// <summary>
/// 通用 UI 弹窗动效组件。
/// 挂在弹窗根物体上后，业务代码可调用 Show/Hide 播放出现和关闭动画。
/// </summary>
public class PopupAnimatorUI : MonoBehaviour
{
    public enum PopupMotion
    {
        ScaleFade,
        DropFade,
        SlideFade
    }

    [Header("Motion")]
    public PopupMotion motion = PopupMotion.ScaleFade;
    public float showDuration = 0.22f;
    public float hideDuration = 0.14f;
    public Ease showEase = Ease.OutBack;
    public Ease hideEase = Ease.InBack;

    [Header("Start State")]
    public float hiddenScale = 0.86f;
    public Vector2 hiddenOffset = new Vector2(0f, -28f);

    [Header("Overshoot")]
    public bool useShowOvershoot = true;
    [Range(0f, 0.35f)] public float scaleOvershoot = 0.08f;
    [Range(0f, 0.6f)] public float positionOvershoot = 0.18f;
    [Range(0.35f, 0.85f)] public float overshootPhase = 0.68f;

    [Header("Options")]
    public bool useUnscaledTime = true;
    public bool hideOnAwake = false;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Vector2 _basePosition;
    private Vector3 _baseScale;
    private bool _hasBaseState;
    private Sequence _sequence;

    private void Awake()
    {
        CacheReferences();
        CaptureBaseState();

        if (hideOnAwake)
            HideImmediate();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureBaseState();
    }

    /// <summary>
    /// 播放弹窗出现动画。
    /// </summary>
    public void Show()
    {
        CacheReferences();
        CaptureBaseState();
        KillSequence();

        gameObject.SetActive(true);
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
        ApplyHiddenState();

        float duration = Mathf.Max(0.01f, showDuration);
        _sequence = DOTween.Sequence().SetTarget(this).SetUpdate(useUnscaledTime);
        _sequence.Join(_canvasGroup.DOFade(1f, duration * 0.75f));

        if (useShowOvershoot)
        {
            float firstPhase = duration * overshootPhase;
            float settlePhase = duration - firstPhase;
            _sequence.Join(_rect.DOScale(GetOvershootScale(), firstPhase).SetEase(Ease.OutCubic));
            _sequence.Join(_rect.DOAnchorPos(GetOvershootPosition(), firstPhase).SetEase(Ease.OutCubic));
            _sequence.Append(_rect.DOScale(_baseScale, settlePhase).SetEase(Ease.OutBack));
            _sequence.Join(_rect.DOAnchorPos(_basePosition, settlePhase).SetEase(Ease.OutBack));
        }
        else
        {
            _sequence.Join(_rect.DOScale(_baseScale, duration).SetEase(showEase));
            _sequence.Join(_rect.DOAnchorPos(_basePosition, duration).SetEase(Ease.OutCubic));
        }
    }

    /// <summary>
    /// 播放弹窗关闭动画，结束后隐藏物体。
    /// </summary>
    public void Hide()
    {
        CacheReferences();
        CaptureBaseState();
        KillSequence();

        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        _sequence = DOTween.Sequence().SetTarget(this).SetUpdate(useUnscaledTime);
        _sequence.Join(_canvasGroup.DOFade(0f, Mathf.Max(0.01f, hideDuration)));
        _sequence.Join(_rect.DOScale(GetHiddenScale(), Mathf.Max(0.01f, hideDuration)).SetEase(hideEase));
        _sequence.Join(_rect.DOAnchorPos(GetHiddenPosition(), Mathf.Max(0.01f, hideDuration)).SetEase(Ease.InCubic));
        _sequence.OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>
    /// 不播放动画，立即显示弹窗。
    /// </summary>
    public void ShowImmediate()
    {
        CacheReferences();
        CaptureBaseState();
        KillSequence();

        gameObject.SetActive(true);
        _rect.anchoredPosition = _basePosition;
        _rect.localScale = _baseScale;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _canvasGroup.interactable = true;
    }

    /// <summary>
    /// 不播放动画，立即隐藏弹窗。
    /// </summary>
    public void HideImmediate()
    {
        CacheReferences();
        CaptureBaseState();
        KillSequence();

        ApplyHiddenState();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
        gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        if (_rect == null)
            _rect = transform as RectTransform;

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CaptureBaseState()
    {
        if (_rect == null || _hasBaseState) return;

        _basePosition = _rect.anchoredPosition;
        _baseScale = _rect.localScale;
        _hasBaseState = true;
    }

    private void ApplyHiddenState()
    {
        _canvasGroup.alpha = 0f;
        _rect.anchoredPosition = GetHiddenPosition();
        _rect.localScale = GetHiddenScale();
    }

    private Vector2 GetHiddenPosition()
    {
        switch (motion)
        {
            case PopupMotion.DropFade:
            case PopupMotion.SlideFade:
                return _basePosition + hiddenOffset;
            case PopupMotion.ScaleFade:
            default:
                return _basePosition;
        }
    }

    private Vector3 GetHiddenScale()
    {
        return motion == PopupMotion.ScaleFade
            ? _baseScale * Mathf.Max(0.01f, hiddenScale)
            : _baseScale;
    }

    private Vector2 GetOvershootPosition()
    {
        Vector2 hiddenPosition = GetHiddenPosition();
        Vector2 travel = _basePosition - hiddenPosition;
        return _basePosition + travel * Mathf.Max(0f, positionOvershoot);
    }

    private Vector3 GetOvershootScale()
    {
        return _baseScale * (1f + Mathf.Max(0f, scaleOvershoot));
    }

    private void KillSequence()
    {
        if (_sequence != null)
        {
            _sequence.Kill();
            _sequence = null;
        }

        DOTween.Kill(this);
    }
}
