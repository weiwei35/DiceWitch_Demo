using UnityEngine;

public class EventState : IGameState
{
    private GameFlowController _flow;
    private EventRoomSO _roomData;

    // 构造函数：接收控制器和房间数据
    public EventState(GameFlowController flow, EventRoomSO roomData)
    {
        _flow = flow;
        _roomData = roomData;
    }

    public void Enter()
    {
        // 1. 防空检查
        if (_flow._eventUIManager == null)
        {
            Debug.LogError("GameFlowController 中未绑定 EventUIManager！");
            _flow.ChangeState(new MapState(_flow));
            return;
        }

        // 2. 界面显隐控制
        if (_flow._mapPanel) _flow._mapPanel.SetActive(false);
        if (_flow._roomUIRoot) _flow._roomUIRoot.SetActive(true);

        // 3. 业务逻辑：从事件房间抽取事件
        if (_roomData.possibleEvents.Count > 0)
        {
            int randomIndex = Random.Range(0, _roomData.possibleEvents.Count);
            RandomEventSO randomEvent = _roomData.possibleEvents[randomIndex];

            // 呼出事件 UI，并传入回调函数 (Lambda 表达式)
            _flow._eventUIManager.ShowEvent(randomEvent, () => 
            {
                Debug.Log("事件处理完毕，状态机请求返回大地图。");
                // 事件结束，切换回地图状态
                _flow.ChangeState(new MapState(_flow)); 
            });
        }
        else
        {
            Debug.LogWarning("这个事件房间没有配置任何事件！直接无事发生返回。");
            _flow.ChangeState(new MapState(_flow));
        }
    }

    public void Exit()
    {
        // 事件状态退出时，如果需要关闭特定UI，可以在这里处理
        // (由于EventUIManager内部可能自己处理了隐藏，这里可以留空)
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
    }
}