using UnityEngine;

public class TargetingArrow : MonoBehaviour
{
    public static TargetingArrow Instance; // 单例，方便调用

    public LineRenderer lineRenderer;
    public Transform arrowHead; // 拖入那个尖端 Sprite/Quad，没有就不拖
    public int segments = 20;   // 曲线的分段数，越多越平滑

    private void Awake()
    {
        Instance = this;
        Hide(); // 游戏开始时隐藏
    }

    public void Show(Vector3 startPos, Vector3 endPos)
    {
        gameObject.SetActive(true);
        DrawQuadraticBezierCurve(startPos, endPos);
        
        // 如果有箭头尖端，更新它的位置和旋转
        if (arrowHead != null)
        {
            arrowHead.position = endPos;
            arrowHead.LookAt(startPos + Vector3.up * 2); // 简单的朝向处理
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 二阶贝塞尔曲线算法
    void DrawQuadraticBezierCurve(Vector3 start, Vector3 end)
    {
        // 1. 计算控制点 (Control Point)
        // 我们取起点和终点的中点，然后往上抬高一点，形成拱形
        Vector3 midPoint = Vector3.Lerp(start, end, 0.5f);
        Vector3 controlPoint = midPoint + Vector3.up * 1.0f; // 3.0f 是拱起的高度，可调整

        lineRenderer.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            // 贝塞尔公式: B(t) = (1-t)^2 * P0 + 2(1-t)t * P1 + t^2 * P2
            Vector3 pixel = CalculateBezierPoint(t, start, controlPoint, end);
            lineRenderer.SetPosition(i, pixel);
        }
    }

    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        
        Vector3 p = uu * p0; 
        p += 2 * u * t * p1; 
        p += tt * p2; 
        
        return p;
    }
}