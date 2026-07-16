using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JuicyButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Target")]
    public RectTransform effectTarget;

    [Header("Hover")]
    public float hoverScale = 1.08f;
    public float hoverDuration = 0.16f;
    public float hoverRotation = 3.5f;

    [Header("Press")]
    public float pressScale = 0.92f;
    public float pressDuration = 0.08f;

    [Header("Click")]
    public float clickPunchScale = 0.14f;
    public float clickPunchDuration = 0.18f;

    private RectTransform _rect;
    private Button _button;
    private bool _isHovering;
    private float _hoverRotationSign = 1f;
    private float _scaleFactor = 1f;
    private float _punchFactor;
    private float _rotationOffsetZ;
    private float _lastAppliedScaleFactor = 1f;
    private Quaternion _lastAppliedRotationOffset = Quaternion.identity;
    private Vector3 _lastOutputScale;
    private Quaternion _lastOutputRotation = Quaternion.identity;
    private bool _hasAppliedEffect;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        ResetEffectPose();
    }

    private void OnDisable()
    {
        KillTweens();
        if (_rect == null) return;

        RemoveAppliedEffect();
        ResetEffectPose();
        _isHovering = false;
    }

    private void LateUpdate()
    {
        if (!HasTarget()) return;

        RemoveAppliedEffect();

        float combinedScale = Mathf.Max(0.01f, _scaleFactor + _punchFactor);
        if (Mathf.Abs(combinedScale - 1f) < 0.0001f && Mathf.Abs(_rotationOffsetZ) < 0.0001f)
            return;

        _lastAppliedScaleFactor = combinedScale;
        _lastAppliedRotationOffset = Quaternion.Euler(0f, 0f, _rotationOffsetZ);

        _rect.localScale *= _lastAppliedScaleFactor;
        _rect.localRotation *= _lastAppliedRotationOffset;
        _lastOutputScale = _rect.localScale;
        _lastOutputRotation = _rect.localRotation;
        _hasAppliedEffect = true;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanAnimate()) return;

        _isHovering = true;
        _hoverRotationSign = Random.value < 0.5f ? -1f : 1f;
        PlayHover();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!HasTarget()) return;

        _isHovering = false;
        PlayNormal();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAnimate()) return;

        KillTweens();
        DOTween.To(() => _scaleFactor, value => _scaleFactor = value, Mathf.Max(0.01f, pressScale), pressDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this);
        DOTween.To(() => _rotationOffsetZ, value => _rotationOffsetZ = value, 0f, pressDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!HasTarget()) return;

        if (_isHovering)
            PlayHover();
        else
            PlayNormal();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanAnimate()) return;

        DOTween.Kill(this);
        _scaleFactor = _isHovering ? Mathf.Max(0.01f, hoverScale) : 1f;
        _punchFactor = 0f;
        DOTween.To(() => _punchFactor, value => _punchFactor = value, clickPunchScale, clickPunchDuration * 0.5f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this)
            .SetLoops(2, LoopType.Yoyo)
            .OnComplete(() =>
            {
                if (this == null || _rect == null || !isActiveAndEnabled) return;
                _punchFactor = 0f;
                if (_isHovering) PlayHover();
                else PlayNormal();
            });
    }

    private void PlayHover()
    {
        KillTweens();

        DOTween.To(() => _scaleFactor, value => _scaleFactor = value, Mathf.Max(0.01f, hoverScale), hoverDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetTarget(this);

        DOTween.To(() => _rotationOffsetZ, value => _rotationOffsetZ = value, GetHoverRotationZ(), hoverDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void PlayNormal()
    {
        KillTweens();

        DOTween.To(() => _scaleFactor, value => _scaleFactor = value, 1f, hoverDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this);

        DOTween.To(() => _rotationOffsetZ, value => _rotationOffsetZ = value, 0f, hoverDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private float GetHoverRotationZ()
    {
        return hoverRotation * _hoverRotationSign;
    }

    private bool CanAnimate()
    {
        CacheReferences();
        return _rect != null && _button != null && _button.interactable && isActiveAndEnabled;
    }

    private bool HasTarget()
    {
        CacheReferences();
        return _rect != null && _button != null && isActiveAndEnabled;
    }

    private void CacheReferences()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        RectTransform target = ResolveEffectTarget();
        if (_rect != target)
        {
            _rect = target;
            ResetEffectPose();
        }
    }

    private void RemoveAppliedEffect()
    {
        if (_rect == null) return;
        if (!_hasAppliedEffect) return;

        if (IsSameScale(_rect.localScale, _lastOutputScale))
        {
            float safeScale = Mathf.Max(0.01f, _lastAppliedScaleFactor);
            _rect.localScale /= safeScale;
        }

        if (IsSameRotation(_rect.localRotation, _lastOutputRotation))
            _rect.localRotation *= Quaternion.Inverse(_lastAppliedRotationOffset);

        _hasAppliedEffect = false;
    }

    private void KillTweens()
    {
        DOTween.Kill(this);
    }

    private RectTransform ResolveEffectTarget()
    {
        if (effectTarget != null)
            return effectTarget;

        if (_button != null
            && _button.targetGraphic != null
            && _button.targetGraphic.transform != transform)
            return _button.targetGraphic.rectTransform;

        return transform as RectTransform;
    }

    private void ResetEffectPose()
    {
        _scaleFactor = 1f;
        _punchFactor = 0f;
        _rotationOffsetZ = 0f;
        _lastAppliedScaleFactor = 1f;
        _lastAppliedRotationOffset = Quaternion.identity;
        _lastOutputScale = Vector3.one;
        _lastOutputRotation = Quaternion.identity;
        _hasAppliedEffect = false;
    }

    private bool IsSameScale(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < 0.000001f;
    }

    private bool IsSameRotation(Quaternion a, Quaternion b)
    {
        return Mathf.Abs(Quaternion.Dot(a, b)) > 0.99999f;
    }
}
