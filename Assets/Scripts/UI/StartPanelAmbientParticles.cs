using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始界面氛围粒子。
/// 用少量 UI Image 生成细微的星尘/萤火虫漂浮、闪烁效果，适合放在背景层之上、按钮层之下。
/// </summary>
public class StartPanelAmbientParticles : MonoBehaviour
{
    [Header("References")]
    [Tooltip("粒子生成范围。为空时使用本物体 RectTransform。")]
    public RectTransform spawnArea;
    [Tooltip("粒子父节点。为空时使用本物体。")]
    public RectTransform particleContainer;
    [Tooltip("粒子可用贴图。为空时会生成纯色小光点。")]
    public List<Sprite> particleSprites = new List<Sprite>();

    [Header("Amount")]
    [Min(0)] public int particleCount = 34;
    [Tooltip("避免粒子同一时间全部出现。")]
    public bool randomizeInitialLife = true;

    [Header("Look")]
    public Color particleColor = new Color(1f, 0.9f, 0.55f, 0.38f);
    public Vector2 sizeRange = new Vector2(4f, 13f);
    public Vector2 alphaRange = new Vector2(0.08f, 0.42f);
    public Vector2 glowPulseRange = new Vector2(0.85f, 1.25f);

    [Header("Glow")]
    public bool createGlow = true;
    public Color glowColor = new Color(1f, 0.9f, 0.45f, 0.22f);
    public float glowScale = 3.2f;
    public float glowAlphaMultiplier = 0.72f;

    [Header("Motion")]
    public Vector2 lifeRange = new Vector2(4.5f, 9f);
    public Vector2 driftSpeedX = new Vector2(-10f, 10f);
    public Vector2 driftSpeedY = new Vector2(8f, 24f);
    public Vector2 swayAmplitudeRange = new Vector2(6f, 18f);
    public Vector2 swayFrequencyRange = new Vector2(0.35f, 0.9f);
    public Vector2 rotationSpeedRange = new Vector2(-18f, 18f);

    private readonly List<ParticleView> _particles = new List<ParticleView>();
    private Vector2 _areaSize;
    private Sprite _fallbackDotSprite;
    private Sprite _softGlowSprite;

    private void OnEnable()
    {
        EnsureParticles();
        ResetAllParticles();
    }

    private void OnDisable()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            if (_particles[i].image != null)
                _particles[i].image.gameObject.SetActive(false);
            if (_particles[i].glowImage != null)
                _particles[i].glowImage.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (_particles.Count != particleCount)
            EnsureParticles();

        RefreshAreaSize();
        float deltaTime = Time.unscaledDeltaTime;

        for (int i = 0; i < _particles.Count; i++)
            TickParticle(_particles[i], deltaTime);
    }

    [ContextMenu("Rebuild Particles")]
    public void RebuildParticles()
    {
        ClearParticles();
        EnsureParticles();
        ResetAllParticles();
    }

    private void EnsureParticles()
    {
        RectTransform parent = GetParticleContainer();

        while (_particles.Count < particleCount)
            _particles.Add(CreateParticle(parent));

        while (_particles.Count > particleCount)
        {
            int lastIndex = _particles.Count - 1;
            ParticleView particle = _particles[lastIndex];
            DestroyParticleObject(particle.image);
            DestroyParticleObject(particle.glowImage);
            _particles.RemoveAt(lastIndex);
        }

        for (int i = 0; i < _particles.Count; i++)
        {
            EnsureGlowForParticle(_particles[i], parent);

            if (_particles[i].image != null && _particles[i].image.rectTransform.parent != parent)
                _particles[i].image.rectTransform.SetParent(parent, false);
            if (_particles[i].glowImage != null && _particles[i].glowImage.rectTransform.parent != parent)
                _particles[i].glowImage.rectTransform.SetParent(parent, false);
        }
    }

    private void EnsureGlowForParticle(ParticleView particle, RectTransform parent)
    {
        if (particle == null) return;

        if (!createGlow)
        {
            if (particle.glowImage != null)
                particle.glowImage.gameObject.SetActive(false);
            return;
        }

        if (particle.glowImage != null && particle.glowRect != null) return;

        GameObject glowObject = new GameObject("AmbientParticleGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform glowRect = glowObject.GetComponent<RectTransform>();
        glowRect.SetParent(parent, false);
        glowRect.anchorMin = new Vector2(0.5f, 0.5f);
        glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);

        Image glowImage = glowObject.GetComponent<Image>();
        glowImage.raycastTarget = false;
        glowImage.sprite = GetSoftGlowSprite();

        particle.glowImage = glowImage;
        particle.glowRect = glowRect;
    }

    private ParticleView CreateParticle(RectTransform parent)
    {
        Image glowImage = null;
        RectTransform glowRect = null;
        if (createGlow)
        {
            GameObject glowObject = new GameObject("AmbientParticleGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glowRect = glowObject.GetComponent<RectTransform>();
            glowRect.SetParent(parent, false);
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);

            glowImage = glowObject.GetComponent<Image>();
            glowImage.raycastTarget = false;
            glowImage.sprite = GetSoftGlowSprite();
        }

        GameObject particleObject = new GameObject("AmbientParticle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = particleObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = particleObject.GetComponent<Image>();
        image.raycastTarget = false;

        return new ParticleView { image = image, rect = rect, glowImage = glowImage, glowRect = glowRect };
    }

    private void ResetAllParticles()
    {
        RefreshAreaSize();

        for (int i = 0; i < _particles.Count; i++)
        {
            float lifeOffset = randomizeInitialLife ? Random.value : 0f;
            ResetParticle(_particles[i], lifeOffset);
        }
    }

    private void TickParticle(ParticleView particle, float deltaTime)
    {
        if (particle == null || particle.rect == null || particle.image == null) return;

        particle.age += deltaTime;
        if (particle.age >= particle.life)
        {
            ResetParticle(particle, 0f);
            return;
        }

        float normalizedLife = Mathf.Clamp01(particle.age / particle.life);
        Vector2 drift = particle.velocity * deltaTime;
        float sway = Mathf.Sin((particle.age + particle.seed) * particle.swayFrequency * Mathf.PI * 2f) * particle.swayAmplitude;

        particle.position += drift;
        particle.rect.anchoredPosition = particle.position + new Vector2(sway, 0f);
        particle.rect.localRotation = Quaternion.Euler(0f, 0f, particle.startRotation + particle.age * particle.rotationSpeed);

        float fade = Mathf.Sin(normalizedLife * Mathf.PI);
        float pulse = Mathf.Lerp(glowPulseRange.x, glowPulseRange.y, (Mathf.Sin((particle.age + particle.seed) * 4.2f) + 1f) * 0.5f);
        Color color = particleColor;
        color.a = particle.baseAlpha * fade * pulse;
        particle.image.color = color;

        if (particle.glowRect != null && particle.glowImage != null)
        {
            particle.glowRect.anchoredPosition = particle.rect.anchoredPosition;
            particle.glowRect.localRotation = Quaternion.identity;
            Color glow = glowColor;
            glow.a = particle.baseAlpha * fade * pulse * Mathf.Max(0f, glowAlphaMultiplier);
            particle.glowImage.color = glow;
        }
    }

    private void ResetParticle(ParticleView particle, float normalizedLifeOffset)
    {
        if (particle == null || particle.rect == null || particle.image == null) return;

        Sprite sprite = PickSprite();
        particle.image.sprite = sprite;
        particle.image.type = Image.Type.Simple;
        particle.image.color = new Color(particleColor.r, particleColor.g, particleColor.b, 0f);
        particle.image.gameObject.SetActive(true);
        bool showGlow = createGlow && particle.glowImage != null && particle.glowRect != null;
        if (showGlow)
        {
            particle.glowImage.sprite = GetSoftGlowSprite();
            particle.glowImage.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
            particle.glowImage.gameObject.SetActive(true);
        }
        else if (particle.glowImage != null)
        {
            particle.glowImage.gameObject.SetActive(false);
        }

        float width = Mathf.Max(1f, _areaSize.x);
        float height = Mathf.Max(1f, _areaSize.y);
        float size = Random.Range(Mathf.Min(sizeRange.x, sizeRange.y), Mathf.Max(sizeRange.x, sizeRange.y));

        particle.life = Random.Range(Mathf.Min(lifeRange.x, lifeRange.y), Mathf.Max(lifeRange.x, lifeRange.y));
        particle.age = Mathf.Clamp01(normalizedLifeOffset) * particle.life;
        particle.baseAlpha = Random.Range(Mathf.Min(alphaRange.x, alphaRange.y), Mathf.Max(alphaRange.x, alphaRange.y));
        particle.seed = Random.Range(0f, 1000f);
        particle.swayAmplitude = Random.Range(Mathf.Min(swayAmplitudeRange.x, swayAmplitudeRange.y), Mathf.Max(swayAmplitudeRange.x, swayAmplitudeRange.y));
        particle.swayFrequency = Random.Range(Mathf.Min(swayFrequencyRange.x, swayFrequencyRange.y), Mathf.Max(swayFrequencyRange.x, swayFrequencyRange.y));
        particle.rotationSpeed = Random.Range(Mathf.Min(rotationSpeedRange.x, rotationSpeedRange.y), Mathf.Max(rotationSpeedRange.x, rotationSpeedRange.y));
        particle.startRotation = Random.Range(0f, 360f);
        particle.velocity = new Vector2(
            Random.Range(Mathf.Min(driftSpeedX.x, driftSpeedX.y), Mathf.Max(driftSpeedX.x, driftSpeedX.y)),
            Random.Range(Mathf.Min(driftSpeedY.x, driftSpeedY.y), Mathf.Max(driftSpeedY.x, driftSpeedY.y))
        );

        particle.position = new Vector2(
            Random.Range(-width * 0.5f, width * 0.5f),
            Random.Range(-height * 0.62f, height * 0.48f)
        );

        particle.rect.sizeDelta = new Vector2(size, size);
        particle.rect.localScale = Vector3.one;
        particle.rect.anchoredPosition = particle.position;
        if (showGlow)
        {
            float glowSize = size * Mathf.Max(1f, glowScale);
            particle.glowRect.sizeDelta = new Vector2(glowSize, glowSize);
            particle.glowRect.localScale = Vector3.one;
            particle.glowRect.anchoredPosition = particle.position;
        }
    }

    private Sprite PickSprite()
    {
        if (particleSprites == null || particleSprites.Count == 0) return GetFallbackDotSprite();

        for (int i = 0; i < particleSprites.Count; i++)
        {
            Sprite sprite = particleSprites[Random.Range(0, particleSprites.Count)];
            if (sprite != null) return sprite;
        }

        return GetFallbackDotSprite();
    }

    private void RefreshAreaSize()
    {
        RectTransform area = spawnArea != null ? spawnArea : transform as RectTransform;
        _areaSize = area != null ? area.rect.size : new Vector2(Screen.width, Screen.height);
    }

    private RectTransform GetParticleContainer()
    {
        RectTransform parent = particleContainer != null ? particleContainer : transform as RectTransform;
        return parent != null ? parent : GetComponent<RectTransform>();
    }

    private void ClearParticles()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            DestroyParticleObject(_particles[i].image);
            DestroyParticleObject(_particles[i].glowImage);
        }

        _particles.Clear();
    }

    private Sprite GetFallbackDotSprite()
    {
        if (_fallbackDotSprite != null) return _fallbackDotSprite;

        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "GeneratedAmbientParticleDot";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.5f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _fallbackDotSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return _fallbackDotSprite;
    }

    private Sprite GetSoftGlowSprite()
    {
        if (_softGlowSprite != null) return _softGlowSprite;

        const int textureSize = 96;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        texture.name = "GeneratedAmbientParticleGlow";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.5f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = Mathf.Pow(alpha, 2.8f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        _softGlowSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        return _softGlowSprite;
    }

    private void DestroyParticleObject(Image image)
    {
        if (image == null) return;

        if (Application.isPlaying)
            Destroy(image.gameObject);
        else
            DestroyImmediate(image.gameObject);
    }

    private class ParticleView
    {
        public Image image;
        public RectTransform rect;
        public Image glowImage;
        public RectTransform glowRect;
        public Vector2 position;
        public Vector2 velocity;
        public float age;
        public float life;
        public float baseAlpha;
        public float seed;
        public float swayAmplitude;
        public float swayFrequency;
        public float rotationSpeed;
        public float startRotation;
    }
}
