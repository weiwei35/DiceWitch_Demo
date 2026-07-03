using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JuicyButtonEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
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
    private Vector3 _baseScale;
    private Quaternion _baseRotation;
    private bool _isHovering;
    private float _hoverRotationSign = 1f;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureBasePose();
    }

    private void OnDisable()
    {
        KillTweens();
        if (_rect == null) return;

        _rect.localScale = _baseScale;
        _rect.localRotation = _baseRotation;
        _isHovering = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanAnimate()) return;

        CaptureBasePose();
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
        _rect.DOScale(_baseScale * pressScale, pressDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this);
        _rect.DOLocalRotateQuaternion(_baseRotation, pressDuration)
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
        _rect.localScale = _isHovering ? GetHoverScale() : _baseScale;
        _rect.DOPunchScale(_baseScale * clickPunchScale, clickPunchDuration, 1, 0.55f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() =>
            {
                if (this == null || _rect == null || !isActiveAndEnabled) return;
                if (_isHovering) PlayHover();
                else PlayNormal();
            });
    }

    private void PlayHover()
    {
        KillTweens();

        _rect.DOScale(GetHoverScale(), hoverDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetTarget(this);

        _rect.DOLocalRotateQuaternion(GetHoverRotation(), hoverDuration)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private void PlayNormal()
    {
        KillTweens();

        _rect.DOScale(_baseScale, hoverDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this);

        _rect.DOLocalRotateQuaternion(_baseRotation, hoverDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true)
            .SetTarget(this);
    }

    private Vector3 GetHoverScale()
    {
        return _baseScale * Mathf.Max(0.01f, hoverScale);
    }

    private Quaternion GetHoverRotation()
    {
        float z = hoverRotation * _hoverRotationSign;
        return _baseRotation * Quaternion.Euler(0f, 0f, z);
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
        if (_rect == null)
            _rect = transform as RectTransform;
        if (_button == null)
            _button = GetComponent<Button>();
    }

    private void CaptureBasePose()
    {
        if (_rect == null) return;

        _baseScale = _rect.localScale;
        _baseRotation = _rect.localRotation;
    }

    private void KillTweens()
    {
        DOTween.Kill(this);
    }
}
