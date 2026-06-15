using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 层的星座连线备用实现。
/// 当世界空间粒子连线不可用时，用 RectTransform/Image 绘制折线、节点和小星点。
/// </summary>
public class ForgeConstellationLine : MonoBehaviour
{
    private const float CoreAlphaMultiplier = 0.95f;
    private const float GlowAlphaMultiplier = 0.18f;
    private const float GlowThicknessMultiplier = 7f;
    private const int SparkMinCount = 3;
    private const int SparkMaxCount = 7;
    private const float SparkMinSize = 2f;
    private const float SparkMaxSize = 4.5f;
    private const float SparkOffset = 4f;

    private SegmentView _firstSegment;
    private SegmentView _secondSegment;
    private RectTransform _bendNode;
    private RectTransform[] _sparks;
    private float[] _sparkPathDistances;
    private float _nodeSize;

    public RectTransform RectTransform { get; private set; }

    /// <summary>
    /// 创建一条由两段 UI 线段和一个折点组成的星座连线。
    /// </summary>
    /// <param name="parent">线段挂载的 UI 父节点。</param>
    /// <param name="start">线段起点，使用父节点局部坐标。</param>
    /// <param name="end">线段终点，使用父节点局部坐标。</param>
    /// <param name="seed">随机种子，用于稳定生成折点和星点。</param>
    /// <param name="color">线段、节点和星点颜色。</param>
    /// <param name="thickness">核心线段厚度。</param>
    /// <param name="nodeSize">折点节点尺寸。</param>
    /// <param name="bendOffset">折点相对直线的偏移强度。</param>
    /// <param name="bendMinT">折点在线段中的最小比例。</param>
    /// <param name="bendMaxT">折点在线段中的最大比例。</param>
    /// <param name="objectName">生成对象名称。</param>
    /// <returns>创建出的 UI 星座连线；参数无效时返回 null。</returns>
    public static ForgeConstellationLine Create(
        Transform parent,
        Vector2 start,
        Vector2 end,
        int seed,
        Color color,
        float thickness,
        float nodeSize,
        float bendOffset,
        float bendMinT,
        float bendMaxT,
        string objectName)
    {
        if (parent == null) return null;

        Vector2 delta = end - start;
        if (delta.sqrMagnitude <= 0.01f) return null;

        GameObject rootObject = new GameObject(objectName, typeof(RectTransform), typeof(ForgeConstellationLine));
        rootObject.transform.SetParent(parent, false);
        rootObject.transform.SetAsFirstSibling();

        ForgeConstellationLine line = rootObject.GetComponent<ForgeConstellationLine>();
        line.RectTransform = rootObject.GetComponent<RectTransform>();
        line.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        line.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        line.RectTransform.pivot = new Vector2(0.5f, 0.5f);
        line.RectTransform.anchoredPosition = Vector2.zero;
        line.RectTransform.sizeDelta = Vector2.zero;
        line._nodeSize = nodeSize;

        Vector2 bend = GetBendPoint(start, end, seed, bendOffset, bendMinT, bendMaxT);
        line._firstSegment = line.CreateSegment(start, bend, color, thickness, "ConstellationSegmentA");
        line._secondSegment = line.CreateSegment(bend, end, color, thickness, "ConstellationSegmentB");
        line._bendNode = line.CreateNode(bend, color);
        line.CreateSparks(start, bend, end, seed, color);
        return line;
    }

    /// <summary>
    /// 设置线段显现进度，用于长按时从法术向启迪生长。
    /// </summary>
    /// <param name="progress">显现进度，0 为隐藏，1 为完整显示。</param>
    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float firstLength = _firstSegment?.FullLength ?? 0f;
        float secondLength = _secondSegment?.FullLength ?? 0f;
        float totalLength = firstLength + secondLength;

        if (totalLength <= 0.01f)
        {
            SetSegmentVisibleLength(_firstSegment, 0f);
            SetSegmentVisibleLength(_secondSegment, 0f);
            if (_bendNode != null) _bendNode.gameObject.SetActive(false);
            return;
        }

        float visibleLength = totalLength * progress;
        SetSegmentVisibleLength(_firstSegment, Mathf.Min(visibleLength, firstLength));
        SetSegmentVisibleLength(_secondSegment, Mathf.Max(0f, visibleLength - firstLength));

        if (_bendNode != null)
        {
            bool reachedNode = visibleLength >= firstLength;
            _bendNode.gameObject.SetActive(reachedNode);
            _bendNode.localScale = reachedNode
                ? Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Clamp01((visibleLength - firstLength) / Mathf.Max(1f, _nodeSize)))
                : Vector3.zero;
        }

        UpdateSparkVisibility(visibleLength);
    }

    /// <summary>
    /// 根据起终点和随机种子计算折线中间节点位置。
    /// </summary>
    /// <param name="start">起点局部坐标。</param>
    /// <param name="end">终点局部坐标。</param>
    /// <param name="seed">随机种子。</param>
    /// <param name="bendOffset">折点偏移强度。</param>
    /// <param name="bendMinT">折点最小路径比例。</param>
    /// <param name="bendMaxT">折点最大路径比例。</param>
    /// <returns>折点局部坐标。</returns>
    private static Vector2 GetBendPoint(Vector2 start, Vector2 end, int seed, float bendOffset, float bendMinT, float bendMaxT)
    {
        Vector2 delta = end - start;
        Vector2 dir = delta.normalized;
        Vector2 perpendicular = new Vector2(-dir.y, dir.x);

        float minT = Mathf.Clamp01(Mathf.Min(bendMinT, bendMaxT));
        float maxT = Mathf.Clamp01(Mathf.Max(bendMinT, bendMaxT));
        if (Mathf.Approximately(minT, maxT)) maxT = Mathf.Clamp01(minT + 0.01f);

        System.Random random = new System.Random(seed & int.MaxValue);
        float t = Mathf.Lerp(minT, maxT, (float)random.NextDouble());
        float sign = random.Next(0, 2) == 0 ? -1f : 1f;
        float offsetScale = Mathf.Lerp(0.35f, 1f, (float)random.NextDouble());
        float distanceScale = Mathf.Clamp(delta.magnitude / 220f, 0.45f, 1.35f);
        float offset = bendOffset * offsetScale * distanceScale * sign;

        return Vector2.Lerp(start, end, t) + perpendicular * offset;
    }

    /// <summary>
    /// 创建一段包含核心线和光晕线的 UI 线段。
    /// </summary>
    /// <param name="start">线段起点。</param>
    /// <param name="end">线段终点。</param>
    /// <param name="color">线段颜色。</param>
    /// <param name="thickness">核心线厚度。</param>
    /// <param name="objectName">线段对象名称前缀。</param>
    /// <returns>线段视图数据。</returns>
    private SegmentView CreateSegment(Vector2 start, Vector2 end, Color color, float thickness, string objectName)
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float glowThickness = thickness * GlowThicknessMultiplier;
        RectTransform glowRect = CreateLineImage(
            objectName + "_Glow",
            start,
            length,
            glowThickness,
            angle,
            WithAlpha(color, color.a * GlowAlphaMultiplier));
        RectTransform coreRect = CreateLineImage(
            objectName + "_Core",
            start,
            length,
            thickness,
            angle,
            WithAlpha(color, color.a * CoreAlphaMultiplier));

        return new SegmentView
        {
            CoreRect = coreRect,
            GlowRect = glowRect,
            FullLength = length,
            Thickness = thickness,
            GlowThickness = glowThickness,
        };
    }

    /// <summary>
    /// 创建单条 Image 线段并设置长度、厚度和旋转。
    /// </summary>
    /// <param name="objectName">生成对象名称。</param>
    /// <param name="start">线段起点。</param>
    /// <param name="length">线段长度。</param>
    /// <param name="thickness">线段厚度。</param>
    /// <param name="angle">线段旋转角度。</param>
    /// <param name="color">线段颜色。</param>
    /// <returns>线段 RectTransform。</returns>
    private RectTransform CreateLineImage(string objectName, Vector2 start, float length, float thickness, float angle, Color color)
    {
        GameObject lineObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(transform, false);

        Image image = lineObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = lineObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = start;
        rect.sizeDelta = new Vector2(length, thickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        return rect;
    }

    /// <summary>
    /// 创建折线中间的星座节点。
    /// </summary>
    /// <param name="position">节点局部坐标。</param>
    /// <param name="color">节点颜色。</param>
    /// <returns>节点 RectTransform。</returns>
    private RectTransform CreateNode(Vector2 position, Color color)
    {
        GameObject nodeObject = new GameObject("ConstellationNode", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        nodeObject.transform.SetParent(transform, false);

        Image image = nodeObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = nodeObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(_nodeSize, _nodeSize);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

        return rect;
    }

    /// <summary>
    /// 沿折线路径生成若干装饰星点。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="seed">随机种子。</param>
    /// <param name="color">星点颜色。</param>
    private void CreateSparks(Vector2 start, Vector2 bend, Vector2 end, int seed, Color color)
    {
        float firstLength = Vector2.Distance(start, bend);
        float secondLength = Vector2.Distance(bend, end);
        float totalLength = firstLength + secondLength;
        if (totalLength <= 0.01f) return;

        System.Random random = new System.Random((seed * 397) & int.MaxValue);
        int count = random.Next(SparkMinCount, SparkMaxCount + 1);
        _sparks = new RectTransform[count];
        _sparkPathDistances = new float[count];

        for (int i = 0; i < count; i++)
        {
            float pathDistance = Mathf.Lerp(totalLength * 0.12f, totalLength * 0.9f, (float)random.NextDouble());
            Vector2 position = GetPointAlongPath(start, bend, end, firstLength, pathDistance, out Vector2 direction);
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float offset = Mathf.Lerp(-SparkOffset, SparkOffset, (float)random.NextDouble());
            float size = Mathf.Lerp(SparkMinSize, SparkMaxSize, (float)random.NextDouble());

            GameObject sparkObject = new GameObject("ConstellationSpark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sparkObject.transform.SetParent(transform, false);

            Image image = sparkObject.GetComponent<Image>();
            image.color = WithAlpha(color, Mathf.Lerp(0.35f, 0.75f, (float)random.NextDouble()) * color.a);
            image.raycastTarget = false;

            RectTransform rect = sparkObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position + perpendicular * offset;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            rect.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, (float)random.NextDouble());

            _sparks[i] = rect;
            _sparkPathDistances[i] = pathDistance;
        }
    }

    /// <summary>
    /// 根据可见长度裁剪一段线段的核心线和光晕线。
    /// </summary>
    /// <param name="segment">要裁剪的线段视图。</param>
    /// <param name="visibleLength">当前可见长度。</param>
    private static void SetSegmentVisibleLength(SegmentView segment, float visibleLength)
    {
        if (segment == null) return;

        float length = Mathf.Clamp(visibleLength, 0f, segment.FullLength);
        bool visible = length > 0.01f;
        SetLineRectVisibleLength(segment.CoreRect, visible, length, segment.Thickness);
        SetLineRectVisibleLength(segment.GlowRect, visible, length, segment.GlowThickness);
    }

    /// <summary>
    /// 设置单条 UI 线段的显隐和尺寸。
    /// </summary>
    /// <param name="rect">线段 RectTransform。</param>
    /// <param name="visible">是否可见。</param>
    /// <param name="length">当前显示长度。</param>
    /// <param name="thickness">线段厚度。</param>
    private static void SetLineRectVisibleLength(RectTransform rect, bool visible, float length, float thickness)
    {
        if (rect == null) return;
        rect.gameObject.SetActive(visible);
        rect.sizeDelta = new Vector2(length, thickness);
    }

    /// <summary>
    /// 根据线段生长进度显示或隐藏路径上的星点。
    /// </summary>
    /// <param name="visibleLength">当前已经显现的路径长度。</param>
    private void UpdateSparkVisibility(float visibleLength)
    {
        if (_sparks == null || _sparkPathDistances == null) return;

        for (int i = 0; i < _sparks.Length; i++)
        {
            RectTransform spark = _sparks[i];
            if (spark == null) continue;

            bool visible = visibleLength >= _sparkPathDistances[i];
            spark.gameObject.SetActive(visible);
            if (visible)
            {
                float reveal = Mathf.Clamp01((visibleLength - _sparkPathDistances[i]) / Mathf.Max(1f, SparkMaxSize * 2f));
                spark.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, reveal);
            }
        }
    }

    /// <summary>
    /// 获取折线路径上指定距离处的点和方向。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段路径长度。</param>
    /// <param name="pathDistance">从起点开始的路径距离。</param>
    /// <param name="direction">该点所在路径段的方向。</param>
    /// <returns>路径上的局部坐标。</returns>
    private static Vector2 GetPointAlongPath(Vector2 start, Vector2 bend, Vector2 end, float firstLength, float pathDistance, out Vector2 direction)
    {
        if (pathDistance <= firstLength)
        {
            direction = (bend - start).normalized;
            return Vector2.Lerp(start, bend, Mathf.Clamp01(pathDistance / Mathf.Max(0.01f, firstLength)));
        }

        float secondLength = Vector2.Distance(bend, end);
        direction = (end - bend).normalized;
        return Vector2.Lerp(bend, end, Mathf.Clamp01((pathDistance - firstLength) / Mathf.Max(0.01f, secondLength)));
    }

    /// <summary>
    /// 返回替换透明度后的颜色。
    /// </summary>
    /// <param name="color">原始颜色。</param>
    /// <param name="alpha">目标透明度。</param>
    /// <returns>设置透明度后的颜色。</returns>
    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    /// <summary>
    /// 一段 UI 星座线的核心线、光晕线和尺寸缓存。
    /// </summary>
    private class SegmentView
    {
        public RectTransform CoreRect;
        public RectTransform GlowRect;
        public float FullLength;
        public float Thickness;
        public float GlowThickness;
    }
}
