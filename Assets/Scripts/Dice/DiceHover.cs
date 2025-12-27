using UnityEngine;

[RequireComponent(typeof(PhysicsDice))]
public class DiceHover : MonoBehaviour
{
    private PhysicsDice physicsDice;
    private DiceDragger dragger; // 引用拖拽脚本

    void Awake()
    {
        physicsDice = GetComponent<PhysicsDice>();
        dragger = GetComponent<DiceDragger>();
    }

    void OnMouseEnter()
    {
        // 1. 如果正在拖拽这个骰子，就不显示Tips了，挡视线
        if (dragger != null && dragger.IsDragging) return;
        
        // 2. 如果骰子还在滚动，也不显示
        if (physicsDice.isRolling) return;

        // 3. 获取信息并显示
        string header = physicsDice.GetDiceName();
        string content = physicsDice.GetFullDescription();
        
        TooltipSystem.Instance.Show(content, header);
    }

    void OnMouseExit()
    {
        TooltipSystem.Instance.Hide();
    }

    void OnMouseDown()
    {
        // 一旦开始点击/拖拽，立刻隐藏
        TooltipSystem.Instance.Hide();
    }
}