using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Abilities/Clone Dice")]
public class Ability_Clone : DiceAbilitySO
{
    [Header("Clone Settings")]
    public GameObject minionDicePrefab; // 1点小骰子的预制体
    public DiceDataSO minionDiceData;   // 小骰子的数据 (6面全是1)

    // 我们在 OnRollEnd 里触发分裂
    public override int OnRollEnd(int primitiveValue)
    {
        // primitiveValue 就是点数 X
        int count = primitiveValue;
        
        // 找到当前那个大骰子 (这里需要一点技巧来获取当前上下文)
        // 由于 DiceAbilitySO 是单例资源，不能存状态。
        // 我们最好修改 PhysicsDice，让它在调用 OnRollEnd 时把自己传进来，或者我们用一种变通方法。
        
        // 变通：我们在这里无法直接销毁骰子，因为 OnRollEnd 只返回 int。
        // 建议：在 PhysicsDice 里加一个特殊判断，或者修改 OnRollEnd 的签名。
        
        return primitiveValue;
    }
    
    // 【更好的方案】新增一个钩子：OnRollFinished(PhysicsDice source)
    // 请去 DiceAbilitySO.cs 加这个虚方法
    public override void OnRollFinished(PhysicsDice sourceDice)
    {
        DiceThrower thrower = FindObjectOfType<DiceThrower>();
        // 只有点数 > 1 才分裂，不然没意义
        int count = sourceDice.currentResultData.value;
        if (count <= 1) return;

        Debug.Log($"分身骰触发！分裂成 {count} 个！");

        // 1. 创建小队控制器
        GameObject groupObj = new GameObject($"Squad_{sourceDice.name}");
        DiceSquadGroup squad = groupObj.AddComponent<DiceSquadGroup>();

        List<DiceDragger> spawnedMinions = new List<DiceDragger>();
        Vector3 centerPos = sourceDice.transform.position;

        // 2. 生成 X 个小骰子
        for (int i = 0; i < count; i++)
        {
            // 随机散落在周围
            Vector3 spawnPos = centerPos + Random.insideUnitSphere * 0.1f;
            spawnPos.y = centerPos.y; // 保持在桌面上

            GameObject minion = Instantiate(minionDicePrefab, spawnPos, Random.rotation);
            
            // 初始化小骰子
            PhysicsDice pDice = minion.GetComponent<PhysicsDice>();
            pDice.Initialize(minionDiceData);
            // 强制设为1点（虽然Data里已经是1，但为了保险）
            pDice.ForceSetValue(0); 
            if (thrower != null)
            {
                thrower.RegisterDice(pDice);
            }
            spawnedMinions.Add(minion.GetComponent<DiceDragger>());
        }

        // 3. 编队
        squad.Initialize(spawnedMinions);

        // 4. 销毁本体 (大骰子)
        Destroy(sourceDice.gameObject);
    }
}