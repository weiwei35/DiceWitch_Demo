using UnityEngine;

public class EventState : IGameState
{
    private EventRoomSO _roomData;

    public EventState(EventRoomSO roomData)
    {
        _roomData = roomData;
    }

    public void Enter()
    {
        if (EventUIManager.Instance == null)
        {
            Debug.LogError("EventUIManager 未找到！");
            GameFlowController.Instance.ChangeState(new MapState());
            return;
        }

        GameFlowController.MapPanel?.SetActive(false);
        GameFlowController.RoomUIRoot?.SetActive(true);

        if (_roomData.possibleEvents.Count > 0)
        {
            int randomIndex = Random.Range(0, _roomData.possibleEvents.Count);
            RandomEventSO randomEvent = _roomData.possibleEvents[randomIndex];

            EventUIManager.Instance.ShowEvent(randomEvent, () =>
            {
                Debug.Log("事件处理完毕，状态机请求返回大地图。");
                GameFlowController.Instance.ChangeState(new MapState());
            });
        }
        else
        {
            Debug.LogWarning("这个事件房间没有配置任何事件！直接无事发生返回。");
            GameFlowController.Instance.ChangeState(new MapState());
        }
    }

    public void Exit() { }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
