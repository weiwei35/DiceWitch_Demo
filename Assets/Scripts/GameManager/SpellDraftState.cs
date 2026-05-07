using System;
using UnityEngine;

public class SpellDraftState : IGameState
{
    private Action _onComplete;

    public SpellDraftState(Action onComplete)
    {
        _onComplete = onComplete;
    }

    public void Enter()
    {
        SpellDraftPanel.Instance.OnSpellSelected = OnSpellSelected;
        SpellDraftPanel.Instance.ShowDraft();
    }

    public void Exit() { SpellDraftPanel.Instance.Hide(); }

    private void OnSpellSelected(DiceAbilitySO selectedSpell)
    {
        GameFlowController.Instance.ChangeState(new TargetSelectionState(selectedSpell, _onComplete));
    }

    public void OnSlotClicked(MagicCircleSlot slotData, Vector3 uiPos) { }
}
