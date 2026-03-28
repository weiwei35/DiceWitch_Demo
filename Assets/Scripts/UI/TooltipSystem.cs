using UnityEngine;
using UnityEngine.UI; // 引用 LayoutRebuilder
using TMPro;

public class TooltipSystem : MonoBehaviour
{
    public static TooltipSystem Instance;

    [Header("UI References")]
    public GameObject tooltipPanel;       // 拖入你的 Panel
    public TextMeshProUGUI tooltipText;   // 拖入 Panel 下的 Text
    public RectTransform panelRect;       // 拖入 Panel 的 RectTransform

    void Awake()
    {
        Instance = this;
        Hide();
    }

    void Update()
    {
        // 只有显示的时候才计算位置
        if (tooltipPanel.activeSelf)
        {
            UpdatePosition();
        }
    }

    private void UpdatePosition()
    {
        // 1. 获取鼠标位置
        Vector2 mousePos = Input.mousePosition;
        
        // 2. 转换为 Canvas 内的坐标
        // 你的 TooltipPanel 的父物体应该是 Canvas 或者某个全屏 Panel
        RectTransform parentRect = panelRect.parent as RectTransform;
        
        Vector2 localPoint;
        // 注意：这里必须传 uiCamera (MainCamera)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, Camera.main, out localPoint))
        {
            // 3. 设置偏移量 (在局部坐标系下)
            // Camera 模式下单位比较小，偏移量也要改小
            float pivotOffsetX = 20f; 
            float pivotOffsetY = -20f;

            // 4. 赋值局部坐标
            panelRect.localPosition = localPoint + new Vector2(pivotOffsetX, pivotOffsetY);
        }
    }

    public void Show(string content, string header = "")
    {
        // 1. 设置文字
        if (string.IsNullOrEmpty(header))
        {
            tooltipText.text = content;
        }
        else
        {
            tooltipText.text = $"<size=110%><b>{header}</b></size>\n<color=#000000>{content}</color>";
        }

        // 2. 激活物体
        tooltipPanel.SetActive(true);

        // 3. 【关键】强制刷新布局！
        // 这一步解决了“背景框不随文字变大”的问题
        // 有时候需要刷新两层：先刷新文字大小，再刷新背景大小
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    public void Hide()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }
}