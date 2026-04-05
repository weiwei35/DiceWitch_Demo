public interface IGameState
{
    // 进入该状态时执行
    void Enter(); 
    // 退出该状态时执行
    void Exit();  
    // 专门用来处理点击槽位的逻辑（因为你的游戏经常需要点来点去）
    void OnSlotClicked(MagicCircleSlot slotData, UnityEngine.Vector3 uiPos);
}