using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 负责绘制地图房间节点之间的虚线曲线路径。
/// </summary>
public static class MapRoutePathRenderer
{
    /// <summary>
    /// 沿一条手绘感曲线路径复制路线节点贴图。
    /// </summary>
    /// <param name="rectA">路径起点节点。</param>
    /// <param name="rectB">路径终点节点。</param>
    /// <param name="parent">路线节点的 UI 父节点。</param>
    /// <param name="sprite">路线节点贴图。</param>
    /// <param name="nodeSize">每个路线节点的显示尺寸。</param>
    /// <param name="spacing">路线节点间距。</param>
    /// <param name="curveOffset">路线相对直线方向的最大摆动幅度。</param>
    /// <param name="color">路线节点颜色。</param>
    public static void DrawDottedCurve(
        RectTransform rectA,
        RectTransform rectB,
        Transform parent,
        Sprite sprite,
        Vector2 nodeSize,
        float spacing,
        float curveOffset,
        Color color,
        int branchIndex = 0,
        int branchCount = 1)
    {
        if (rectA == null || rectB == null || parent == null) return;

        if (sprite == null)
        {
            Debug.LogError("地图路线节点贴图未配置，无法绘制地图路线。");
            return;
        }

        Vector2 centerA = parent.InverseTransformPoint(rectA.position);
        Vector2 centerB = parent.InverseTransformPoint(rectB.position);
        Vector2 start = GetEdgePoint(centerA, rectA.rect.size, centerB);
        Vector2 end = GetEdgePoint(centerB, rectB.rect.size, centerA);

        DrawDottedCurve(start, end, parent, sprite, nodeSize, spacing, curveOffset, color, branchIndex, branchCount);
    }

    /// <summary>
    /// 沿一条房间连接边绘制虚线。
    /// </summary>
    /// <param name="start">路径起点，parent 局部坐标。</param>
    /// <param name="end">路径终点，parent 局部坐标。</param>
    /// <param name="parent">路线节点的 UI 父节点。</param>
    /// <param name="sprite">路线节点贴图。</param>
    /// <param name="nodeSize">每个路线节点的显示尺寸。</param>
    /// <param name="spacing">路线节点沿弧长的固定间距。</param>
    /// <param name="curveOffset">路线相对直线方向的最大摆动幅度。</param>
    /// <param name="color">路线节点颜色。</param>
    /// <param name="branchIndex">同一房间出口中的分支序号。</param>
    /// <param name="branchCount">同一房间出口数量。</param>
    public static void DrawDottedCurve(
        Vector2 start,
        Vector2 end,
        Transform parent,
        Sprite sprite,
        Vector2 nodeSize,
        float spacing,
        float curveOffset,
        Color color,
        int branchIndex = 0,
        int branchCount = 1)
    {
        if (parent == null) return;

        if (sprite == null)
        {
            Debug.LogError("地图路线节点贴图未配置，无法绘制地图路线。");
            return;
        }

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return;

        Vector2[] pathPoints = BuildHandDrawnPath(start, end, Mathf.Abs(curveOffset), branchIndex, branchCount);
        List<PathSample> samples = BuildArcLengthSamples(pathPoints);
        float pathLength = samples.Count > 0 ? samples[samples.Count - 1].Distance : 0f;
        float safeSpacing = Mathf.Max(1f, spacing);
        int dotCount = Mathf.Max(2, Mathf.FloorToInt(pathLength / safeSpacing));

        for (int i = 0; i <= dotCount; i++)
        {
            float distanceOnPath = Mathf.Min(i * safeSpacing, pathLength);
            float t = GetTAtDistance(samples, distanceOnPath);
            Vector2 position = EvaluatePath(pathPoints, t);
            Vector2 routeTangent = EvaluatePathTangent(pathPoints, t);

            GameObject dotObject = new GameObject("RouteDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dotObject.transform.SetParent(parent, false);

            Image image = dotObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            RectTransform rect = dotObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = nodeSize;

            float angle = Mathf.Atan2(routeTangent.y, routeTangent.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private static Vector2 GetEdgePoint(Vector2 center, Vector2 size, Vector2 target)
    {
        Vector2 direction = target - center;
        if (direction.sqrMagnitude <= 0.0001f) return center;

        direction.Normalize();
        Vector2 halfSize = size * 0.5f;
        float scaleX = Mathf.Abs(direction.x) > 0.0001f ? halfSize.x / Mathf.Abs(direction.x) : float.MaxValue;
        float scaleY = Mathf.Abs(direction.y) > 0.0001f ? halfSize.y / Mathf.Abs(direction.y) : float.MaxValue;
        return center + direction * Mathf.Min(scaleX, scaleY);
    }

    private static Vector2[] BuildHandDrawnPath(Vector2 start, Vector2 end, float curveOffset, int branchIndex, int branchCount)
    {
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return new[] { start, end };

        Vector2 tangent = delta.normalized;
        Vector2 normal = new Vector2(-tangent.y, tangent.x);

        int seed = Mathf.RoundToInt(start.x * 13f + start.y * 17f + end.x * 23f + end.y * 29f + branchIndex * 131f + branchCount * 197f);
        float distanceFactor = Mathf.InverseLerp(220f, 1050f, distance);
        float branchFactor = branchCount > 1 ? 0.78f : 1f;
        float horizontalFactor = Mathf.Clamp01(Mathf.Abs(delta.x) / Mathf.Max(1f, distance));
        float verticalDampen = Mathf.Lerp(0.14f, 0.46f, horizontalFactor);
        float branchSpread = branchCount > 1 ? branchIndex - (branchCount - 1) * 0.5f : 0f;
        float bendSign = Mathf.Abs(branchSpread) > 0.01f
            ? Mathf.Sign(branchSpread)
            : (Hash01(seed) > 0.5f ? 1f : -1f);
        float branchMagnitude = branchCount > 1
            ? Mathf.Lerp(0.45f, 1f, Mathf.Abs(branchSpread) / Mathf.Max(1f, (branchCount - 1) * 0.5f))
            : 1f;

        float maxBend = Mathf.Min(Mathf.Max(1f, curveOffset) * 1.55f, distance * 0.52f);
        float bend = maxBend * Mathf.Lerp(0.26f, 1.14f, distanceFactor) * branchFactor * branchMagnitude;
        Vector2 bendVector = normal * bendSign * bend;
        bendVector.y *= verticalDampen;

        float handleDistance = distance * Mathf.Lerp(0.38f, 0.58f, distanceFactor);
        Vector2 horizontalDrift = Vector2.right * ((Hash01(seed + 31) - 0.5f) * distance * Mathf.Lerp(0.08f, 0.28f, distanceFactor));
        Vector2 controlA = start + tangent * handleDistance + bendVector * Mathf.Lerp(0.68f, 1.18f, Hash01(seed + 11)) + horizontalDrift * 0.55f;
        Vector2 controlB = end - tangent * handleDistance + bendVector * Mathf.Lerp(0.68f, 1.18f, Hash01(seed + 23)) + horizontalDrift;

        const int sampleCount = 16;
        Vector2[] points = new Vector2[sampleCount + 1];
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            points[i] = EvaluateCubic(start, controlA, controlB, end, t);
        }

        return points;
    }

    private static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * oneMinusT * p0
            + 3f * oneMinusT * oneMinusT * t * p1
            + 3f * oneMinusT * t * t * p2
            + t * t * t * p3;
    }


    private static Vector2 EvaluatePath(Vector2[] points, float t)
    {
        GetSegment(points, t, out Vector2 p0, out Vector2 p1, out Vector2 p2, out Vector2 p3, out float localT);
        return EvaluateCatmullRom(p0, p1, p2, p3, localT);
    }

    private static Vector2 EvaluatePathTangent(Vector2[] points, float t)
    {
        GetSegment(points, t, out Vector2 p0, out Vector2 p1, out Vector2 p2, out Vector2 p3, out float localT);
        return EvaluateCatmullRomTangent(p0, p1, p2, p3, localT);
    }

    private static void GetSegment(Vector2[] points, float t, out Vector2 p0, out Vector2 p1, out Vector2 p2, out Vector2 p3, out float localT)
    {
        int segmentCount = Mathf.Max(1, points.Length - 1);
        float scaledT = Mathf.Clamp01(t) * segmentCount;
        int segment = Mathf.Min(Mathf.FloorToInt(scaledT), segmentCount - 1);
        localT = scaledT - segment;

        int i0 = Mathf.Max(0, segment - 1);
        int i1 = segment;
        int i2 = Mathf.Min(points.Length - 1, segment + 1);
        int i3 = Mathf.Min(points.Length - 1, segment + 2);

        p0 = points[i0];
        p1 = points[i1];
        p2 = points[i2];
        p3 = points[i3];
    }

    private static Vector2 EvaluateCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static Vector2 EvaluateCatmullRomTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * ((-p0 + p2)
            + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
            + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
    }

    private struct PathSample
    {
        public float T;
        public float Distance;

        public PathSample(float t, float distance)
        {
            T = t;
            Distance = distance;
        }
    }

    private static List<PathSample> BuildArcLengthSamples(Vector2[] points)
    {
        const int segments = 120;
        List<PathSample> samples = new List<PathSample>(segments + 1);
        float distance = 0f;
        Vector2 previous = EvaluatePath(points, 0f);
        samples.Add(new PathSample(0f, 0f));

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector2 current = EvaluatePath(points, t);
            distance += Vector2.Distance(previous, current);
            samples.Add(new PathSample(t, distance));
            previous = current;
        }

        return samples;
    }

    private static float GetTAtDistance(List<PathSample> samples, float targetDistance)
    {
        if (samples == null || samples.Count == 0) return 0f;
        if (targetDistance <= 0f) return 0f;

        PathSample last = samples[samples.Count - 1];
        if (targetDistance >= last.Distance) return 1f;

        for (int i = 1; i < samples.Count; i++)
        {
            PathSample current = samples[i];
            if (current.Distance < targetDistance) continue;

            PathSample previous = samples[i - 1];
            float segmentLength = current.Distance - previous.Distance;
            float lerp = segmentLength <= 0.001f ? 0f : (targetDistance - previous.Distance) / segmentLength;
            return Mathf.Lerp(previous.T, current.T, lerp);
        }

        return 1f;
    }

    private static float Hash01(int value)
    {
        float x = Mathf.Sin(value * 12.9898f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }
}
