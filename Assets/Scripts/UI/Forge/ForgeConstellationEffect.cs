using UnityEngine;

/// <summary>
/// 世界空间的冥想星座连线效果。
/// 使用 LineRenderer、SpriteRenderer 和 ParticleSystem 生成可发光的折线、节点和星尘粒子。
/// </summary>
public class ForgeConstellationEffect : MonoBehaviour
{
    private const float SparkEmitInterval = 0.055f;
    private const int LineSortingOrder = 1;
    private const int ParticleSortingOrder = 2;
    private const int NodeSortingOrder = 3;

    private Camera _camera;
    private SegmentPiece[] _segments;
    private ParticleSystem _dustParticles;
    private SpriteRenderer _nodeRenderer;
    private Sprite _nodeSprite;
    private Vector2 _startScreen;
    private Vector2 _endScreen;
    private Vector2 _screenShake;
    private Vector3 _startWorld;
    private Vector3 _bendWorld;
    private Vector3 _endWorld;
    private float _depth;
    private float _particleSize;
    private float _particleSpread;
    private float _idleBendAmplitude;
    private float _idleBendSpeed;
    private float _idleBendPhase;
    private float _nodeSizePixels;
    private float _hdrIntensity;
    private Color _sparkColor;
    private float _progress;
    private float _sparkTimer;

    /// <summary>
    /// 创建一条世界空间星座连线效果。
    /// </summary>
    /// <param name="effectCamera">用于把屏幕坐标转换为世界坐标的相机。</param>
    /// <param name="parent">效果对象父节点，可为空。</param>
    /// <param name="startScreen">起点屏幕坐标。</param>
    /// <param name="bendScreen">折点屏幕坐标。</param>
    /// <param name="endScreen">终点屏幕坐标。</param>
    /// <param name="seed">随机种子，用于决定虚实线组合。</param>
    /// <param name="color">线段和粒子的基础颜色。</param>
    /// <param name="coreWidth">核心线段宽度。</param>
    /// <param name="particleSize">星尘粒子尺寸。</param>
    /// <param name="particleSpread">星尘粒子偏移范围。</param>
    /// <param name="hdrIntensity">HDR 颜色强度，用于配合后处理 Bloom。</param>
    /// <param name="nodeSprite">折点节点使用的贴图。</param>
    /// <param name="nodeSizePixels">折点节点期望屏幕尺寸。</param>
    /// <param name="depth">屏幕坐标转世界坐标时的相机深度。</param>
    /// <param name="objectName">生成对象名称。</param>
    /// <returns>创建出的星座效果；参数无效时返回 null。</returns>
    public static ForgeConstellationEffect Create(
        Camera effectCamera,
        Transform parent,
        Vector2 startScreen,
        Vector2 bendScreen,
        Vector2 endScreen,
        int seed,
        Color color,
        float coreWidth,
        float particleSize,
        float particleSpread,
        float hdrIntensity,
        Sprite nodeSprite,
        float nodeSizePixels,
        float depth,
        string objectName)
    {
        if (effectCamera == null) return null;
        if ((endScreen - startScreen).sqrMagnitude <= 0.01f) return null;

        GameObject rootObject = new GameObject(objectName, typeof(ForgeConstellationEffect));
        if (parent != null)
        {
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.SetAsFirstSibling();
        }

        ForgeConstellationEffect effect = rootObject.GetComponent<ForgeConstellationEffect>();
        effect._camera = effectCamera;
        effect._startScreen = startScreen;
        effect._endScreen = endScreen;
        effect._depth = Mathf.Max(0.01f, depth);
        effect._particleSize = Mathf.Max(0.001f, particleSize);
        effect._particleSpread = Mathf.Max(0f, particleSpread);
        effect._nodeSizePixels = Mathf.Max(1f, nodeSizePixels);
        effect._hdrIntensity = Mathf.Max(1f, hdrIntensity);
        effect._sparkColor = WithAlpha(color * effect._hdrIntensity, color.a);

        effect._startWorld = effect.ScreenToWorld(startScreen);
        effect._bendWorld = effect.ScreenToWorld(bendScreen);
        effect._endWorld = effect.ScreenToWorld(endScreen);
        effect.transform.position = effect._startWorld;

        Material coreMaterial = CreateAdditiveMaterial(color * effect._hdrIntensity);
        effect.CreateLinePattern(coreMaterial, coreWidth, WithAlpha(color * effect._hdrIntensity, color.a), seed);
        effect._dustParticles = effect.CreateDustParticles(color, particleSize, effect._hdrIntensity);
        effect._nodeRenderer = effect.CreateNodeSprite(nodeSprite, color, effect._hdrIntensity);
        effect.SetProgress(0f);
        return effect;
    }

    /// <summary>
    /// 每帧更新完整线段的待机摆动，并沿已显现路径发射星尘粒子。
    /// </summary>
    private void Update()
    {
        if (_progress <= 0.01f) return;

        UpdateLines();

        if (_dustParticles == null) return;

        _sparkTimer -= Time.unscaledDeltaTime;
        if (_sparkTimer > 0f) return;

        _sparkTimer = SparkEmitInterval;
        EmitSparkAlongVisiblePath();
    }

    /// <summary>
    /// 设置线段显现进度。
    /// </summary>
    /// <param name="progress">显现进度，0 为隐藏，1 为完整显示。</param>
    public void SetProgress(float progress)
    {
        _progress = Mathf.Clamp01(progress);
        UpdateLines();
    }

    /// <summary>
    /// 设置长按刻印时的屏幕空间抖动偏移。
    /// </summary>
    /// <param name="screenShake">屏幕像素单位的抖动偏移。</param>
    public void SetScreenShake(Vector2 screenShake)
    {
        _screenShake = screenShake;
        UpdateLines();
    }

    /// <summary>
    /// 根据当前进度、抖动和待机偏移刷新线段与节点。
    /// </summary>
    private void UpdateLines()
    {
        Vector3 start = _startWorld + ScreenDeltaToWorld(_screenShake);
        Vector3 bend = GetLiveBendWorld();
        Vector3 end = _endWorld + ScreenDeltaToWorld(_screenShake);

        float firstLength = Vector3.Distance(start, bend);
        float secondLength = Vector3.Distance(bend, end);
        float totalLength = firstLength + secondLength;
        float visibleLength = totalLength * _progress;

        UpdateLinePattern(start, bend, end, firstLength, visibleLength);
        UpdateNode(bend, firstLength, visibleLength);
    }

    /// <summary>
    /// 根据线段显现长度刷新折点节点的位置和可见性。
    /// </summary>
    /// <param name="bend">折点世界坐标。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="visibleLength">当前已经显现的路径长度。</param>
    private void UpdateNode(Vector3 bend, float firstLength, float visibleLength)
    {
        if (_nodeRenderer == null) return;

        bool reachedNode = visibleLength >= firstLength;
        _nodeRenderer.gameObject.SetActive(reachedNode);
        _nodeRenderer.transform.position = bend;
        FaceCamera(_nodeRenderer.transform);
    }

    /// <summary>
    /// 沿当前已经显现的路径随机发射一个星尘粒子。
    /// </summary>
    private void EmitSparkAlongVisiblePath()
    {
        Vector3 start = _startWorld + ScreenDeltaToWorld(_screenShake);
        Vector3 bend = GetLiveBendWorld();
        Vector3 end = _endWorld + ScreenDeltaToWorld(_screenShake);
        float t = Random.Range(0f, _progress);
        Vector3 position = GetPointAtProgress(start, bend, end, t) + ScreenDeltaToWorld(Random.insideUnitCircle * Mathf.Max(0f, _particleSpread));
        Vector3 tangent = GetTangentAtProgress(start, bend, end, t);
        Vector3 normal = Vector3.Cross(tangent, _camera.transform.forward).normalized;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = normal * Random.Range(-_particleSize * 2.5f, _particleSize * 2.5f),
            startLifetime = Random.Range(0.45f, 0.9f),
            startSize = _particleSize * Random.Range(0.75f, 1.65f),
            startColor = WithAlpha(_sparkColor, Random.Range(0.55f, 1f) * _sparkColor.a),
        };
        _dustParticles.Emit(emitParams, 1);
    }

    /// <summary>
    /// 获取当前帧实际使用的折点世界坐标。
    /// 完整刻印后的待机状态会在这里加入轻微浮动。
    /// </summary>
    /// <returns>当前折点世界坐标。</returns>
    private Vector3 GetLiveBendWorld()
    {
        Vector3 bend = _bendWorld + ScreenDeltaToWorld(_screenShake);
        if (_progress >= 0.99f && _idleBendAmplitude > 0f && _idleBendSpeed > 0f)
            bend += ScreenDeltaToWorld(Vector2.up * (Mathf.Sin(Time.unscaledTime * _idleBendSpeed + _idleBendPhase) * _idleBendAmplitude));
        return bend;
    }

    /// <summary>
    /// 获取折线路径上指定进度对应的世界坐标。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="progress">路径进度，0-1。</param>
    /// <returns>路径上的世界坐标。</returns>
    private Vector3 GetPointAtProgress(Vector3 start, Vector3 bend, Vector3 end, float progress)
    {
        float firstLength = Vector3.Distance(start, bend);
        float secondLength = Vector3.Distance(bend, end);
        float totalLength = firstLength + secondLength;
        float distance = totalLength * Mathf.Clamp01(progress);

        if (distance <= firstLength)
            return Vector3.Lerp(start, bend, distance / Mathf.Max(0.01f, firstLength));

        return Vector3.Lerp(bend, end, (distance - firstLength) / Mathf.Max(0.01f, secondLength));
    }

    /// <summary>
    /// 获取折线路径指定进度处的切线方向。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="progress">路径进度，0-1。</param>
    /// <returns>归一化切线方向。</returns>
    private Vector3 GetTangentAtProgress(Vector3 start, Vector3 bend, Vector3 end, float progress)
    {
        float firstLength = Vector3.Distance(start, bend);
        float secondLength = Vector3.Distance(bend, end);
        float totalLength = firstLength + secondLength;
        float distance = totalLength * Mathf.Clamp01(progress);

        Vector3 tangent = distance <= firstLength
            ? bend - start
            : end - bend;
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.right;
    }

    /// <summary>
    /// 创建一条用于星座线段或虚线点的 LineRenderer。
    /// </summary>
    /// <param name="objectName">生成对象名称。</param>
    /// <param name="material">线段材质。</param>
    /// <param name="width">线段宽度。</param>
    /// <param name="color">线段颜色。</param>
    /// <returns>配置完成的 LineRenderer。</returns>
    private LineRenderer CreateLineRenderer(string objectName, Material material, float width, Color color)
    {
        GameObject lineObject = new GameObject(objectName, typeof(LineRenderer));
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 3;
        line.widthMultiplier = Mathf.Max(0.001f, width);
        line.startColor = color;
        line.endColor = color;
        line.material = material;
        line.sortingOrder = LineSortingOrder;
        return line;
    }

    /// <summary>
    /// 创建两段线段的虚实组合。
    /// </summary>
    /// <param name="material">线段材质。</param>
    /// <param name="width">核心线宽。</param>
    /// <param name="color">线段颜色。</param>
    /// <param name="seed">随机种子。</param>
    private void CreateLinePattern(Material material, float width, Color color, int seed)
    {
        float firstLength = Vector3.Distance(_startWorld, _bendWorld);
        float secondLength = Vector3.Distance(_bendWorld, _endWorld);
        float totalLength = firstLength + secondLength;
        if (totalLength <= 0.01f) return;

        System.Random random = new System.Random(seed & int.MaxValue);
        bool firstDotted = random.Next(0, 2) == 0;
        _segments = new[]
        {
            CreateSegmentPattern(0f, firstLength, firstDotted, material, width, color, totalLength),
            CreateSegmentPattern(firstLength, totalLength, !firstDotted, material, width, color, totalLength)
        };
    }

    /// <summary>
    /// 创建一段实线或虚线段的数据和 Renderer。
    /// </summary>
    /// <param name="startDistance">该段在总路径上的起始距离。</param>
    /// <param name="endDistance">该段在总路径上的结束距离。</param>
    /// <param name="dotted">为 true 时创建虚线段，否则创建实线段。</param>
    /// <param name="material">线段材质。</param>
    /// <param name="width">核心线宽。</param>
    /// <param name="color">线段颜色。</param>
    /// <param name="totalLength">整条折线总长度。</param>
    /// <returns>线段片段数据。</returns>
    private SegmentPiece CreateSegmentPattern(float startDistance, float endDistance, bool dotted, Material material, float width, Color color, float totalLength)
    {
        var segment = new SegmentPiece
        {
            StartDistance = startDistance,
            EndDistance = endDistance,
            Dotted = dotted
        };

        if (!dotted)
        {
            segment.Renderer = CreateLineRenderer("ConstellationSolidSegment", material, width, color);
            return segment;
        }

        float dotWidth = width * 1.25f;
        float dotSpacing = Mathf.Max(width * 3.6f, totalLength * 0.028f);
        int dotCount = Mathf.Max(2, Mathf.FloorToInt((endDistance - startDistance) / dotSpacing) + 1);
        segment.DotDistances = new float[dotCount];
        segment.DotRenderers = new LineRenderer[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            float t = dotCount <= 1 ? 0f : i / (float)(dotCount - 1);
            segment.DotDistances[i] = Mathf.Lerp(startDistance, endDistance, t);
            LineRenderer dot = CreateLineRenderer("ConstellationDot", material, dotWidth, color);
            dot.numCapVertices = 5;
            segment.DotRenderers[i] = dot;
        }

        segment.DotWidth = dotWidth;
        return segment;
    }

    /// <summary>
    /// 根据当前显现长度刷新所有实线和虚线片段。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="visibleLength">当前已经显现的路径长度。</param>
    private void UpdateLinePattern(Vector3 start, Vector3 bend, Vector3 end, float firstLength, float visibleLength)
    {
        if (_segments == null) return;

        foreach (SegmentPiece segment in _segments)
        {
            if (segment == null) continue;

            if (segment.Dotted)
                UpdateDottedSegment(segment, start, bend, end, firstLength, visibleLength);
            else
                UpdateSolidSegment(segment, start, bend, end, firstLength, visibleLength);
        }
    }

    /// <summary>
    /// 刷新一段实线片段的端点和可见性。
    /// </summary>
    /// <param name="segment">要刷新的实线片段。</param>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="visibleLength">当前已经显现的路径长度。</param>
    private void UpdateSolidSegment(SegmentPiece segment, Vector3 start, Vector3 bend, Vector3 end, float firstLength, float visibleLength)
    {
        if (segment.Renderer == null) return;

        bool visible = visibleLength > segment.StartDistance + 0.001f;
        segment.Renderer.enabled = visible;
        if (!visible) return;

        float endDistance = Mathf.Min(segment.EndDistance, visibleLength);
        segment.Renderer.positionCount = 2;
        segment.Renderer.SetPosition(0, GetPointAtDistance(start, bend, end, firstLength, segment.StartDistance));
        segment.Renderer.SetPosition(1, GetPointAtDistance(start, bend, end, firstLength, endDistance));
    }

    /// <summary>
    /// 刷新一段虚线片段中每个短点的端点和可见性。
    /// </summary>
    /// <param name="segment">要刷新的虚线片段。</param>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="visibleLength">当前已经显现的路径长度。</param>
    private void UpdateDottedSegment(SegmentPiece segment, Vector3 start, Vector3 bend, Vector3 end, float firstLength, float visibleLength)
    {
        if (segment.DotRenderers == null || segment.DotDistances == null) return;

        for (int i = 0; i < segment.DotRenderers.Length; i++)
        {
            LineRenderer dot = segment.DotRenderers[i];
            if (dot == null) continue;

            float distance = segment.DotDistances[i];
            bool visible = visibleLength >= distance;
            dot.enabled = visible;
            if (!visible) continue;

            Vector3 point = GetPointAtDistance(start, bend, end, firstLength, distance);
            Vector3 tangent = GetTangentAtDistance(start, bend, end, firstLength, distance);
            Vector3 half = tangent * segment.DotWidth * 0.22f;
            dot.positionCount = 2;
            dot.SetPosition(0, point - half);
            dot.SetPosition(1, point + half);
        }
    }

    /// <summary>
    /// 获取折线路径上指定距离处的世界坐标。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="distance">从起点开始的路径距离。</param>
    /// <returns>路径上的世界坐标。</returns>
    private static Vector3 GetPointAtDistance(Vector3 start, Vector3 bend, Vector3 end, float firstLength, float distance)
    {
        if (distance <= firstLength)
            return Vector3.Lerp(start, bend, distance / Mathf.Max(0.01f, firstLength));

        float secondLength = Vector3.Distance(bend, end);
        return Vector3.Lerp(bend, end, (distance - firstLength) / Mathf.Max(0.01f, secondLength));
    }

    /// <summary>
    /// 获取折线路径上指定距离处的切线方向。
    /// </summary>
    /// <param name="start">路径起点。</param>
    /// <param name="bend">路径折点。</param>
    /// <param name="end">路径终点。</param>
    /// <param name="firstLength">第一段线段长度。</param>
    /// <param name="distance">从起点开始的路径距离。</param>
    /// <returns>归一化切线方向。</returns>
    private static Vector3 GetTangentAtDistance(Vector3 start, Vector3 bend, Vector3 end, float firstLength, float distance)
    {
        Vector3 tangent = distance <= firstLength ? bend - start : end - bend;
        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.right;
    }

    /// <summary>
    /// 创建沿星座线发射的星尘粒子系统。
    /// </summary>
    /// <param name="color">粒子基础颜色。</param>
    /// <param name="size">粒子基础尺寸。</param>
    /// <param name="hdrIntensity">HDR 颜色强度。</param>
    /// <returns>配置完成的粒子系统。</returns>
    private ParticleSystem CreateDustParticles(Color color, float size, float hdrIntensity)
    {
        GameObject particleObject = new GameObject("ConstellationDust", typeof(ParticleSystem));
        particleObject.transform.SetParent(transform, false);

        ParticleSystem particles = particleObject.GetComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(0.001f, size);
        main.startLifetime = 0.7f;
        main.startColor = WithAlpha(color * hdrIntensity, color.a);

        var emission = particles.emission;
        emission.enabled = false;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = CreateAdditiveMaterial(color * hdrIntensity);
        renderer.sortingOrder = ParticleSortingOrder;
        return particles;
    }

    /// <summary>
    /// 创建折点节点 SpriteRenderer。
    /// </summary>
    /// <param name="sprite">节点贴图。</param>
    /// <param name="color">节点基础颜色。</param>
    /// <param name="hdrIntensity">HDR 颜色强度。</param>
    /// <returns>配置完成的节点 Renderer；贴图为空时返回 null。</returns>
    private SpriteRenderer CreateNodeSprite(Sprite sprite, Color color, float hdrIntensity)
    {
        if (sprite == null) return null;

        GameObject nodeObject = new GameObject("ConstellationNode", typeof(SpriteRenderer));
        nodeObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = nodeObject.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = WithAlpha(color * (hdrIntensity * 0.9f), color.a);
        renderer.material = CreateAdditiveMaterial(color * hdrIntensity);
        renderer.sortingOrder = NodeSortingOrder;

        _nodeRenderer = renderer;
        _nodeSprite = sprite;
        ApplyNodeSize();
        nodeObject.SetActive(false);
        return renderer;
    }

    /// <summary>
    /// 设置节点期望屏幕尺寸，并重新计算世界缩放。
    /// </summary>
    /// <param name="nodeSizePixels">节点屏幕像素尺寸。</param>
    public void SetNodeSizePixels(float nodeSizePixels)
    {
        _nodeSizePixels = Mathf.Max(1f, nodeSizePixels);
        ApplyNodeSize();
    }

    /// <summary>
    /// 设置星尘粒子在路径附近的随机偏移范围。
    /// </summary>
    /// <param name="particleSpread">屏幕像素单位的偏移范围。</param>
    public void SetParticleSpread(float particleSpread)
    {
        _particleSpread = Mathf.Max(0f, particleSpread);
    }

    /// <summary>
    /// 设置完整刻印后折点的待机浮动参数。
    /// </summary>
    /// <param name="bendAmplitude">屏幕像素单位的浮动幅度。</param>
    /// <param name="bendSpeed">浮动速度。</param>
    /// <param name="phase">浮动相位。</param>
    public void SetIdleMotion(float bendAmplitude, float bendSpeed, float phase)
    {
        _idleBendAmplitude = Mathf.Max(0f, bendAmplitude);
        _idleBendSpeed = Mathf.Max(0f, bendSpeed);
        _idleBendPhase = phase;
    }

    /// <summary>
    /// 更新连线三个关键点的屏幕坐标，用于跟随 UI 图标和待机浮动。
    /// </summary>
    /// <param name="startScreen">起点屏幕坐标。</param>
    /// <param name="bendScreen">折点屏幕坐标。</param>
    /// <param name="endScreen">终点屏幕坐标。</param>
    public void SetScreenPoints(Vector2 startScreen, Vector2 bendScreen, Vector2 endScreen)
    {
        _startScreen = startScreen;
        _endScreen = endScreen;
        _startWorld = ScreenToWorld(startScreen);
        _bendWorld = ScreenToWorld(bendScreen);
        _endWorld = ScreenToWorld(endScreen);
        UpdateLines();
    }

    /// <summary>
    /// 将节点的屏幕像素尺寸转换为当前相机深度下的世界缩放。
    /// </summary>
    private void ApplyNodeSize()
    {
        if (_nodeRenderer == null || _nodeSprite == null) return;

        float targetWorldSize = ScreenDeltaToWorld(new Vector2(_nodeSizePixels, 0f)).magnitude;
        Vector2 spriteSize = _nodeSprite.bounds.size;
        float largestSpriteSide = Mathf.Max(0.0001f, Mathf.Max(spriteSize.x, spriteSize.y));
        float worldScale = targetWorldSize / largestSpriteSide;

        Transform parent = _nodeRenderer.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        _nodeRenderer.transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    /// <summary>
    /// 让指定 Transform 朝向效果相机，避免节点贴图歪斜。
    /// </summary>
    /// <param name="target">需要朝向相机的 Transform。</param>
    private void FaceCamera(Transform target)
    {
        if (target == null || _camera == null) return;
        target.rotation = _camera.transform.rotation;
    }

    /// <summary>
    /// 将屏幕坐标转换为效果相机深度上的世界坐标。
    /// </summary>
    /// <param name="screenPoint">屏幕坐标。</param>
    /// <returns>世界坐标。</returns>
    private Vector3 ScreenToWorld(Vector2 screenPoint)
    {
        return _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, _depth));
    }

    /// <summary>
    /// 将屏幕空间偏移转换为当前深度下的世界空间偏移。
    /// </summary>
    /// <param name="screenDelta">屏幕像素偏移。</param>
    /// <returns>世界空间偏移。</returns>
    private Vector3 ScreenDeltaToWorld(Vector2 screenDelta)
    {
        if (screenDelta == Vector2.zero) return Vector3.zero;

        Vector3 origin = _camera.ScreenToWorldPoint(new Vector3(0f, 0f, _depth));
        Vector3 offset = _camera.ScreenToWorldPoint(new Vector3(screenDelta.x, screenDelta.y, _depth));
        return offset - origin;
    }

    /// <summary>
    /// 创建用于 Bloom 的加法材质，并写入颜色。
    /// </summary>
    /// <param name="color">材质颜色。</param>
    /// <returns>配置完成的材质。</returns>
    private static Material CreateAdditiveMaterial(Color color)
    {
        Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = new Material(shader);
        ApplyMaterialColor(material, color);
        return material;
    }

    /// <summary>
    /// 将颜色写入材质支持的颜色属性。
    /// </summary>
    /// <param name="material">目标材质。</param>
    /// <param name="color">目标颜色。</param>
    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
    }

    /// <summary>
    /// 返回替换透明度后的颜色。
    /// </summary>
    /// <param name="color">原始颜色。</param>
    /// <param name="alpha">目标透明度。</param>
    /// <returns>设置透明度后的颜色。</returns>
    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    /// <summary>
    /// 星座线的一段路径片段，可以是实线或由多个短点组成的虚线。
    /// </summary>
    private class SegmentPiece
    {
        public LineRenderer Renderer;
        public LineRenderer[] DotRenderers;
        public float[] DotDistances;
        public float StartDistance;
        public float EndDistance;
        public float DotWidth;
        public bool Dotted;
    }
}
