using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapNodeAnchor : MonoBehaviour
{
    [Header("节点配置")]
    public Enum.BoardNodeType nodeType = Enum.BoardNodeType.Empty;
    public int effectValue = 0; // 加血/扣血的具体数值[Tooltip("当前节点的主建筑图标（例如小房子、帐篷等）")]
    public Sprite baseIconSprite; // 【新增】让策划可以随意配置主建筑的贴图

    [Header("UI 表现 (子节点引用)")]
    public Image backgroundImage;     // 底图，用来显示已走过/未走过的颜色状态
    public Image baseIconImage;       // 【新增】主建筑的 Image 组件
    public Image effectIconImage;     // 加成状态的小图标 (原 iconImage)
    public TextMeshProUGUI valueText; // 节点下方的数值文本[Header("状态图标图集配置")]
    public Sprite healIcon;
    public Sprite trapIcon;
    public Sprite treasureIcon;
    public Sprite roomEventIcon;[Header("状态颜色配置 (进度)")]
    public Color passedColor = new Color(0.5f, 0.5f, 0.5f, 1f); 
    public Color currentColor = new Color(0.5f, 1f, 1f, 1f);    
    public Color futureColor = Color.white;                     

    public enum NodeState { Future, Current, Passed }
    [Header("当前运行状态")]
    public NodeState currentState = NodeState.Future;

    private void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            UpdateVisuals();
        };
#endif
    }

    public void UpdateVisuals()
    {
        // 0. 【新增】更新主建筑图标
        if (baseIconImage != null)
        {
            if (baseIconSprite != null)
            {
                baseIconImage.sprite = baseIconSprite;
                baseIconImage.gameObject.SetActive(true);
            }
            else
            {
                // 如果没有配置主建筑图标，可以选择隐藏
                baseIconImage.gameObject.SetActive(false);
            }
        }

        // 1. 更新数值文本
        if (valueText != null)
        {
            if (effectValue > 0)
            {
                valueText.text = effectValue.ToString();
                valueText.gameObject.SetActive(true);
            }
            else
            {
                valueText.text = "";
                valueText.gameObject.SetActive(false);
            }
        }

        // 2. 更新底部的【加成状态小图标】
        if (effectIconImage != null)
        {
            switch (nodeType)
            {
                case Enum.BoardNodeType.Heal:
                    effectIconImage.sprite = healIcon;
                    effectIconImage.gameObject.SetActive(true);
                    break;
                case Enum.BoardNodeType.Trap:
                    effectIconImage.sprite = trapIcon;
                    effectIconImage.gameObject.SetActive(true);
                    break;
                case Enum.BoardNodeType.Treasure:
                    effectIconImage.sprite = treasureIcon;
                    effectIconImage.gameObject.SetActive(true);
                    break;
                case Enum.BoardNodeType.RoomEvent:
                    effectIconImage.sprite = roomEventIcon;
                    effectIconImage.gameObject.SetActive(true);
                    break;
                case Enum.BoardNodeType.Empty:
                default:
                    effectIconImage.sprite = null;
                    effectIconImage.gameObject.SetActive(false); // 空地隐藏状态图标
                    break;
            }
        }

        // 3. 更新底图颜色（进度状态）
        if (backgroundImage != null)
        {
            switch (currentState)
            {
                case NodeState.Passed:
                    backgroundImage.color = passedColor;
                    break;
                case NodeState.Current:
                    backgroundImage.color = currentColor;
                    break;
                case NodeState.Future:
                    backgroundImage.color = futureColor;
                    break;
            }
        }
    }

    public void SetState(NodeState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    public void OnNodeClicked()
    {
        Debug.Log($"<color=#00FF00>点击了地图节点</color> 类型: {nodeType}, 数值: {effectValue}");
    }
}