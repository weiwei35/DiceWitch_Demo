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
        GameFlowController.SetBattleUIVisible(true);
        BattleManager.Instance.StartNewBattle(_roomData);
    }

    public void Exit()
    {
        BattleManager.Instance?.ExitBattleGuide();
        GameFlowController.SetBattleUIVisible(false);
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
