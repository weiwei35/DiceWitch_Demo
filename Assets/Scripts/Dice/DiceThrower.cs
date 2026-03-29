using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class DiceThrower : MonoBehaviour
{
    [Header("Settings")]
    public GameObject dicePrefab; // 拖入你的骰子预制体
    public Transform spawnPoint;  // 蓝色托盘上方的生成点
    public float throwForce = 5f;
    public float torqueForce = 10f;
    
    private Transform _container; // 父物体容器
    
    private List<PhysicsDice> _highlightedDiceList = new List<PhysicsDice>();
    private Vector3 _originalScale;
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
    public void SpawnAndThrow(List<BattleDiceEntry> diceEntries)
    {
        ClearOldDice();
        if (_container == null) _container = new GameObject("--- Dice Container ---").transform;

        // =========================================================
        // 【新增】提取作弊点数，并随机决定对哪一颗骰子下手
        // =========================================================
        int fixedDiceValue = PlayerManager.Instance.nextBattleFixedDiceValue;
        PlayerManager.Instance.nextBattleFixedDiceValue = 0; // 提取后清空

        int cheatIndex = -1;
        if (fixedDiceValue > 0 && diceEntries.Count > 0)
        {
            cheatIndex = Random.Range(0, diceEntries.Count); // 随机选一颗
        }

        for (int i = 0; i < diceEntries.Count; i++)
        {
            var entry = diceEntries[i];
            Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
            Vector3 spawnPos = spawnPoint.position + randomOffset;

            GameObject newDiceObj = Instantiate(dicePrefab, spawnPos, Random.rotation);
            newDiceObj.transform.SetParent(_container);
        
            PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
            if (pDice != null)
            {
                pDice.Initialize(entry.combatData, entry.sourceRef); 
                activeDiceList.Add(pDice);
            
                // =========================================================
                // 【新增】如果这是被选中的作弊骰子，调用作弊逻辑
                // =========================================================
                if (i == cheatIndex)
                {
                    Debug.Log($"<color=magenta>【命运羁绊】这颗骰子 ({pDice.name}) 必定会掷出 {fixedDiceValue}！</color>");
                    
                    // 【任务】：你需要在你的 PhysicsDice.cs 脚本中，实现并调用一个类似 SetCheatFace() 的方法。
                    // pDice.SetCheatFace(fixedDiceValue); 
                }

                Vector3 force = Vector3.down * 2f + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f)) * throwForce;
                Vector3 torque = Random.insideUnitSphere * torqueForce;
                pDice.Roll(force, torque);
            }
        }
    }
    // 【新增】为了方便 UI 管理器调用，提取一个生成单个骰子的方法
    // isGhostSpawn: 仅仅是为了逻辑区分，目前逻辑一样
    public PhysicsDice SpawnSingleDice(DiceDataSO data, PlayerDice sourceRef = null)
    {
        Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
        Vector3 spawnPos = spawnPoint.position + randomOffset;

        // 如果是幽灵复活，建议生成点抬高一点，或者加点随机偏移，避免和还没销毁的骰子撞在一起
        if (sourceRef == null) spawnPos += Vector3.up * 2.0f; 

        GameObject newDiceObj = Instantiate(dicePrefab, spawnPos, Random.rotation);
        
        if (_container != null) newDiceObj.transform.SetParent(_container);
    
        PhysicsDice pDice = newDiceObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
            pDice.Initialize(data, sourceRef); 
            activeDiceList.Add(pDice);
            
            Vector3 force = Vector3.down * 2f + new Vector3(Random.Range(-1f,1f), 0, Random.Range(-1f,1f)) * throwForce;
            Vector3 torque = Random.insideUnitSphere * torqueForce;
            pDice.Roll(force, torque);

            return pDice; // 【新增】返回它
        }
        return null;
    }
    // 清理逻辑
    public void ClearOldDice()
    {
        StopHighlight();
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
    public void HighlightDice(PlayerDice targetData)
    {
        StopHighlight(); // 先清除旧的高亮

        // 【修改】遍历所有骰子，找到所有匹配的儿子
        foreach (var dice in activeDiceList)
        {
            if (dice == null) continue;
            // 只要引用相同，就加入高亮名单
            if (dice.sourceDataRef == targetData)
            {
                _highlightedDiceList.Add(dice);
                
                // 执行视觉效果 (变大/发光)
                dice.transform.DOKill();
                _originalScale = dice.transform.localScale;
                dice.transform.DOScale(dice.transform.localScale * 1.3f, 0.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(dice.gameObject);
            }
        }
    }

    public void StopHighlight()
    {
        // 【修改】批量复原
        if (_highlightedDiceList.Count > 0)
        {
            foreach (var dice in _highlightedDiceList)
            {
                if (dice != null)
                {
                    dice.transform.DOKill();
                    dice.transform.localScale = _originalScale;
                }
            }
            _highlightedDiceList.Clear();
        }
    }
    public int GetValidDiceCount()
    {
        int count = 0;
        foreach (var dice in activeDiceList)
        {
            if (dice != null && dice.gameObject != null) count++;
        }
        return count;
    }
}