using UnityEngine;
using System.Collections.Generic;

public class DiceThrower : MonoBehaviour
{
    [Header("Settings")]
    public GameObject dicePrefab; // 拖入你的骰子预制体
    public Transform spawnPoint;  // 蓝色托盘上方的生成点
    public float throwForce = 5f;
    public float torqueForce = 10f;

    // 这是一个动态列表，用来记录当前场上活着的所有骰子
    private List<PhysicsDice> activeDiceList = new List<PhysicsDice>();
    public void RegisterDice(PhysicsDice dice)
    {
        if (!activeDiceList.Contains(dice))
        {
            activeDiceList.Add(dice);
        }
    }
    // 修改方法：传入要生成几个骰子
    public void SpawnAndThrow(List<DiceDataSO> diceToSpawn)
    {
        // 1. 先清理上一回合剩下的骰子
        ClearOldDice();

        // 2. 生成新的
        // 遍历传入的数据列表
        foreach (var diceData in diceToSpawn)
        {
            // ... 生成位置逻辑保持不变 ...
            Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
            Vector3 spawnPos = spawnPoint.position + randomOffset;

            GameObject newDiceObj = Instantiate(dicePrefab, spawnPos, Random.rotation);
        
            PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
            if (pDice != null)
            {
                // ---> 关键变化：注入数据 <---
                pDice.Initialize(diceData); 
            
                activeDiceList.Add(pDice);
            
                // ... 扔出去的物理逻辑保持不变 ...
                Vector3 force = Vector3.down * 2f + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f)) * throwForce;
                Vector3 torque = Random.insideUnitSphere * torqueForce;
                pDice.Roll(force, torque);
            }
        }
    }

    // 清理逻辑
    public void ClearOldDice()
    {
        // 倒序遍历删除，比较安全
        for (int i = activeDiceList.Count - 1; i >= 0; i--)
        {
            if (activeDiceList[i] != null)
            {
                Destroy(activeDiceList[i].gameObject);
            }
        }
        activeDiceList.Clear();
        var allSquads = FindObjectsOfType<DiceSquadGroup>();
        foreach (var squad in allSquads)
        {
            Destroy(squad.gameObject);
        }
    }
}