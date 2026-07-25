using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 将骰子相机中的 3D 骰子投影为 RawImage 子级 UI 目标。
/// 引导视觉只作用于投影框，不修改骰子模型、碰撞体或物理尺寸。
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectedDiceWeakGuide : MonoBehaviour
{
    [Min(0f)] public float padding = 18f;
    [Min(1f)] public float minimumSize = 72f;

    private DiceViewMonitor _monitor;
    private PhysicsDice _dice;
    private RectTransform _overlayRect;
    private Image _overlayImage;
    public PhysicsDice Dice => _dice;
    public bool IsAvailable => _dice != null
        && _monitor != null
        && _monitor.rectTrans != null
        && _dice.gameObject.activeInHierarchy;

    public void Bind(DiceViewMonitor monitor, PhysicsDice dice)
    {
        _monitor = monitor;
        _dice = dice;
        EnsureOverlay();
        UpdateProjection();
    }

    public bool Show(UnityEngine.Object owner, string guideId)
    {
        if (!IsAvailable || WeakGuideService.Instance == null)
            return false;

        EnsureOverlay();
        _overlayRect.gameObject.SetActive(true);
        UpdateProjection();
        return WeakGuideService.Instance.ShowGuide(
            owner,
            guideId,
            _overlayRect,
            _overlayImage);
    }

    public Vector3 GetArrowStartWorldPosition()
    {
        return IsAvailable
            ? _monitor.GetWorldPositionFromDice(_dice.transform.position)
            : Vector3.zero;
    }

    public void Hide()
    {
        _dice = null;
        if (_overlayRect != null)
            _overlayRect.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (IsAvailable)
        {
            UpdateProjection();
            return;
        }

        if (_overlayRect != null)
            _overlayRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_overlayRect != null)
            Destroy(_overlayRect.gameObject);
    }

    private void EnsureOverlay()
    {
        if (_monitor == null || _monitor.rectTrans == null)
            return;

        if (_overlayRect == null)
        {
            GameObject overlay = new GameObject(
                $"{name}_DiceGuideProjection",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _overlayRect = overlay.GetComponent<RectTransform>();
            _overlayImage = overlay.GetComponent<Image>();
            _overlayImage.sprite = WeakGuideEffect.GetHaloFrameSprite();
            _overlayImage.type = Image.Type.Sliced;
            _overlayImage.color = new Color(1f, 0.96f, 0.8f, 0.52f);
            _overlayImage.raycastTarget = false;
        }

        if (_overlayRect.parent != _monitor.rectTrans)
            _overlayRect.SetParent(_monitor.rectTrans, false);

        _overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        _overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        _overlayRect.pivot = new Vector2(0.5f, 0.5f);
        _overlayRect.SetAsLastSibling();
    }

    private void UpdateProjection()
    {
        if (!IsAvailable || _overlayRect == null)
            return;

        Bounds bounds = GetVisualBounds(_dice);
        Vector3 centerViewport = _monitor.diceCamera.WorldToViewportPoint(bounds.center);

        Vector3 minViewport = new Vector3(float.MaxValue, float.MaxValue, 0f);
        Vector3 maxViewport = new Vector3(float.MinValue, float.MinValue, 0f);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 viewport = _monitor.diceCamera.WorldToViewportPoint(corner);
                    minViewport = Vector3.Min(minViewport, viewport);
                    maxViewport = Vector3.Max(maxViewport, viewport);
                }
            }
        }

        Rect parentRect = _monitor.rectTrans.rect;
        _overlayRect.anchoredPosition = new Vector2(
            (centerViewport.x - 0.5f) * parentRect.width,
            (centerViewport.y - 0.5f) * parentRect.height);
        _overlayRect.sizeDelta = new Vector2(
            Mathf.Max(minimumSize, (maxViewport.x - minViewport.x) * parentRect.width + padding * 2f),
            Mathf.Max(minimumSize, (maxViewport.y - minViewport.y) * parentRect.height + padding * 2f));
    }

    private static Bounds GetVisualBounds(PhysicsDice dice)
    {
        Renderer[] renderers = dice.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(dice.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

}
