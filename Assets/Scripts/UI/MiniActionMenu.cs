using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MiniActionMenu : MonoBehaviour
{
    public float verticalOffset = 150f; // 建议设大一点，比如100-200像素
    public Button upgradeButton;
    public TextMeshProUGUI upgradeCostText;
    
    public Button enchantButton;
    public TextMeshProUGUI enchantCostText;

    private MagicCircleSlot _currentSlot;

    void Start()
    {
        // 绑定按钮事件
        upgradeButton.onClick.AddListener(() => {
            if (_currentSlot != null) // 加个安全检查
            {
                GameFlowController.Instance.UpgradeSlotAttribute(_currentSlot);
                Close();
            }
        });

        enchantButton.onClick.AddListener(() => {
            if (_currentSlot != null)
            {
                GameFlowController.Instance.StartAttributeEnchantProcess(_currentSlot);
                Close();
            }
        });
        
        // 假设这个脚本挂在一个铺满全屏的透明 Button 上用来点击关闭
        Button bgButton = GetComponent<Button>();
        if (bgButton != null)
        {
            bgButton.onClick.AddListener(Close);
        }
    }

    public void Show(MagicCircleSlot slot, Vector3 anchorPos)
    {
        // --- 【关键修复】 ---
        _currentSlot = slot; 
        // ------------------

        // 1. 世界坐标对齐
        transform.position = anchorPos;

        // 2. 锚点坐标偏移 (像素单位)
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 currentAnchored = rect.anchoredPosition;
            // 确保只修改 Y 轴偏移，保持 X 轴相对位置
            rect.anchoredPosition = new Vector2(currentAnchored.x, currentAnchored.y + verticalOffset);
        }
        
        transform.SetAsLastSibling();
        
        // 更新 UI
        upgradeCostText.text = "5"; 
        enchantCostText.text = "10";

        // 逻辑：如果有属性显示升级，没属性显示附魔？
        // 或者两者都显示，看你设计。通常有了属性也可以重新附魔(替换)
        // 这里假设：有属性才能升级，附魔按钮一直都在(用于注入或替换)
        bool hasAttribute = (slot.currentAttribute != null && slot.currentAttribute.data != null);
        
        upgradeButton.gameObject.SetActive(hasAttribute);
        enchantButton.gameObject.SetActive(true); 

        gameObject.SetActive(true);
        
        // 获取当前资源
        int currentDust = PlayerProgressionManager.Instance.manaDust;
        int upgradeCost = 5; // 建议设为常量
        int enchantCost = 10;

        // 控制按钮是否可点
        if (hasAttribute)
        {
            upgradeButton.interactable = (currentDust >= upgradeCost);
            // 如果钱不够，可以把文字变红
            upgradeCostText.color = (currentDust >= upgradeCost) ? Color.white : Color.red;
        }

        enchantButton.interactable = (currentDust >= enchantCost);
        enchantCostText.color = (currentDust >= enchantCost) ? Color.white : Color.red;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        _currentSlot = null; // 关闭时清理引用
    }
}