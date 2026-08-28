using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 让多层 UI 背景跟随鼠标反方向轻微移动，制造简单视差。
/// 列表越靠后默认越接近前景，移动幅度越大。
/// </summary>
public class MouseParallaxUI : MonoBehaviour
{
    [Header("Layers")]
    [Tooltip("批量拖入背景层 RectTransform。列表越靠后，移动幅度越大。")]
    public List<RectTransform> backgroundLayers = new List<RectTransform>();

    [Header("Motion")]
    [Tooltip("最前景层在鼠标移到屏幕边缘时的最大偏移。")]
    public Vector2 nearLayerMovement = new Vector2(24f, 12f);
    [Tooltip("最远景层相对前景层的移动比例。")]
    [Range(0f, 1f)] public float farLayerMultiplier = 0.25f;
    [Tooltip("数值越大跟随越紧，越小越柔和。")]
    public float followSpeed = 3.5f;
    [Tooltip("等待入场动画结束后再开始捕获基础位置。")]
    public float startDelay = 0.8f;

    private readonly Dictionary<RectTransform, Vector2> _basePositions = new Dictionary<RectTransform, Vector2>();
    private float _enableTime;
    private bool _capturedBasePosition;

    private void OnEnable()
    {
        _enableTime = Time.time;
        _capturedBasePosition = false;
    }

    private void OnDisable()
    {
        RestoreBasePositions();
    }

    private void LateUpdate()
    {
        if (Time.time - _enableTime < Mathf.Max(0f, startDelay))
            return;

        if (!_capturedBasePosition)
        {
            CaptureBasePositions();
            _capturedBasePosition = true;
        }

        Vector2 mouseOffset = GetMouseOffsetFromCenter();
        float follow = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSpeed) * Time.deltaTime);

        for (int i = 0; i < backgroundLayers.Count; i++)
        {
            RectTransform layer = backgroundLayers[i];
            if (layer == null) continue;
            if (!_basePositions.TryGetValue(layer, out Vector2 basePosition)) continue;

            float depth = GetLayerDepth(i);
            Vector2 targetPosition = basePosition - new Vector2(mouseOffset.x * nearLayerMovement.x, mouseOffset.y * nearLayerMovement.y) * depth;
            layer.anchoredPosition = Vector2.Lerp(layer.anchoredPosition, targetPosition, follow);
        }
    }

    [ContextMenu("Capture Base Positions")]
    public void CaptureBasePositions()
    {
        _basePositions.Clear();
        foreach (RectTransform layer in backgroundLayers)
        {
            if (layer != null)
                _basePositions[layer] = layer.anchoredPosition;
        }
    }

    [ContextMenu("Restore Base Positions")]
    public void RestoreBasePositions()
    {
        foreach (KeyValuePair<RectTransform, Vector2> pair in _basePositions)
        {
            if (pair.Key != null)
                pair.Key.anchoredPosition = pair.Value;
        }
    }

    private float GetLayerDepth(int index)
    {
        if (backgroundLayers.Count <= 1)
            return 1f;

        float t = index / (float)(backgroundLayers.Count - 1);
        return Mathf.Lerp(farLayerMultiplier, 1f, t);
    }

    private Vector2 GetMouseOffsetFromCenter()
    {
        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);
        Vector2 mouse = Input.mousePosition;

        return new Vector2(
            Mathf.Clamp((mouse.x / width - 0.5f) * 2f, -1f, 1f),
            Mathf.Clamp((mouse.y / height - 0.5f) * 2f, -1f, 1f)
        );
    }
}
