using System;
using UnityEngine;
using UnityEngine.UI; // 用于按钮
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("References")]
    public DiceThrower diceThrower; // 引用之前的投掷脚本
    public Button endTurnButton;    // 引用 UI 上的结束回合按钮
    
    [Header("Wave Config")]
    public List<Transform> spawnPoints; // 拖入场景里的生成点 (SpawnPoint_1, 2, 3...)
    public List<WaveDataSO> levelWaves; // 拖入你配置好的波次文件 (Wave1, Wave2...)
    private int currentWaveIndex = 0;   // 当前第几波
    
    [Header("Runtime State")]
    public List<EnemyTarget> enemies = new List<EnemyTarget>();
    public bool isPlayerTurn = true;
    
    [Header("Player Deck")]
    public List<DiceDataSO> playerDeck = new List<DiceDataSO>(); 
    
    // 当敌人死亡时触发
    public event Action OnEnemyKilledEvent;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
        
        // 游戏开始，加载第一波
        currentWaveIndex = 0;
        StartCoroutine(LoadWaveRoutine());
    }

    // --- 核心：加载波次协程 ---
    IEnumerator LoadWaveRoutine()
    {
        isPlayerTurn = false;
        endTurnButton.interactable = false; // 禁用按钮防止乱点
        // 1. 检查是否通关
        if (currentWaveIndex >= levelWaves.Count)
        {
            Debug.Log("<color=green>关卡胜利！所有波次已清除！</color>");
            // ShowVictoryUI();
            yield break;
        }

        // 2. 获取当前波次数据
        WaveDataSO currentWave = levelWaves[currentWaveIndex];
        Debug.Log($"--- 开始第 {currentWaveIndex + 1} 波 ---");

        // 3. 生成敌人
        SpawnEnemies(currentWave);

        // 4. (可选) 给一点特写时间或UI显示 "Wave 2 Start"
        yield return new WaitForSeconds(1.0f);

        // 5. 开始新回合
        StartNewRound();
    }

    void SpawnEnemies(WaveDataSO waveData)
    {
        // 清理旧列表 (理论上是空的，但为了保险)
        enemies.Clear();

        // 遍历配置里的怪物
        for (int i = 0; i < waveData.enemyPrefabs.Count; i++)
        {
            // 防止生成点不够用
            if (i >= spawnPoints.Count) break; 

            GameObject prefab = waveData.enemyPrefabs[i];
            Transform point = spawnPoints[i];

            // 实例化
            GameObject enemyObj = Instantiate(prefab, point.position, point.rotation);
            
            // 获取脚本并加入列表
            EnemyTarget target = enemyObj.GetComponent<EnemyTarget>();
            if (target != null)
            {
                enemies.Add(target);
                
                // 确保新生成的敌人UI是初始化的
                // 如果需要手动初始化可以在这里调用 target.Init();
            }
        }
    }

    // --- 修改移除敌人的逻辑 ---
    public void RemoveEnemy(EnemyTarget enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
            OnEnemyKilledEvent?.Invoke();
        }

        // 检查：当前场上是否还有活着的敌人？
        if (enemies.Count == 0)
        {
            Debug.Log("波次清除！准备下一波...");
            
            // 索引+1
            currentWaveIndex++;
            
            // 清理桌上的骰子 (可选，看你设计：是保留骰子到下一波，还是清空)
            // diceThrower.ClearOldDice(); 

            // 延迟一点时间后加载下一波
            StartCoroutine(LoadWaveRoutine());
        }
    }
    // 获取同队的随机目标
    public BattleTarget GetRandomTargetOfTeam(TargetTeam team, BattleTarget exclusion)
    {
        List<BattleTarget> candidates = new List<BattleTarget>();

        if (team == TargetTeam.Enemy)
        {
            // 找敌人
            foreach (var e in enemies) {
                if (e != null && e.currentHp > 0 && e != exclusion) candidates.Add(e);
            }
        }
        else if (team == TargetTeam.Player)
        {
            // 找玩家 (目前可能只有1个玩家，就是 exclusion 自己)
            // 如果你想支持弹射回自己，就把它加入列表
            var playerTarget = FindObjectOfType<PlayerTarget>(); // 简单获取
            if (playerTarget != null) candidates.Add(playerTarget);
        }

        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }
        
        return null;
    }
    public BattleTarget GetRandomTarget(BattleTarget exclusion)
    {
        List<BattleTarget> candidates = new List<BattleTarget>();

        // 找敌人
        foreach (var e in enemies) {
            if (e != null && e.currentHp > 0 && e != exclusion) candidates.Add(e);
        }
        // 找玩家 (目前可能只有1个玩家，就是 exclusion 自己)
        // 如果你想支持弹射回自己，就把它加入列表
        var playerTarget = FindObjectOfType<PlayerTarget>(); // 简单获取
        if (playerTarget != null) candidates.Add(playerTarget);

        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }
        
        return null;
    }
    // --- 阶段 1: 新回合开始 ---
    public void StartNewRound()
    {
        isPlayerTurn = true;
        endTurnButton.interactable = true;

        PlayerManager.Instance.ResetArmor();

        foreach (var enemy in enemies)
        {
            if(enemy != null) enemy.PlanNextMove();
        }

        // --- 修改这里 ---
        // 以前是 diceThrower.ThrowAllDice();
        // 现在指定生成数量，比如 3 个（或者根据你的装备变量来）
        diceThrower.SpawnAndThrow(playerDeck);
    
        Debug.Log("--- 玩家回合开始 ---");
    }

    // --- 阶段 2: 玩家点击结束回合 ---
    public void OnEndTurnClicked()
    {
        if (!isPlayerTurn) return;
        diceThrower.ClearOldDice();
        // 进入敌人回合
        StartCoroutine(EnemyTurnRoutine());
    }

    // --- 阶段 3: 敌人行动 (协程控制节奏) ---
    IEnumerator EnemyTurnRoutine()
    {
        isPlayerTurn = false;
        endTurnButton.interactable = false; // 禁用按钮防止乱点

        // 清理掉桌上没用完的骰子 (可选)
        // CleanupRemainingDice(); 

        Debug.Log("--- 敌人回合开始 ---");
        // 先触发所有敌人的 OnTurnStart (处理燃烧等)
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            // 二次检查：防止对象已经被销毁
            if (enemies[i] != null)
            {
                enemies[i].OnTurnStart();
            }
        }
        
        // 等一点时间展示扣血效果
        yield return new WaitForSeconds(0.5f);
        // 攻击阶段的代码建议稍微改一下，确保安全
        // 这里可以用 ToList() 创建一个副本列表来遍历，这样原列表删除了也不影响当前循环

        var livingEnemies = new List<EnemyTarget>(enemies); // 创建一个快照

        foreach (var enemy in livingEnemies)
        {
            // 必须检查：因为在快照里它还在，但在真实世界里它可能刚刚被燃烧烫死了
            if (enemy != null && enemy.gameObject.activeInHierarchy) 
            {
                yield return StartCoroutine(enemy.ExecuteAction());
                yield return new WaitForSeconds(0.5f);
            }
        }

        // 所有敌人动完了，开始新回合
        StartNewRound();
    }
}