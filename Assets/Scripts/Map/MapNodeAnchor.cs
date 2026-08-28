using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MapNodeAnchor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("节点配置")]
    public GameEnums.BoardNodeType nodeType = GameEnums.BoardNodeType.空;
    public int effectValue = 0;
    [Tooltip("锻造节点必填。锻造一定会附带一个额外节点效果。")]
    public GameEnums.BoardNodeType forgeBonusType = GameEnums.BoardNodeType.空;

    [Header("节点已抵达背景")]
    [Tooltip("填写地图节点背景文件夹中的 Sprite 名称，不包含 .png。起点等无需背景的节点可留空。")]
    public string passedBackgroundSpriteName;

    [Header("UI 表现 (子节点引用)")]
    public Image baseIconImage;
    [Tooltip("节点预制体中的房间边框。贴图与显隐仍由地图表现配置决定。")]
    public Image frameImage;
    public Image effectIconImage;
    public TextMeshProUGUI valueText;
    public Image passedBackgroundImage;

    private RoomDataSO _roomData;
    private MapPresentationCatalogSO _presentationCatalog;
    private Sprite _passedBackgroundSprite;

    // 锻造行 UI 元素（自动查找子物体 ForgeRow/Icon、ForgeRow/Text）
    public Image _forgeIconImage;


    public enum NodeState { Future, Current, Passed, Disabled }
    [Header("当前运行状态")]
    public NodeState currentState = NodeState.Future;

    public void UpdateVisuals()
    {
        // 0. 主图标由房间类型 + 运行状态决定，不再在单个节点上手动配置。
        if (baseIconImage != null)
        {
            Sprite roomIcon = _presentationCatalog != null ? _presentationCatalog.GetRoomStateSprite(_roomData, currentState) : null;
            if (roomIcon != null)
            {
                baseIconImage.sprite = roomIcon;
                baseIconImage.gameObject.SetActive(true);
            }
            else
            {
                baseIconImage.sprite = null;
                baseIconImage.gameObject.SetActive(false);
                Debug.LogError($"地图房间节点缺少状态贴图: room={GetRoomDebugName()}, state={currentState}", this);
            }
        }

        if (frameImage != null)
        {
            Sprite frameSprite = _presentationCatalog != null ? _presentationCatalog.GetRoomFrameSprite(_roomData) : null;
            if (frameSprite != null)
            {
                frameImage.sprite = frameSprite;
                frameImage.gameObject.SetActive(true);
            }
            else
            {
                frameImage.sprite = null;
                frameImage.gameObject.SetActive(false);
            }
        }

        bool isNodeActive = (currentState != NodeState.Disabled);

        if (passedBackgroundImage != null)
        {
            bool showPassedBackground = isNodeActive
                && (currentState == NodeState.Current || currentState == NodeState.Passed)
                && _passedBackgroundSprite != null;

            if (showPassedBackground)
            {
                if (passedBackgroundImage.sprite != _passedBackgroundSprite)
                {
                    passedBackgroundImage.sprite = _passedBackgroundSprite;
                }

                passedBackgroundImage.gameObject.SetActive(true);
            }
            else
            {
                passedBackgroundImage.sprite = null;
                passedBackgroundImage.gameObject.SetActive(false);
            }
        }

        // 1. 加成行：效果图标 + 数值
        //    锻造节点的加成效果由 forgeBonusType 决定
        GameEnums.BoardNodeType effectiveType = nodeType;
        if (nodeType == GameEnums.BoardNodeType.锻造 && forgeBonusType != GameEnums.BoardNodeType.空)
            effectiveType = forgeBonusType;

        bool showEffectRow = isNodeActive && effectiveType != GameEnums.BoardNodeType.空 && effectiveType != GameEnums.BoardNodeType.锻造;
        bool hasValue = effectValue != 0;

        if (effectIconImage != null)
        {
            if (showEffectRow && _presentationCatalog != null)
            {
                effectIconImage.sprite = _presentationCatalog.GetNodeEffectIcon(effectiveType, effectValue);
                effectIconImage.gameObject.SetActive(true);
            }
            else
            {
                effectIconImage.sprite = null;
                effectIconImage.gameObject.SetActive(false);
            }
        }

        if (valueText != null)
        {
            if (showEffectRow && hasValue && _presentationCatalog != null && _presentationCatalog.ShouldShowNodeValue(effectiveType, effectValue))
            {
                valueText.text = _presentationCatalog.FormatNodeValue(effectiveType, effectValue);
                valueText.gameObject.SetActive(true);
            }
            else
            {
                valueText.text = "";
                valueText.gameObject.SetActive(false);
            }
        }

        // 2. 锻造行 (仅 Forge 节点显示)
        bool showForgeRow = isNodeActive && nodeType == GameEnums.BoardNodeType.锻造;
        if (_forgeIconImage != null)
        {
            if (showForgeRow && _presentationCatalog != null)
            {
                _forgeIconImage.sprite = _presentationCatalog.forgeIcon;
                _forgeIconImage.gameObject.SetActive(true);
            }
            else
            {
                _forgeIconImage.sprite = null;
                _forgeIconImage.gameObject.SetActive(false);
            }
        }
    }

    public void SetState(NodeState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    public void SetPresentationContext(
        RoomDataSO roomData,
        MapPresentationCatalogSO presentationCatalog,
        Sprite passedBackgroundSprite = null)
    {
        _roomData = roomData;
        _presentationCatalog = presentationCatalog;
        _passedBackgroundSprite = passedBackgroundSprite;
        UpdateVisuals();
    }

    private string GetRoomDebugName()
    {
        if (_roomData == null) return "null";

        string roomName = string.IsNullOrEmpty(_roomData.roomName) ? _roomData.name : _roomData.roomName;
        return $"{roomName} ({_roomData.roomType})";
    }

    public void OnNodeClicked()
    {
        Debug.Log($"<color=#00FF00>点击了地图节点</color> 类型: {nodeType}, 数值: {effectValue}");
    }

    // =========================================================
    // Tooltip
    // =========================================================
    private void GetTooltipInfo(out string header, out string content)
    {
        if (currentState == NodeState.Disabled)
        {
            header = "已失效";
            content = AppendRoomInfo("<color=#888888>该路线已废弃，无法触发任何效果。</color>");
            return;
        }

        if (_presentationCatalog == null)
        {
            header = nodeType.ToString();
            content = AppendRoomInfo("地图表现配置未设置");
            return;
        }

        if (nodeType == GameEnums.BoardNodeType.锻造)
        {
            header = "锻造熔炉";
            content = "<color=#FF8800>可以为骰子刻印词条</color>";
            if (forgeBonusType != GameEnums.BoardNodeType.空)
                content += "\n" + _presentationCatalog.BuildNodeTooltip(forgeBonusType, effectValue);
        }
        else
        {
            header = _presentationCatalog.GetNodeTooltipHeader(nodeType);
            content = _presentationCatalog.BuildNodeTooltip(nodeType, effectValue);
        }

        content = AppendRoomInfo(content);
    }

    private string AppendRoomInfo(string content)
    {
        if (_roomData == null) return content;

        string roomName = string.IsNullOrEmpty(_roomData.roomName) ? "未命名房间" : _roomData.roomName;
        string roomType = _presentationCatalog != null ? _presentationCatalog.GetRoomDisplayName(_roomData.roomType) : _roomData.roomType.ToString();
        return $"{content}\n\n房间: {roomName}\n类别: {roomType}";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            GetTooltipInfo(out string header, out string content);
            TooltipSystem.Instance.Show(content, header);
            transform.localScale = Vector3.one * 1.1f;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipSystem.Instance != null)
        {
            TooltipSystem.Instance.Hide();
            transform.localScale = Vector3.one;
        }
    }
}
