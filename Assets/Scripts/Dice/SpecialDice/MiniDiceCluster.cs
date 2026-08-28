using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class MiniDiceCluster : MonoBehaviour
{
    public readonly List<PhysicsDice> members = new List<PhysicsDice>();

    public void ArrangeAt(Vector3 center, int columns, float columnSpacing, float rowSpacing, float duration)
    {
        members.RemoveAll(dice => dice == null);
        int count = members.Count;
        if (count == 0) return;

        int rows = Mathf.CeilToInt(count / (float)columns);
        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            int columnsInRow = Mathf.Min(columns, count - row * columns);
            float x = (column - (columnsInRow - 1) * 0.5f) * columnSpacing;
            float z = ((rows - 1) * 0.5f - row) * rowSpacing;

            PhysicsDice dice = members[i];
            dice.StopMotionAndSetKinematic(true);
            dice.transform.DOMove(center + new Vector3(x, 0f, z), duration).SetEase(Ease.OutQuad);
            dice.transform.DORotateQuaternion(dice.GetCurrentResultRotation(), duration).SetEase(Ease.OutQuad);
        }
    }
}
