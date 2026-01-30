using UnityEngine;
using UnityEngine.UI;

public class DiceViewMonitor : MonoBehaviour
{
    public static DiceViewMonitor Instance;

    [Header("References")]
    public Camera diceCamera;      // 拖入那个拍骰子的 DiceCamera
    public RawImage rawImage;      // 拖入自己 (UI_DiceView)
    public RectTransform rectTrans; // 拖入自己的 RectTransform

    void Awake()
    {
        Instance = this;
        if (rawImage == null) rawImage = GetComponent<RawImage>();
        if (rectTrans == null) rectTrans = GetComponent<RectTransform>();
    }

    // --- 核心数学：屏幕坐标 -> 骰子世界射线 ---
    public Ray GetDiceRay(Vector2 screenPos)
    {
        Vector2 localPoint;

        // --- 核心修改 ---
        // 1. 获取渲染 UI 的摄像机
        // 如果你的 Canvas Render Mode 是 Screen Space - Camera，这里必须传对应的相机
        Camera uiCamera = Camera.main; 
        
        // 2. 将第三个参数从 null 改为 uiCamera
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTrans, screenPos, uiCamera, out localPoint))
        {
            return new Ray(); 
        }
        // ----------------

        // 下面的逻辑保持不变
        float normalizedX = (localPoint.x / rectTrans.rect.width) + 0.5f;
        float normalizedY = (localPoint.y / rectTrans.rect.height) + 0.5f;

        return diceCamera.ViewportPointToRay(new Vector3(normalizedX, normalizedY, 0));
    }
    
    // 转换为屏幕像素坐标
    public Vector3 GetScreenPosFromDice3D(Vector3 diceWorldPos)
    {
        // 1. 世界坐标 -> 视口坐标 (0 ~ 1)
        // 比如骰子在相机视野正中间，这里返回 (0.5, 0.5)
        Vector3 viewportPos = diceCamera.WorldToViewportPoint(diceWorldPos);

        // 2. 获取 RawImage 在屏幕上的四个角
        // corners[0]=左下, [1]=左上, [2]=右上, [3]=右下
        Vector3[] corners = new Vector3[4];
        rectTrans.GetWorldCorners(corners);

        // 3. 根据视口比例，插值计算出实际的屏幕坐标
        // 既然 diceCamera 把骰子渲染到了 RawImage 上，那骰子在 RawImage 里的相对位置就等于 viewportPos
        float screenX = Mathf.Lerp(corners[0].x, corners[2].x, viewportPos.x);
        float screenY = Mathf.Lerp(corners[0].y, corners[2].y, viewportPos.y);

        return new Vector3(screenX, screenY, 0);
    }
}