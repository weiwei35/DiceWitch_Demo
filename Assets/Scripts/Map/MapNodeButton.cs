using UnityEngine;
using UnityEngine.UI;

public class MapNodeButton : MonoBehaviour
{
    [Header("UI References")]
    public Button button;
    public Image nodeIcon;      // 显示房间类型的图标
    public Image outlineImage;  // 显示选中/高亮的外框
    
    [Header("Colors")]
    public Color lockedColor = Color.gray;
    public Color availableColor = Color.white;
    public Color visitedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 半透明
    public Color completedColor = Color.green;

    private MapNode _nodeData;

    // 初始化方法
    public void Setup(MapNode node)
    {
        _nodeData = node;
        
        // 1. 设置外观 (根据房间类型)
        // 暂时只用颜色区分，等你有了美术资源再换 Sprite
        switch (node.roomType)
        {
            case Enum.RoomType.Battle: nodeIcon.color = Color.red; break;
            case Enum.RoomType.Boss: nodeIcon.color = new Color(0.5f, 0, 0); break; // 深红
            case Enum.RoomType.Event: nodeIcon.color = Color.magenta; break;
            case Enum.RoomType.Rest: nodeIcon.color = Color.yellow; break;
            case Enum.RoomType.Shop: nodeIcon.color = Color.blue; break;
            default: nodeIcon.color = Color.white; break;
        }

        // 2. 设置状态 (颜色 & 交互)
        UpdateStateVisuals();

        // 3. 绑定点击事件
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnNodeClicked);
    }

    public void UpdateStateVisuals()
    {
        if (_nodeData == null) return;

        switch (_nodeData.status)
        {
            case Enum.NodeStatus.Locked:
                outlineImage.color = lockedColor;
                button.interactable = false;
                break;
            case Enum.NodeStatus.Available:
                outlineImage.color = availableColor;
                button.interactable = true;
                // 这里可以用简单的 Animation 让它呼吸/闪烁
                break;
            case Enum.NodeStatus.Visited:
                outlineImage.color = visitedColor;
                button.interactable = false; 
                break;
            case Enum.NodeStatus.Completed:
                outlineImage.color = completedColor;
                button.interactable = false;
                break;
        }
    }

    private void OnNodeClicked()
    {
        if (_nodeData.roomDataRef != null)
        {
            MapManager.Instance.EnterNode(_nodeData);
            Debug.Log($"点击了节点: {_nodeData.roomType}, 加载数据: {_nodeData.roomDataRef.name}");
        }
        else
        {
            Debug.LogWarning($"点击了节点: {_nodeData.roomType}, 但没有数据！");
        }
    }
}