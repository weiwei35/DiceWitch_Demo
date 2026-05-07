using UnityEngine;

public class BattleState : IGameState
{
    private BattleRoomSO _roomData;

    public BattleState(BattleRoomSO roomData)
    {
        _roomData = roomData;
    }

    public void Enter()
    {
        GameFlowController.MapPanel?.SetActive(false);
        GameFlowController.RoomUIRoot?.SetActive(true);
        BattleManager.Instance.StartNewBattle(_roomData);
    }

    public void Exit() { }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
