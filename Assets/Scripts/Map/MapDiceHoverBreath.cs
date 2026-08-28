using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(DiceViewMonitor))]
public sealed class MapDiceHoverBreath : MonoBehaviour
{
    public MapInteractionManager interactionManager;

    [Header("Breath")]
    [Range(0f, 0.2f)] public float scaleAmount = 0.04f;
    [Min(0.1f)] public float breathPeriod = 1.1f;
    [Min(0.01f)] public float responseDuration = 0.16f;

    private DiceViewMonitor _monitor;
    private RectTransform _rect;
    private Vector3 _baseScale;
    private float _hoverWeight;
    private float _phase;
    private bool _initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Update()
    {
        if (!_initialized) return;

        bool hovering = IsHoveringMapDice();
        float responseStep = Time.unscaledDeltaTime / Mathf.Max(0.01f, responseDuration);
        _hoverWeight = Mathf.MoveTowards(_hoverWeight, hovering ? 1f : 0f, responseStep);

        if (hovering || _hoverWeight > 0f)
            _phase += Time.unscaledDeltaTime * Mathf.PI * 2f / Mathf.Max(0.1f, breathPeriod);

        float wave = 0.5f - 0.5f * Mathf.Cos(_phase);
        float scale = 1f + scaleAmount * wave * _hoverWeight;
        _rect.localScale = _baseScale * scale;

        if (!hovering && _hoverWeight <= 0f)
            _phase = 0f;
    }

    private void OnDisable()
    {
        RestoreBaseScale();
        _hoverWeight = 0f;
        _phase = 0f;
    }

    private void Initialize()
    {
        if (_initialized) return;

        _monitor = GetComponent<DiceViewMonitor>();
        _rect = transform as RectTransform;
        if (_rect == null) return;

        _baseScale = _rect.localScale;
        _initialized = true;
    }

    private bool IsHoveringMapDice()
    {
        return IsMapDiceAtScreenPoint(Input.mousePosition);
    }

    public bool IsMapDiceAtScreenPoint(Vector2 screenPosition)
    {
        if (interactionManager == null
            || !interactionManager.IsMapDiceStageActive
            || interactionManager.MapDice == null
            || interactionManager.MapDice.isRolling
            || _monitor == null)
        {
            return false;
        }

        return _monitor.GetDiceUnderScreenPoint(screenPosition) == interactionManager.MapDice;
    }

    private void RestoreBaseScale()
    {
        if (_rect != null)
            _rect.localScale = _baseScale;
    }
}
