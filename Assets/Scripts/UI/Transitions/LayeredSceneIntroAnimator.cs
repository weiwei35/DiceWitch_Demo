using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LayeredSceneIntroAnimator : MonoBehaviour
{
    public enum SlideDirection
    {
        FromLeft,
        FromRight,
        FromTop,
        FromBottom,
        FadeOnly
    }

    [System.Serializable]
    public class LayerElement
    {
        public RectTransform image;
        public SlideDirection direction = SlideDirection.FromLeft;
        public float slideDistance = 120f;
    }

    [System.Serializable]
    public class IntroLayer
    {
        public string layerName;
        [Tooltip("同一层内的多个元素会同时播放，但每个元素可以单独设置滑入方向和距离。")]
        public List<LayerElement> elements = new List<LayerElement>();
        public float duration = 0.55f;
        public Ease ease = Ease.OutCubic;
        public bool fade = true;
        public bool popScale = false;
        public float hiddenScale = 0.92f;
    }

    [Header("Playback")]
    public bool playOnEnable = true;
    public bool resetBeforePlay = true;
    public bool restoreOnDisable = false;

    [Header("Timing")]
    public float startDelay = 0f;
    public float layerInterval = 0.12f;

    [Header("Default Layer Settings")]
    public SlideDirection defaultDirection = SlideDirection.FromLeft;
    public float defaultSlideDistance = 120f;
    public float defaultDuration = 0.55f;
    public Ease defaultEase = Ease.OutCubic;

    [Header("Layers")]
    public List<IntroLayer> layers = new List<IntroLayer>();

    private readonly Dictionary<RectTransform, Vector2> _basePositions = new Dictionary<RectTransform, Vector2>();
    private readonly Dictionary<RectTransform, Vector3> _baseScales = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, CanvasGroup> _canvasGroups = new Dictionary<RectTransform, CanvasGroup>();
    private Sequence _sequence;

    private void Awake()
    {
        CaptureBaseState();
    }

    private void OnEnable()
    {
        CaptureBaseState();
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        KillSequence();

        if (restoreOnDisable)
            ShowInstant();
    }

    [ContextMenu("Play Intro")]
    public void Play()
    {
        CaptureBaseState();
        KillSequence();

        if (resetBeforePlay)
            ResetToHidden();

        _sequence = DOTween.Sequence().SetTarget(this);
        _sequence.AppendInterval(Mathf.Max(0f, startDelay));

        for (int i = 0; i < layers.Count; i++)
        {
            IntroLayer layer = layers[i];
            if (layer == null) continue;

            float insertTime = startDelay + i * Mathf.Max(0f, layerInterval);
            AddLayerTween(_sequence, layer, insertTime);
        }
    }

    [ContextMenu("Reset To Hidden")]
    public void ResetToHidden()
    {
        CaptureBaseState();
        KillTweensOnImages();

        foreach (IntroLayer layer in layers)
        {
            if (layer == null) continue;

            foreach (LayerElement element in GetLayerElements(layer))
            {
                RectTransform image = element.image;
                if (image == null || !_basePositions.ContainsKey(image)) continue;

                image.anchoredPosition = _basePositions[image] + GetOffset(element);
                image.localScale = GetHiddenScale(layer, image);

                CanvasGroup canvasGroup = GetCanvasGroup(image);
                if (canvasGroup != null)
                    canvasGroup.alpha = layer.fade ? 0f : 1f;
            }
        }
    }

    [ContextMenu("Show Instant")]
    public void ShowInstant()
    {
        CaptureBaseState();
        KillTweensOnImages();

        foreach (IntroLayer layer in layers)
        {
            if (layer == null) continue;

            foreach (LayerElement element in GetLayerElements(layer))
            {
                RectTransform image = element.image;
                if (image == null || !_basePositions.ContainsKey(image)) continue;

                image.anchoredPosition = _basePositions[image];
                image.localScale = _baseScales[image];

                CanvasGroup canvasGroup = GetCanvasGroup(image);
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;
            }
        }
    }

    [ContextMenu("Apply Defaults To Empty Layers")]
    public void ApplyDefaultsToLayers()
    {
        foreach (IntroLayer layer in layers)
        {
            if (layer == null) continue;
            layer.duration = defaultDuration;
            layer.ease = defaultEase;

            foreach (LayerElement element in layer.elements)
            {
                if (element == null) continue;

                element.direction = defaultDirection;
                element.slideDistance = defaultSlideDistance;
            }
        }
    }

    private void AddLayerTween(Sequence sequence, IntroLayer layer, float insertTime)
    {
        if (sequence == null || layer == null) return;

        foreach (LayerElement element in GetLayerElements(layer))
        {
            RectTransform image = element.image;
            if (image == null || !_basePositions.ContainsKey(image)) continue;

            image.anchoredPosition = _basePositions[image] + GetOffset(element);
            image.localScale = GetHiddenScale(layer, image);

            CanvasGroup canvasGroup = GetCanvasGroup(image);
            if (canvasGroup != null && layer.fade)
                canvasGroup.alpha = 0f;

            float duration = Mathf.Max(0.01f, layer.duration);
            sequence.Insert(insertTime, image.DOAnchorPos(_basePositions[image], duration).SetEase(layer.ease));
            sequence.Insert(insertTime, image.DOScale(_baseScales[image], duration).SetEase(layer.popScale ? Ease.OutBack : layer.ease));

            if (canvasGroup != null && layer.fade)
                sequence.Insert(insertTime, canvasGroup.DOFade(1f, duration * 0.75f).SetEase(Ease.OutQuad));
        }
    }

    private IEnumerable<LayerElement> GetLayerElements(IntroLayer layer)
    {
        if (layer == null || layer.elements == null) yield break;

        foreach (LayerElement element in layer.elements)
        {
            if (element != null && element.image != null)
                yield return element;
        }
    }

    private Vector2 GetOffset(LayerElement element)
    {
        if (element == null) return Vector2.zero;

        float distance = Mathf.Max(0f, element.slideDistance);

        switch (element.direction)
        {
            case SlideDirection.FromLeft:
                return Vector2.left * distance;
            case SlideDirection.FromRight:
                return Vector2.right * distance;
            case SlideDirection.FromTop:
                return Vector2.up * distance;
            case SlideDirection.FromBottom:
                return Vector2.down * distance;
            case SlideDirection.FadeOnly:
            default:
                return Vector2.zero;
        }
    }

    private Vector3 GetHiddenScale(IntroLayer layer, RectTransform image)
    {
        if (!layer.popScale || image == null || !_baseScales.ContainsKey(image))
            return image != null && _baseScales.ContainsKey(image) ? _baseScales[image] : Vector3.one;

        return _baseScales[image] * Mathf.Max(0.01f, layer.hiddenScale);
    }

    private void CaptureBaseState()
    {
        foreach (IntroLayer layer in layers)
        {
            if (layer == null) continue;

            foreach (LayerElement element in GetLayerElements(layer))
            {
                RectTransform image = element.image;
                if (image == null) continue;

                if (!_basePositions.ContainsKey(image))
                    _basePositions[image] = image.anchoredPosition;

                if (!_baseScales.ContainsKey(image))
                    _baseScales[image] = image.localScale;

                GetCanvasGroup(image);
            }
        }
    }

    private CanvasGroup GetCanvasGroup(RectTransform image)
    {
        if (image == null) return null;
        if (_canvasGroups.TryGetValue(image, out CanvasGroup cachedGroup) && cachedGroup != null)
        {
            ApplyRaycastPolicy(image, cachedGroup);
            return cachedGroup;
        }

        CanvasGroup canvasGroup = image.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = image.gameObject.AddComponent<CanvasGroup>();

        _canvasGroups[image] = canvasGroup;
        ApplyRaycastPolicy(image, canvasGroup);
        return canvasGroup;
    }

    private void ApplyRaycastPolicy(RectTransform target, CanvasGroup canvasGroup)
    {
        if (target == null || canvasGroup == null) return;

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (ContainsInteractiveUI(target))
            return;

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic != null)
                graphic.raycastTarget = false;
        }
    }

    private bool ContainsInteractiveUI(RectTransform target)
    {
        if (target == null) return false;
        if (target.GetComponentInChildren<Selectable>(true) != null) return true;

        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null) continue;
            if (behaviour is IPointerClickHandler ||
                behaviour is IPointerEnterHandler ||
                behaviour is IPointerExitHandler ||
                behaviour is IPointerDownHandler ||
                behaviour is IPointerUpHandler ||
                behaviour is IBeginDragHandler ||
                behaviour is IDragHandler ||
                behaviour is IEndDragHandler)
            {
                return true;
            }
        }

        return false;
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

    private void KillTweensOnImages()
    {
        KillSequence();

        foreach (IntroLayer layer in layers)
        {
            if (layer == null) continue;

            foreach (LayerElement element in GetLayerElements(layer))
            {
                RectTransform image = element.image;
                if (image == null) continue;

                image.DOKill();
                CanvasGroup canvasGroup = GetCanvasGroup(image);
                if (canvasGroup != null)
                    canvasGroup.DOKill();
            }
        }
    }
}
