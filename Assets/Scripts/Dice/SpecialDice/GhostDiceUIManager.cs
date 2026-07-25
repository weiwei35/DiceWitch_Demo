using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public struct GhostRequest
{
    public RuntimeDiceData data; // 幽灵的基础模板
    public int bonusValue;          // 要继承的额外点数 (来自槽位属性)
}
public class GhostDiceUIManager : MonoBehaviour
{
    public static GhostDiceUIManager Instance;

    [Header("UI Settings")]
    public Transform iconContainer; 
    public GameObject ghostIconPrefab; 
    public float releaseDelay = 0.3f; 

    [Header("References")]
    public DiceThrower diceThrower; 

    private Queue<GhostRequest> _ghostQueue = new Queue<GhostRequest>();

    void Awake() { Instance = this; }

    void Start()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnEnemyKilledEvent += OnEnemyKilled;
    }

    void OnDestroy()
    {
        if (BattleManager.Instance != null)
            BattleManager.Instance.OnEnemyKilledEvent -= OnEnemyKilled;
    }

    public void AddGhost(RuntimeDiceData template, int bonus)
    {
        if (template == null) return;

        GhostRequest req = new GhostRequest 
        { 
            data = template, 
            bonusValue = bonus 
        };
        _ghostQueue.Enqueue(req);

        // UI 生成图标逻辑
        if (iconContainer != null && ghostIconPrefab != null) Instantiate(ghostIconPrefab, iconContainer);
    }

    private void OnEnemyKilled()
    {
        if (_ghostQueue.Count > 0)
        {
            StartCoroutine(ReleaseGhostsRoutine());
        }
    }

    IEnumerator ReleaseGhostsRoutine()
    {
        yield return new WaitForSeconds(0.2f);

        while (_ghostQueue.Count > 0)
        {
            GhostRequest req = _ghostQueue.Dequeue();

            // 移除 UI
            if (iconContainer.childCount > 0)
            {
                Destroy(iconContainer.GetChild(0).gameObject);
            }

            // 生成实体
            SpawnAndThrowGhost(req);

            yield return new WaitForSeconds(releaseDelay);
        }
    }

    // 【修改】生成实体并注入属性
    private void SpawnAndThrowGhost(GhostRequest req)
    {
        if (diceThrower == null) return;

        // 1. 位置
        Vector3 spawnPos = diceThrower.layoutCenter != null ? diceThrower.layoutCenter.position : diceThrower.spawnPoint.position;

        // 2. 实例化
        GameObject diceObj = Instantiate(diceThrower.dicePrefab, spawnPos, Random.rotation);
        
        PhysicsDice pDice = diceObj.GetComponent<PhysicsDice>();
        if (pDice != null)
        {
            // A. 初始化基础数据
            pDice.Initialize(req.data, null); // 来源设为null，因为它是由Ability生成的
            pDice.SnapFaceUp(pDice.GetMaxValueFaceIndex());
            
            // B. 【关键】注入继承来的属性加成
            pDice.ApplyTemporaryBonus(req.bonusValue);

            // C. 注册并投掷
            diceThrower.RegisterDice(pDice);
            diceThrower.RollDiceInPlace(pDice);
        }
    }
    public void ClearAllGhosts()
    {
        // 1. 清空队列
        _ghostQueue.Clear();

        // 2. 停止正在进行的发射协程
        StopAllCoroutines();

        // 3. 清空 UI 图标
        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }
        
        Debug.Log("幽灵 UI 已重置");
    }
}
