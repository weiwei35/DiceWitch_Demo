using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reveals authored overlay images through visited-node circles or a horizontal cutoff.
/// The source images keep their original sprites, positions, and seams.
/// </summary>
public class MapGridRevealLayer : MonoBehaviour
{
    private const int MaxCirclesPerImage = 64;

    private static readonly int RevealCirclesId = Shader.PropertyToID("_RevealCircles");
    private static readonly int RevealCountId = Shader.PropertyToID("_RevealCount");
    private static readonly int RevealLocalRectId = Shader.PropertyToID("_RevealLocalRect");
    private static readonly int RevealUvRectId = Shader.PropertyToID("_RevealUvRect");
    private static readonly int RevealModeId = Shader.PropertyToID("_RevealMode");
    private static readonly int RevealCutoffXId = Shader.PropertyToID("_RevealCutoffX");
    private static readonly int RevealCutoffFeatherId = Shader.PropertyToID("_RevealCutoffFeather");

    [Header("Authored Overlay")]
    [SerializeField] private Shader revealShader;
    [SerializeField] private List<Image> overlayImages = new List<Image>();

    private readonly List<Material> _runtimeMaterials = new List<Material>();
    private readonly Vector4[] _circleBuffer = new Vector4[MaxCirclesPerImage];
    private bool _initialized;

    public bool IsConfigured =>
        revealShader != null &&
        overlayImages != null &&
        overlayImages.Count > 0 &&
        !overlayImages.Contains(null);

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        if (!IsConfigured)
        {
            SetOverlayImagesEnabled(false);
            Debug.LogError("地图已走过格纹层缺少 revealShader 或 overlayImages 配置。", this);
            return;
        }

        foreach (Image image in overlayImages)
        {
            if (image == null) continue;

            Material material = new Material(revealShader)
            {
                name = $"{revealShader.name} ({image.name}) Runtime"
            };
            SetRevealGeometry(image, material);
            image.material = material;
            image.raycastTarget = false;
            image.enabled = true;
            SetCircleRevealData(image, material, 0);
            _runtimeMaterials.Add(material);
        }

        ScheduleRenderingMaterialRefresh();
    }

    public void ApplyReveal(
        IReadOnlyDictionary<int, RectTransform> nodeRects,
        IReadOnlyCollection<int> revealedNodeIndices,
        float radius,
        float feather)
    {
        Initialize();
        if (!IsConfigured) return;

        float safeRadius = Mathf.Max(0f, radius);
        float safeFeather = Mathf.Clamp(feather, 0f, safeRadius);
        int materialIndex = 0;

        foreach (Image image in overlayImages)
        {
            if (image == null) continue;
            if (materialIndex >= _runtimeMaterials.Count) break;

            RectTransform imageRect = image.rectTransform;
            int circleCount = BuildCircleBuffer(imageRect, nodeRects, revealedNodeIndices, safeRadius, safeFeather);
            Material material = _runtimeMaterials[materialIndex++];
            SetCircleRevealData(image, material, circleCount);
        }
    }

    public void ApplyRevealLeftOf(Transform revealBoundary, float feather)
    {
        Initialize();
        if (!IsConfigured || revealBoundary == null) return;

        int materialIndex = 0;
        foreach (Image image in overlayImages)
        {
            if (image == null) continue;
            if (materialIndex >= _runtimeMaterials.Count) break;

            float cutoffX = image.rectTransform.InverseTransformPoint(revealBoundary.position).x;
            Material material = _runtimeMaterials[materialIndex++];
            SetLeftRevealData(image, material, cutoffX, Mathf.Max(0f, feather));
        }
    }

    private void SetCircleRevealData(Image image, Material sourceMaterial, int circleCount)
    {
        SetRevealGeometry(image, sourceMaterial);
        SetCircleRevealData(sourceMaterial, circleCount);

        // MaskableGraphic may render with a cached stencil-material copy rather than image.material.
        // Shader arrays are not ShaderLab properties, so Unity does not copy them automatically.
        Material renderingMaterial = image.materialForRendering;
        if (renderingMaterial != null && renderingMaterial != sourceMaterial)
        {
            SetRevealGeometry(image, renderingMaterial);
            SetCircleRevealData(renderingMaterial, circleCount);
        }
    }

    private void SetLeftRevealData(Image image, Material sourceMaterial, float cutoffX, float feather)
    {
        SetRevealGeometry(image, sourceMaterial);
        SetLeftRevealData(sourceMaterial, cutoffX, feather);

        Material renderingMaterial = image.materialForRendering;
        if (renderingMaterial != null && renderingMaterial != sourceMaterial)
        {
            SetRevealGeometry(image, renderingMaterial);
            SetLeftRevealData(renderingMaterial, cutoffX, feather);
        }
    }

    private static void SetRevealGeometry(Image image, Material material)
    {
        Rect localRect = image.rectTransform.rect;
        material.SetVector(
            RevealLocalRectId,
            new Vector4(localRect.xMin, localRect.yMin, localRect.xMax, localRect.yMax));

        Vector4 outerUv = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
        material.SetVector(RevealUvRectId, outerUv);
    }

    private void SetCircleRevealData(Material material, int circleCount)
    {
        material.SetVectorArray(RevealCirclesId, _circleBuffer);
        material.SetFloat(RevealCountId, circleCount);
        material.SetFloat(RevealModeId, 0f);
    }

    private static void SetLeftRevealData(Material material, float cutoffX, float feather)
    {
        material.SetFloat(RevealCountId, 0f);
        material.SetFloat(RevealModeId, 1f);
        material.SetFloat(RevealCutoffXId, cutoffX);
        material.SetFloat(RevealCutoffFeatherId, feather);
    }

    private void OnEnable()
    {
        if (_initialized)
            ScheduleRenderingMaterialRefresh();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= RefreshRenderingMaterials;
    }

    private void ScheduleRenderingMaterialRefresh()
    {
        Canvas.willRenderCanvases -= RefreshRenderingMaterials;
        Canvas.willRenderCanvases += RefreshRenderingMaterials;
    }

    private void RefreshRenderingMaterials()
    {
        Canvas.willRenderCanvases -= RefreshRenderingMaterials;

        int imageCount = Mathf.Min(overlayImages.Count, _runtimeMaterials.Count);
        for (int i = 0; i < imageCount; i++)
        {
            Image image = overlayImages[i];
            Material sourceMaterial = _runtimeMaterials[i];
            if (image == null || sourceMaterial == null) continue;

            Material renderingMaterial = image.materialForRendering;
            if (renderingMaterial == null || renderingMaterial == sourceMaterial) continue;

            renderingMaterial.SetVectorArray(
                RevealCirclesId,
                sourceMaterial.GetVectorArray(RevealCirclesId));
            renderingMaterial.SetFloat(
                RevealCountId,
                sourceMaterial.GetFloat(RevealCountId));
            renderingMaterial.SetVector(
                RevealLocalRectId,
                sourceMaterial.GetVector(RevealLocalRectId));
            renderingMaterial.SetVector(
                RevealUvRectId,
                sourceMaterial.GetVector(RevealUvRectId));
            renderingMaterial.SetFloat(
                RevealModeId,
                sourceMaterial.GetFloat(RevealModeId));
            renderingMaterial.SetFloat(
                RevealCutoffXId,
                sourceMaterial.GetFloat(RevealCutoffXId));
            renderingMaterial.SetFloat(
                RevealCutoffFeatherId,
                sourceMaterial.GetFloat(RevealCutoffFeatherId));
        }
    }

    private int BuildCircleBuffer(
        RectTransform imageRect,
        IReadOnlyDictionary<int, RectTransform> nodeRects,
        IReadOnlyCollection<int> revealedNodeIndices,
        float radius,
        float feather)
    {
        int count = 0;
        float paddedRadius = radius + feather;
        float paddedRadiusSquared = paddedRadius * paddedRadius;
        Rect imageBounds = imageRect.rect;

        foreach (int nodeIndex in revealedNodeIndices)
        {
            if (!nodeRects.TryGetValue(nodeIndex, out RectTransform nodeRect) || nodeRect == null)
                continue;

            Vector2 localCenter = imageRect.InverseTransformPoint(nodeRect.position);
            Vector2 nearestPoint = new Vector2(
                Mathf.Clamp(localCenter.x, imageBounds.xMin, imageBounds.xMax),
                Mathf.Clamp(localCenter.y, imageBounds.yMin, imageBounds.yMax));

            if ((nearestPoint - localCenter).sqrMagnitude > paddedRadiusSquared)
                continue;

            if (count >= MaxCirclesPerImage)
            {
                Debug.LogWarning($"地图格纹单张图片最多支持 {MaxCirclesPerImage} 个相交揭示圆，后续圆已忽略。", this);
                break;
            }

            _circleBuffer[count++] = new Vector4(localCenter.x, localCenter.y, radius, feather);
        }

        return count;
    }

    private void SetOverlayImagesEnabled(bool isEnabled)
    {
        if (overlayImages == null) return;

        foreach (Image image in overlayImages)
        {
            if (image != null)
                image.enabled = isEnabled;
        }
    }

    private void OnDestroy()
    {
        Canvas.willRenderCanvases -= RefreshRenderingMaterials;

        foreach (Material material in _runtimeMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        _runtimeMaterials.Clear();
    }
}
