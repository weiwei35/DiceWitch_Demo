using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 为一组独立的 UI 星星 Image 添加光晕、微弱呼吸和不完全同步的闪动。
/// 挂在星星父物体上即可，默认会自动收集子级 Image。
/// </summary>
public class StarImageIdleEffect : MonoBehaviour
{
    private const string GeneratedGlowSuffix = "__GeneratedStarGlow";

    [Header("Targets")]
    [Tooltip("为空时会自动收集当前物体子级中的 Image。")]
    public List<Image> stars = new List<Image>();
    public bool autoCollectChildren = true;

    [Header("Glow")]
    public bool createGlow = true;
    public Color glowColor = new Color(1f, 0.92f, 0.55f, 0.32f);
    public float glowScale = 2.9f;
    public float glowMinAlpha = 0.08f;
    public float glowMaxAlpha = 0.24f;
    public bool hideGlowWhenStopped = true;
    public int glowTextureSize = 128;
    [Range(0.25f, 0.95f)] public float glowSoftness = 0.78f;
    [Range(0f, 0.35f)] public float glowEdgeNoise = 0.12f;

    [Header("Star Body")]
    public float starBodyScale = 1.06f;
    public float starBodyScaleDuration = 1.8f;
    public Ease starBodyEase = Ease.InOutSine;
    public bool rotateStarBody = true;
    public float starBodyRotationRange = 2.5f;
    public float starBodyRotationDuration = 2.4f;

    [Header("Glow Pulse")]
    public float glowPulseScale = 1.1f;
    public float glowPulseDuration = 2.1f;
    public Ease glowPulseEase = Ease.InOutSine;

    [Header("Twinkle")]
    public float playDelay = 0f;
    public float starMinAlpha = 0.72f;
    public float starMaxAlpha = 1f;
    public float twinkleDurationMin = 0.45f;
    public float twinkleDurationMax = 1.15f;
    public float randomStartDelayMax = 1.2f;

    private readonly Dictionary<Image, Image> _glows = new Dictionary<Image, Image>();
    private readonly Dictionary<Image, CanvasGroup> _starGroups = new Dictionary<Image, CanvasGroup>();
    private readonly Dictionary<Image, CanvasGroup> _glowGroups = new Dictionary<Image, CanvasGroup>();
    private readonly Dictionary<RectTransform, Vector3> _baseScales = new Dictionary<RectTransform, Vector3>();
    private readonly Dictionary<RectTransform, Quaternion> _baseRotations = new Dictionary<RectTransform, Quaternion>();
    private readonly Dictionary<CanvasGroup, float> _baseAlphas = new Dictionary<CanvasGroup, float>();
    private Sprite _softGlowSprite;
    private int _softGlowTextureSize;
    private float _softGlowSoftness;
    private float _softGlowEdgeNoise;

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>
    /// 重新收集星星并开始播放所有待机效果。
    /// </summary>
    [ContextMenu("Play Stars")]
    public void Play()
    {
        Stop();

        if (autoCollectChildren)
            CollectChildStars();

        foreach (Image star in stars)
        {
            if (star == null) continue;
            RectTransform starRect = star.rectTransform;
            CaptureBasePose(starRect);

            CanvasGroup starGroup = GetCanvasGroup(star);
            Image glow = createGlow ? GetOrCreateGlow(star) : null;
            CanvasGroup glowGroup = glow != null ? GetCanvasGroup(glow) : null;

            float startDelay = Mathf.Max(0f, playDelay) + Random.Range(0f, Mathf.Max(0f, randomStartDelayMax));
            PlayStarBreath(starRect, startDelay);
            PlayStarTwinkle(starGroup, startDelay);

            if (glow != null)
            {
                RectTransform glowRect = glow.rectTransform;
                CaptureBasePose(glowRect);
                SyncGlowTransform(star, glow);
                PlayGlowBreath(glowRect, startDelay);
                PlayGlowTwinkle(glowGroup, startDelay);
            }

            if (rotateStarBody)
                PlayStarBodyRotation(starRect, startDelay);
        }
    }

    /// <summary>
    /// 停止所有星星动画，并恢复主星的基础缩放和旋转。
    /// </summary>
    [ContextMenu("Stop Stars")]
    public void Stop()
    {
        DOTween.Kill(this);

        foreach (Image star in stars)
        {
            if (star == null) continue;
            RectTransform rect = star.rectTransform;

            if (_baseScales.TryGetValue(rect, out Vector3 scale))
                rect.localScale = scale;
            if (_baseRotations.TryGetValue(rect, out Quaternion rotation))
                rect.localRotation = rotation;

            if (_starGroups.TryGetValue(star, out CanvasGroup group) && group != null)
                RestoreAlpha(group);
        }

        foreach (KeyValuePair<Image, Image> pair in _glows)
        {
            Image glow = pair.Value;
            if (glow == null) continue;

            if (_glowGroups.TryGetValue(glow, out CanvasGroup group) && group != null)
                group.alpha = hideGlowWhenStopped ? 0f : GetBaseAlpha(group);
        }
    }

    /// <summary>
    /// 从当前物体子级收集星星 Image，自动忽略脚本生成的光晕 Image。
    /// </summary>
    [ContextMenu("Collect Child Stars")]
    public void CollectChildStars()
    {
        stars.Clear();
        Image[] childImages = GetComponentsInChildren<Image>(true);
        foreach (Image image in childImages)
        {
            if (image == null || image.transform == transform) continue;
            if (IsGeneratedGlow(image)) continue;
            stars.Add(image);
        }
    }

    /// <summary>
    /// 清理脚本生成的光晕对象，方便重新配置。
    /// </summary>
    [ContextMenu("Destroy Generated Glows")]
    public void DestroyGeneratedGlows()
    {
        foreach (KeyValuePair<Image, Image> pair in _glows)
        {
            Image glow = pair.Value;
            if (glow == null) continue;

            if (Application.isPlaying)
                Destroy(glow.gameObject);
            else
                DestroyImmediate(glow.gameObject);
        }

        _glows.Clear();
        _glowGroups.Clear();
    }

    private void PlayStarBreath(RectTransform rect, float startDelay)
    {
        Vector3 baseScale = _baseScales[rect];
        float targetScale = Random.Range(Mathf.Lerp(1f, starBodyScale, 0.65f), starBodyScale);
        float duration = Mathf.Max(0.01f, starBodyScaleDuration) * Random.Range(0.88f, 1.18f);

        rect.DOScale(baseScale * Mathf.Max(0.01f, targetScale), duration)
            .SetEase(starBodyEase)
            .SetDelay(startDelay)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    private void PlayGlowBreath(RectTransform rect, float startDelay)
    {
        Vector3 baseScale = _baseScales[rect];
        float targetScale = Random.Range(Mathf.Lerp(1f, glowPulseScale, 0.65f), glowPulseScale);
        float duration = Mathf.Max(0.01f, glowPulseDuration) * Random.Range(0.9f, 1.22f);

        rect.DOScale(baseScale * Mathf.Max(0.01f, targetScale), duration)
            .SetEase(glowPulseEase)
            .SetDelay(startDelay)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    private void PlayStarTwinkle(CanvasGroup group, float startDelay)
    {
        if (group == null) return;

        DOVirtual.DelayedCall(startDelay, () => PlayRandomFade(group, starMinAlpha, starMaxAlpha))
            .SetTarget(this);
    }

    private void PlayGlowTwinkle(CanvasGroup group, float startDelay)
    {
        if (group == null) return;

        DOVirtual.DelayedCall(startDelay * 0.75f, () => PlayRandomFade(group, glowMinAlpha, glowMaxAlpha))
            .SetTarget(this);
    }

    private void PlayRandomFade(CanvasGroup group, float minAlpha, float maxAlpha)
    {
        if (group == null || !isActiveAndEnabled) return;

        float min = Mathf.Clamp01(Mathf.Min(minAlpha, maxAlpha));
        float max = Mathf.Clamp01(Mathf.Max(minAlpha, maxAlpha));
        float duration = Random.Range(Mathf.Max(0.01f, twinkleDurationMin), Mathf.Max(0.02f, twinkleDurationMax));
        float alpha = Random.Range(min, max);

        group.DOFade(alpha, duration)
            .SetEase(Ease.InOutSine)
            .SetTarget(this)
            .OnComplete(() => PlayRandomFade(group, min, max));
    }

    private void PlayStarBodyRotation(RectTransform rect, float startDelay)
    {
        Quaternion baseRotation = _baseRotations[rect];
        float targetRotation = Random.Range(-starBodyRotationRange, starBodyRotationRange);
        float duration = Mathf.Max(0.01f, starBodyRotationDuration) * Random.Range(0.85f, 1.25f);

        rect.DOLocalRotateQuaternion(baseRotation * Quaternion.Euler(0f, 0f, targetRotation), duration)
            .SetEase(Ease.InOutSine)
            .SetDelay(startDelay * 0.5f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    private Image GetOrCreateGlow(Image star)
    {
        if (star == null) return null;
        if (_glows.TryGetValue(star, out Image cachedGlow) && cachedGlow != null)
        {
            ConfigureGlowImage(star, cachedGlow);
            return cachedGlow;
        }

        Image existingGlow = FindExistingGlow(star);
        if (existingGlow != null)
        {
            ConfigureGlowImage(star, existingGlow);
            _glows[star] = existingGlow;
            return existingGlow;
        }

        GameObject glowObject = new GameObject($"{star.name}{GeneratedGlowSuffix}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        Transform parent = star.transform.parent;
        glowRect.SetParent(parent, false);
        glowRect.SetSiblingIndex(star.transform.GetSiblingIndex());

        Image glow = glowObject.GetComponent<Image>();
        ConfigureGlowImage(star, glow);
        _glows[star] = glow;
        return glow;
    }

    private void ConfigureGlowImage(Image star, Image glow)
    {
        if (star == null || glow == null) return;

        glow.raycastTarget = false;
        glow.sprite = GetSoftGlowSprite();
        glow.type = Image.Type.Simple;
        glow.preserveAspect = false;
        glow.color = glowColor;

        SyncGlowTransform(star, glow);
    }

    private void SyncGlowTransform(Image star, Image glow)
    {
        if (star == null || glow == null) return;

        RectTransform starRect = star.rectTransform;
        RectTransform glowRect = glow.rectTransform;
        float starWidth = Mathf.Max(starRect.rect.width, Mathf.Abs(starRect.sizeDelta.x));
        float starHeight = Mathf.Max(starRect.rect.height, Mathf.Abs(starRect.sizeDelta.y));
        float glowSize = Mathf.Max(starWidth, starHeight, 1f) * Mathf.Max(0.01f, glowScale);

        glowRect.anchorMin = starRect.anchorMin;
        glowRect.anchorMax = starRect.anchorMax;
        glowRect.pivot = starRect.pivot;
        glowRect.anchoredPosition = starRect.anchoredPosition;
        glowRect.sizeDelta = new Vector2(glowSize, glowSize);
        glowRect.localRotation = starRect.localRotation;
        glowRect.localScale = starRect.localScale;
    }

    private Image FindExistingGlow(Image star)
    {
        if (star == null || star.transform.parent == null) return null;

        string glowName = $"{star.name}{GeneratedGlowSuffix}";
        Transform found = star.transform.parent.Find(glowName);
        return found != null ? found.GetComponent<Image>() : null;
    }

    private Sprite GetSoftGlowSprite()
    {
        int size = Mathf.Clamp(glowTextureSize, 32, 512);
        float softness = Mathf.Clamp01(glowSoftness);
        float edgeNoise = Mathf.Clamp01(glowEdgeNoise);

        if (_softGlowSprite != null &&
            _softGlowTextureSize == size &&
            Mathf.Approximately(_softGlowSoftness, softness) &&
            Mathf.Approximately(_softGlowEdgeNoise, edgeNoise))
        {
            return _softGlowSprite;
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Generated_StarSoftGlow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        float center = (size - 1) * 0.5f;
        float innerRadius = Mathf.Lerp(0.02f, 0.32f, 1f - softness);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float radialAlpha = 1f - Mathf.SmoothStep(innerRadius, 1f, distance);
                float fringe = Mathf.PerlinNoise(x * 0.115f + 13.7f, y * 0.115f + 29.3f);
                float fringeAlpha = Mathf.Lerp(1f - edgeNoise, 1f + edgeNoise, fringe);
                float alpha = Mathf.Clamp01(radialAlpha * radialAlpha * fringeAlpha);

                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);

        _softGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _softGlowSprite.name = "Generated_StarSoftGlow";
        _softGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        _softGlowTextureSize = size;
        _softGlowSoftness = softness;
        _softGlowEdgeNoise = edgeNoise;
        return _softGlowSprite;
    }

    private CanvasGroup GetCanvasGroup(Image image)
    {
        if (image == null) return null;

        Dictionary<Image, CanvasGroup> cache = IsGeneratedGlow(image) ? _glowGroups : _starGroups;
        if (cache.TryGetValue(image, out CanvasGroup cachedGroup) && cachedGroup != null)
            return cachedGroup;

        CanvasGroup group = image.GetComponent<CanvasGroup>();
        if (group == null)
            group = image.gameObject.AddComponent<CanvasGroup>();

        cache[image] = group;
        CaptureBaseAlpha(group);
        return group;
    }

    private void CaptureBasePose(RectTransform rect)
    {
        if (rect == null) return;

        if (!_baseScales.ContainsKey(rect))
            _baseScales[rect] = rect.localScale;

        if (!_baseRotations.ContainsKey(rect))
            _baseRotations[rect] = rect.localRotation;
    }

    private void CaptureBaseAlpha(CanvasGroup group)
    {
        if (group != null && !_baseAlphas.ContainsKey(group))
            _baseAlphas[group] = group.alpha;
    }

    private void RestoreAlpha(CanvasGroup group)
    {
        if (group == null) return;

        group.alpha = GetBaseAlpha(group);
    }

    private float GetBaseAlpha(CanvasGroup group)
    {
        return group != null && _baseAlphas.TryGetValue(group, out float alpha) ? alpha : 1f;
    }

    private bool IsGeneratedGlow(Image image)
    {
        return image != null && (_glows.ContainsValue(image) || image.name.EndsWith(GeneratedGlowSuffix));
    }
}
