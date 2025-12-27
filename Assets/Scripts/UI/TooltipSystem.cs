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
        
        // 2. 设置偏移量 (向右下偏移，防止鼠标遮住文字)
        float pivotOffsetX = 15f;
        float pivotOffsetY = -15f;

        // 3. 直接移动 Panel
        // 注意：如果你的 Canvas 是 Screen Space - Overlay，直接赋值 position 是最稳的
        panelRect.position = mousePos + new Vector2(pivotOffsetX, pivotOffsetY);

        // (进阶：防止跑出屏幕的代码可以以后加)
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
            tooltipText.text = $"<size=110%><b>{header}</b></size>\n<color=#CCCCCC>{content}</color>";
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
        tooltipPanel.SetActive(false);
    }
}