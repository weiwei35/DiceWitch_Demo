using UnityEngine;

[RequireComponent(typeof(PhysicsDice))]
public class DiceHover : MonoBehaviour
{
    private PhysicsDice physicsDice;
    private DiceDragger dragger; 

    void Awake()
    {
        physicsDice = GetComponent<PhysicsDice>();
        dragger = GetComponent<DiceDragger>();
    }

    // 改名：供外部管理器调用
    public void OnManualMouseEnter()
    {
        // 1. 如果正在拖拽，不显示
        if (dragger != null && dragger.IsDragging) return;
        
        // 2. 如果还在滚动，不显示
        if (physicsDice.isRolling) return;

        // 3. 显示 Tips
        string header = physicsDice.GetDiceName();
        string content = physicsDice.GetFullDescription();
        
        // 假设 TooltipSystem 已经做好了单例
        TooltipSystem.Instance.Show(content, header);
        
        // 可选：高亮一下骰子模型，提示选中
        // transform.DOScale(originalScale * 1.1f, 0.1f);
    }

    public void OnManualMouseExit()
    {
        TooltipSystem.Instance.Hide();
        // transform.DOScale(originalScale, 0.1f);
    }
}