using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/Ghost Dice")]
public class Ability_Ghost : DiceAbilitySO
{
    [Header("Ghost Settings")]
    public GameObject dicePrefab; // 骰子预制体 (通常就是它自己)
    public DiceDataSO diceData;   // 它自己的数据
    public Material ghostMaterial; // 一个半透明的材质球

    public void SpawnGhost(Vector3 spawnPos, DiceThrower thrower)
    {
        if (dicePrefab == null || thrower == null) return;

        // 1. 生成新骰子
        // 稍微抬高一点，防止穿模
        GameObject ghostObj = Instantiate(dicePrefab, spawnPos + Vector3.up * 0.5f, Random.rotation);
        
        // 2. 初始化数据
        PhysicsDice pDice = ghostObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
            pDice.Initialize(diceData);
            // 注册到管理器 (这样回合结束会被清理)
            thrower.RegisterDice(pDice);
        }

        // 3. 挂载幽灵行为组件
        GhostDiceBehavior ghostBehavior = ghostObj.AddComponent<GhostDiceBehavior>();
        ghostBehavior.EnterGhostMode(ghostMaterial);
    }
}