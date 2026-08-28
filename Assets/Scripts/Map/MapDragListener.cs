using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


// =========================================================
// 监听器组件：挂在 ScrollRect 上，当玩家鼠标拖动时触发
// =========================================================
public class MapDragListener : MonoBehaviour, IBeginDragHandler
{
    public MapViewController mapUI;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (mapUI != null)
        {
            // 玩家一开始拖拽，立刻取消摄像机的自动跟随！
            mapUI.isAutoFollowing = false;
        }
    }
}
