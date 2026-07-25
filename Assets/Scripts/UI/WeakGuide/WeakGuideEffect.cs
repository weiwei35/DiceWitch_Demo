using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum WeakGuideVisualMode
{
    Pulse,
    HoldCharge
}

/// <summary>
/// 弱引导的纯视觉组件。
/// 不改变目标图片颜色；点击目标使用呼吸缩放，长按目标使用向内收束的独立光圈。
/// </summary>
public sealed class WeakGuideEffect : MonoBehaviour
{
    private RectTransform _scaleTarget;
    private Graphic _glowGraphic;
    private JuicyButtonEffect _juicyButtonEffect;
    private RectTransform _haloRect;
    private Image _haloImage;
    private Material _haloMaterial;
    private Sequence _sequence;
    private float _fallbackScaleFactor = 1f;
    private float _glowAlpha;
    private float _haloScaleFactor = 1f;
    private Vector3 _fallbackBaseScale = Vector3.one;
    private bool _usesFallbackScale;
    private bool _isPlaying;
    private WeakGuideVisualMode _visualMode;

    private static Sprite _haloFrameSprite;
    private static Shader _haloShader;

    public bool IsPlaying => _isPlaying;

    public static WeakGuideEffect GetOrCreate(
        RectTransform scaleTarget,
        Graphic glowGraphic,
        bool useGraphicAlpha = true,
        WeakGuideVisualMode visualMode = WeakGuideVisualMode.Pulse)
    {
        WeakGuideEffect effect = scaleTarget.GetComponent<WeakGuideEffect>();
        if (effect == null)
            effect = scaleTarget.gameObject.AddComponent<WeakGuideEffect>();

        effect.Configure(scaleTarget, glowGraphic, useGraphicAlpha, visualMode);
        return effect;
    }

    public void Configure(
        RectTransform scaleTarget,
        Graphic glowGraphic,
        bool useGraphicAlpha = true,
        WeakGuideVisualMode visualMode = WeakGuideVisualMode.Pulse)
    {
        if (_scaleTarget != scaleTarget)
        {
            StopGuide(immediate: true);
            _scaleTarget = scaleTarget;
        }

        Graphic resolvedGraphic = glowGraphic;
        if (resolvedGraphic == null)
        {
            Button button = scaleTarget.GetComponent<Button>();
            if (button != null)
                resolvedGraphic = button.targetGraphic;
        }
        if (resolvedGraphic == null)
            resolvedGraphic = scaleTarget.GetComponent<Graphic>();
        _glowGraphic = resolvedGraphic;

        _visualMode = visualMode;
        _juicyButtonEffect = scaleTarget.GetComponent<JuicyButtonEffect>();
    }

    public void PlayGuide(WeakGuideService settings)
    {
        if (settings == null || _scaleTarget == null) return;
        if (_isPlaying) return;

        _isPlaying = true;
        DOTween.Kill(this);
        EnsureHalo(settings);

        _juicyButtonEffect = _scaleTarget.GetComponent<JuicyButtonEffect>();
        _usesFallbackScale = _juicyButtonEffect == null;
        _fallbackScaleFactor = 1f;
        _haloScaleFactor = _visualMode == WeakGuideVisualMode.HoldCharge
            ? settings.holdChargeStartScale
            : 1f;
        _glowAlpha = settings.glowMinAlpha;
        if (_usesFallbackScale)
            _fallbackBaseScale = _scaleTarget.localScale;

        ApplyScaleFactor(1f);
        ApplyHaloVisual(_haloScaleFactor, settings.glowMinAlpha, settings.glowColor);

        _sequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetTarget(this);

        if (_visualMode == WeakGuideVisualMode.HoldCharge)
        {
            _sequence.Append(DOTween.To(
                    () => _haloScaleFactor,
                    value =>
                    {
                        _haloScaleFactor = value;
                        ApplyHaloVisual(_haloScaleFactor, _glowAlpha, settings.glowColor);
                    },
                    1f,
                    settings.holdChargeDuration)
                .SetEase(Ease.InCubic));
            _sequence.Join(DOTween.To(
                    () => _glowAlpha,
                    alpha =>
                    {
                        _glowAlpha = alpha;
                        ApplyHaloVisual(_haloScaleFactor, _glowAlpha, settings.glowColor);
                    },
                    settings.glowMaxAlpha,
                    settings.holdChargeDuration)
                .SetEase(Ease.InQuad));
            _sequence.Append(DOTween.To(
                    () => _glowAlpha,
                    alpha =>
                    {
                        _glowAlpha = alpha;
                        ApplyHaloVisual(_haloScaleFactor, _glowAlpha, settings.glowColor);
                    },
                    0f,
                    settings.holdChargeFadeDuration)
                .SetEase(Ease.OutQuad));
            _sequence.SetLoops(-1, LoopType.Restart);
        }
        else
        {
            _sequence.Append(DOTween.To(
                    () => _fallbackScaleFactor,
                    value =>
                    {
                        _fallbackScaleFactor = value;
                        ApplyScaleFactor(value);
                    },
                    Mathf.Max(1f, settings.pulseScale),
                    settings.pulseDuration * 0.5f)
                .SetEase(Ease.InOutSine));
            _sequence.Join(DOTween.To(
                    () => _glowAlpha,
                    alpha =>
                    {
                        _glowAlpha = alpha;
                        ApplyHaloVisual(1f, _glowAlpha, settings.glowColor);
                    },
                    settings.glowMaxAlpha,
                    settings.pulseDuration * 0.5f)
                .SetEase(Ease.InOutSine));
            _sequence.SetLoops(-1, LoopType.Yoyo);
        }
    }

    public void StopGuide(bool immediate = false)
    {
        if (!_isPlaying && !immediate)
        {
            HideHalo();
            return;
        }

        _isPlaying = false;
        DOTween.Kill(this);
        _sequence = null;

        if (immediate || _scaleTarget == null || !_scaleTarget.gameObject.activeInHierarchy)
        {
            ResetVisuals();
            return;
        }

        float startScale = _fallbackScaleFactor;
        float startHaloScale = _haloScaleFactor;
        Color startColor = _haloImage != null ? _haloImage.color : Color.clear;
        DOTween.To(
                () => 0f,
                progress =>
                {
                    float scale = Mathf.Lerp(startScale, 1f, progress);
                    ApplyScaleFactor(scale);
                    float haloScale = Mathf.Lerp(startHaloScale, 1f, progress);
                    float alpha = Mathf.Lerp(startColor.a, 0f, progress);
                    ApplyHaloVisual(haloScale, alpha, startColor);
                },
                1f,
                0.15f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(ResetVisuals);
    }

    private void OnDisable()
    {
        StopGuide(immediate: true);
    }

    private void LateUpdate()
    {
        if (!_isPlaying || !_usesFallbackScale || _scaleTarget == null) return;

        JuicyButtonEffect addedJuicyEffect = _scaleTarget.GetComponent<JuicyButtonEffect>();
        if (addedJuicyEffect != null)
        {
            _scaleTarget.localScale = _fallbackBaseScale;
            _juicyButtonEffect = addedJuicyEffect;
            _usesFallbackScale = false;
            ApplyScaleFactor(_fallbackScaleFactor);
            return;
        }

        _scaleTarget.localScale = _fallbackBaseScale * _fallbackScaleFactor;
    }

    private void EnsureHalo(WeakGuideService settings)
    {
        if (_scaleTarget == null) return;

        RectTransform haloParent = _glowGraphic != null
            ? _glowGraphic.rectTransform
            : _scaleTarget;
        if (_haloRect == null)
        {
            GameObject haloObject = new GameObject(
                "WeakGuideHalo",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _haloRect = haloObject.GetComponent<RectTransform>();
            _haloImage = haloObject.GetComponent<Image>();
            _haloImage.raycastTarget = false;
        }

        if (_haloRect.parent != haloParent)
            _haloRect.SetParent(haloParent, false);

        Image sourceImage = _glowGraphic as Image;
        bool followsSpriteShape = sourceImage != null && sourceImage.sprite != null;
        if (followsSpriteShape)
            CopyImageShape(sourceImage, _haloImage);
        else
        {
            _haloImage.sprite = GetHaloFrameSprite();
            _haloImage.type = Image.Type.Sliced;
            _haloImage.preserveAspect = false;
            _haloImage.fillCenter = true;
        }
        EnsureHaloMaterial();
        ConfigureHaloMaterial(_haloImage.sprite, haloParent, settings);
        _haloImage.material = _haloMaterial;

        float padding = Mathf.Max(0f, settings.haloPadding);
        _haloRect.anchorMin = Vector2.zero;
        _haloRect.anchorMax = Vector2.one;
        _haloRect.pivot = new Vector2(0.5f, 0.5f);
        _haloRect.offsetMin = new Vector2(-padding, -padding);
        _haloRect.offsetMax = new Vector2(padding, padding);
        _haloRect.SetAsLastSibling();
        _haloRect.gameObject.SetActive(true);
    }

    private static void CopyImageShape(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.overrideSprite = source.overrideSprite;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.useSpriteMesh = source.useSpriteMesh;
    }

    private void ApplyScaleFactor(float factor)
    {
        factor = Mathf.Max(0.01f, factor);
        if (_juicyButtonEffect != null)
        {
            _juicyButtonEffect.SetGuideScaleFactor(factor);
            return;
        }

        if (_scaleTarget != null)
            _scaleTarget.localScale = _fallbackBaseScale * factor;
    }

    private void ApplyHaloVisual(float scale, float alpha, Color baseColor)
    {
        if (_haloRect != null)
            _haloRect.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        if (_haloImage == null) return;

        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);
        _haloImage.color = color;
    }

    private void ResetVisuals()
    {
        if (_juicyButtonEffect != null)
            _juicyButtonEffect.SetGuideScaleFactor(1f);
        else if (_scaleTarget != null && _usesFallbackScale)
            _scaleTarget.localScale = _fallbackBaseScale;

        _fallbackScaleFactor = 1f;
        _haloScaleFactor = 1f;
        _glowAlpha = 0f;
        HideHalo();
    }

    private void HideHalo()
    {
        if (_haloRect != null)
            _haloRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_haloMaterial != null)
            Destroy(_haloMaterial);
    }

    public static Sprite GetHaloFrameSprite()
    {
        if (_haloFrameSprite != null)
            return _haloFrameSprite;

        const int size = 16;
        const int thickness = 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "WeakGuideHaloTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < thickness
                    || x >= size - thickness
                    || y < thickness
                    || y >= size - thickness;
                pixels[y * size + x] = isBorder ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        _haloFrameSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(4f, 4f, 4f, 4f));
        _haloFrameSprite.name = "WeakGuideHaloFrameSprite";
        _haloFrameSprite.hideFlags = HideFlags.HideAndDontSave;
        return _haloFrameSprite;
    }

    private void EnsureHaloMaterial()
    {
        if (_haloMaterial != null)
            return;

        if (_haloShader == null)
            _haloShader = Resources.Load<Shader>("Shaders/UIWeakGuideHalo");
        if (_haloShader == null)
            _haloShader = Shader.Find("DiceWitch/UI/WeakGuideHalo");
        if (_haloShader == null)
            return;

        _haloMaterial = new Material(_haloShader)
        {
            name = "WeakGuideHaloMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void ConfigureHaloMaterial(
        Sprite sprite,
        RectTransform sourceRect,
        WeakGuideService settings)
    {
        if (_haloMaterial == null || sprite == null || sourceRect == null)
            return;

        Vector4 outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        Vector2 uvCenter = new Vector2(
            (outerUv.x + outerUv.z) * 0.5f,
            (outerUv.y + outerUv.w) * 0.5f);

        Rect rect = sourceRect.rect;
        float width = Mathf.Max(1f, rect.width);
        float height = Mathf.Max(1f, rect.height);
        float padding = Mathf.Max(0f, settings.haloPadding);
        Vector2 uvScale = new Vector2(
            (width + padding * 2f) / width,
            (height + padding * 2f) / height);

        _haloMaterial.SetVector("_SpriteUvRect", outerUv);
        _haloMaterial.SetVector("_SpriteUvCenter", uvCenter);
        _haloMaterial.SetVector("_SpriteUvScale", uvScale);
        _haloMaterial.SetFloat("_HaloRadius", Mathf.Max(1f, settings.haloTextureRadius));
    }
}
