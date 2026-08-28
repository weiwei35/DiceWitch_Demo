using System;
using UnityEngine;

public class TargetSelectionState : IGameState
{
    private DiceAbilitySO _pendingSpell;
    private Action _onComplete;

    public TargetSelectionState(DiceAbilitySO spell, Action onComplete)
    {
        _pendingSpell = spell;
        _onComplete = onComplete;
    }

    public void Enter()
    {
        GameFlowController.SetBattleUIVisible(false);
        RewardDiceSelectionPanel panel = RewardDiceSelectionPanel.GetConfigured();
        if (panel == null)
        {
            CompleteFlow();
            return;
        }

        panel.Show(_pendingSpell, OnSlotSelected);
    }

    public void Exit()
    {
        RewardDiceSelectionPanel.Instance?.Hide();
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos)
    {
    }

    private void OnSlotSelected(MagicCircleSlot slotData)
    {
        if (!slotData.isUnlocked || slotData.currentDice == null) return;

        MagicCircleManager.Instance.ImprintAbilityToDice(slotData.currentDice, _pendingSpell);
        Debug.Log("附魔成功！");

        MagicCircleDisplay.Instance?.RefreshAll();
        CompleteFlow();
    }

    private void CompleteFlow()
    {
        if (_onComplete != null)
        {
            _onComplete.Invoke();
        }
        else
        {
            GameFlowController.Instance.ChangeState(new MapState());
        }
    }
}
