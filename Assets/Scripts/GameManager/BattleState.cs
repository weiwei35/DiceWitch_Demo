using UnityEngine;

public class BattleState : IGameState
{
    private GameFlowController _flow;
    private BattleRoomSO _roomData;

    public BattleState(GameFlowController flow, BattleRoomSO roomData)
    {
        _flow = flow;
        _roomData = roomData;
    }

    public void Enter()
    {
        if (_flow._mapPanel) _flow._mapPanel.SetActive(false);
        if (_flow._roomUIRoot) _flow._roomUIRoot.SetActive(true);

        // 启动战斗
        BattleManager.Instance.StartNewBattle(_roomData);
    }

    public void Exit()
    {
        // 战斗结束离开时的清理可以在这里做
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
    }
}