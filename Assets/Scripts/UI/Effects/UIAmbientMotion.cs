using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIAmbientMotion : MonoBehaviour
{
    public enum MovementMode
    {
        None,
        Radius,
        SafeOverflow
    }

    [Header("Breath")]
    public bool breathe;
    [Range(0f, 0.25f)] public float breathAmount = 0.03f;
    [Min(0.1f)] public float breathDuration = 2f;
    [Range(0f, 1f)] public float breathStartPhase = 0.5f;

    [Header("Movement")]
    public MovementMode movementMode;
    public Vector2 movementDirection = Vector2.right;
    [Min(0f)] public float movementRadius = 8f;
    [Min(0.1f)] public float movementDuration = 8f;
    [Range(0f, 1f)] public float movementStartPhase = 0.5f;
    public bool reverseMovement;

    private RectTransform _rect;
    private Vector2 _basePosition;
    private Vector3 _baseScale;
    private float _elapsed;

    private void OnEnable()
    {
        _rect = transform as RectTransform;
        _basePosition = _rect.anchoredPosition;
        _baseScale = _rect.localScale;
        _elapsed = 0f;
        ApplyMotion();
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        ApplyMotion();
    }

    private void OnDisable()
    {
        if (_rect == null) return;
        _rect.anchoredPosition = _basePosition;
        _rect.localScale = _baseScale;
    }

    private void ApplyMotion()
    {
        if (_rect == null) return;

        if (breathe)
        {
            float phase = EvaluatePhase(_elapsed, breathDuration, breathStartPhase, false);
            float scale = Mathf.Lerp(1f - breathAmount, 1f + breathAmount, phase);
            _rect.localScale = _baseScale * scale;
        }
        else
        {
            _rect.localScale = _baseScale;
        }

        if (movementMode == MovementMode.None)
        {
            _rect.anchoredPosition = _basePosition;
            return;
        }

        Vector2 travel = movementMode == MovementMode.SafeOverflow
            ? CalculateSafeTravel()
            : movementDirection.normalized * movementRadius;
        float movementPhase = EvaluatePhase(_elapsed, movementDuration, movementStartPhase, reverseMovement);
        _rect.anchoredPosition = _basePosition + Vector2.Lerp(-travel, travel, movementPhase);
    }

    private Vector2 CalculateSafeTravel()
    {
        if (_rect.parent is not RectTransform parent) return Vector2.zero;

        Vector2 direction = movementDirection.normalized;
        if (direction.sqrMagnitude < 0.0001f) return Vector2.zero;

        float minimumBreathScale = breathe ? Mathf.Max(0.01f, 1f - breathAmount) : 1f;
        Vector2 visualSize = new Vector2(
            _rect.rect.width * Mathf.Abs(_baseScale.x) * minimumBreathScale,
            _rect.rect.height * Mathf.Abs(_baseScale.y) * minimumBreathScale);
        Vector2 available = new Vector2(
            Mathf.Max(0f, (visualSize.x - parent.rect.width) * 0.5f),
            Mathf.Max(0f, (visualSize.y - parent.rect.height) * 0.5f));

        float distance = float.PositiveInfinity;
        if (Mathf.Abs(direction.x) > 0.0001f)
            distance = Mathf.Min(distance, available.x / Mathf.Abs(direction.x));
        if (Mathf.Abs(direction.y) > 0.0001f)
            distance = Mathf.Min(distance, available.y / Mathf.Abs(direction.y));

        return float.IsInfinity(distance) ? Vector2.zero : direction * distance;
    }

    private static float EvaluatePhase(float elapsed, float duration, float startPhase, bool reverse)
    {
        float phase = Mathf.PingPong(elapsed / Mathf.Max(0.1f, duration) + Mathf.Clamp01(startPhase), 1f);
        phase = Mathf.SmoothStep(0f, 1f, phase);
        return reverse ? 1f - phase : phase;
    }
}
