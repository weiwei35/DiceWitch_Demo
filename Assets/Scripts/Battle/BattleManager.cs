using System;
using UnityEngine;
using UnityEngine.UI; 
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance;

    [Header("References")]
    public DiceThrower diceThrower; 
    public Button endTurnButton;    
    
    [Header("Scene Config")]
    // 【恢复】直接在 Inspector 里拖入场景里的固定生成点
    public List<Transform> fixedSpawnPoints; 
    
    [Header("Runtime State")]
    public List<EnemyTarget> enemies = new List<EnemyTarget>();
    public bool isPlayerTurn = true;
    private bool _isBattleActive = false; // 战斗锁
    
    private Transform _enemyContainer;
    
    public event Action OnEnemyKilledEvent;
    public event Action OnPlayerTurnEnd;

    private bool _isLevelingUp = false;    // 是否正在处理升级界面
    private bool _isVictoryPending = false; // 是否有一场胜利正在排队等待结算
    void Awake() { Instance = this; }

    void Start()
    {
        // 之前让你删空了，现在把监听逻辑加回来
        if (PlayerProgressionManager.Instance != null)
        {
            PlayerProgressionManager.Instance.OnLevelUp += HandleLevelUp;
        }
    }
    // --- 新入口：由 GameFlowController 调用 ---
    public void StartNewBattle(BattleRoomSO roomData)
    {
        // 1. 清理战场 (这是单场景最重要的一步！)
        CleanUpBattlefield();

        // 2. 重置玩家状态 (可选，比如重置护甲)
        // PlayerManager.Instance.ResetStatus();
        
        // 3. 开始战斗逻辑
        if (roomData != null && roomData.enemyWave != null)
        {
            Debug.Log($"<color=orange>开始战斗：{roomData.roomName}</color>");
            StartBattle(roomData.enemyWave); // 调用你原有的 StartBattle
        }
        else
        {
            Debug.LogError("战斗数据为空！");
        }
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    // --- 清理逻辑 ---
    private void CleanUpBattlefield()
    {
        // 1. 销毁所有活着的敌人
        foreach (var enemy in enemies)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        enemies.Clear();

        // 2. 销毁尸体 (如果你的尸体是单独的 GameObject)
        // 3. 清理地上的骰子
        diceThrower.ClearOldDice();
        
        if (GhostDiceUIManager.Instance != null)
        {
            GhostDiceUIManager.Instance.ClearAllGhosts();
        }
        // 4. 重置 UI 状态
        _isBattleActive = true;
        _isVictoryPending = false;
        // endTurnButton.interactable = true;
    }

    void OnDestroy()
    {
        if (PlayerProgressionManager.Instance != null)
            PlayerProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    // =========================================================
    // 战斗入口与出口 (Exploration Interface)
    // =========================================================

    public void StartBattle(WaveDataSO waveData)
    {
        _isBattleActive = true;
        
        // 使用固定的生成点
        SpawnEnemies(waveData, fixedSpawnPoints);
        
        // 开启第一回合
        StartNewRound();
    }

    // 【出口】战斗胜利，清理现场，通知 GameManager
    private void EndBattleVictory()
    {
        if (!_isBattleActive) return;
        _isBattleActive = false;
        
        Debug.Log("战斗胜利！");
        StopAllCoroutines();
        
        diceThrower.ClearOldDice();
        // 【新增修复】战斗赢了，也要把没用掉的幽灵清空
        if (GhostDiceUIManager.Instance != null)
        {
            GhostDiceUIManager.Instance.ClearAllGhosts();
        }

        // 1. 通知地图：这个房间搞定了
        if (MapManager.Instance != null)
        {
            MapManager.Instance.CompleteCurrentRoom();
        }

        // 2. 叫 GameFlow 切回地图界面
        GameFlowController.Instance.EnterMapState();
    }

    // =========================================================
    // 战斗逻辑 (Battle Logic)
    // =========================================================

    // 生成敌人逻辑
    void SpawnEnemies(WaveDataSO waveData, List<Transform> spawnPoints)
    {
        // 清理旧列表
        enemies.Clear();
        
        // 创建或获取容器
        if (_enemyContainer == null) _enemyContainer = new GameObject("--- Enemies ---").transform;
        
        // 遍历生成
        for (int i = 0; i < waveData.enemyPrefabs.Count; i++)
        {
            // 防止生成点不够用
            if (spawnPoints == null || i >= spawnPoints.Count) break; 

            GameObject prefab = waveData.enemyPrefabs[i];
            Transform point = spawnPoints[i];

            // 实例化
            GameObject enemyObj = Instantiate(prefab, point.position, point.rotation);
            enemyObj.transform.SetParent(_enemyContainer);
            
            EnemyTarget target = enemyObj.GetComponent<EnemyTarget>();
            if (target != null)
            {
                enemies.Add(target);
            }
        }
    }

    // 敌人死亡逻辑
    public void RemoveEnemy(EnemyTarget enemy)
    {
        if (enemies.Contains(enemy))
        {
            // 1. 结算奖励
            PlayerProgressionManager.Instance.AddExperience(enemy.xpReward);
            PlayerProgressionManager.Instance.AddManaDust(enemy.manaDustReward);

            // 2. 移除列表
            enemies.Remove(enemy);
            OnEnemyKilledEvent?.Invoke();
        }

        // 3. 检查战斗是否结束
        if (enemies.Count == 0)
        {
            // 如果是正在升级，就不要立刻结算胜利
            if (_isLevelingUp)
            {
                Debug.Log("战斗结束，但正在升级中... 胜利结算挂起。");
                _isVictoryPending = true; // 挂起
            }
            else
            {
                // 正常结算
                EndBattleVictory();
            }
        }
    }

    // 开始新回合
    public void StartNewRound()
    {
        if (!_isBattleActive) return; 
        isPlayerTurn = true;
        endTurnButton.interactable = true;

        PlayerManager.Instance.ResetArmor();

        // 敌人预告意图
        foreach (var enemy in enemies)
        {
            if(enemy != null) enemy.PlanNextMove();
        }

        // 从养成系统获取骰子数据
        var newDeck = PlayerProgressionManager.Instance.GetBattleDeck();
        diceThrower.SpawnAndThrow(newDeck);
    
        Debug.Log("--- 玩家回合开始 ---");
    }

    // 点击结束回合按钮
    public void OnEndTurnClicked()
    {
        if (!_isBattleActive) return; // 如果战斗结束了，按钮无效
        if (!isPlayerTurn) return;
        
        // 清理当前回合的骰子
        diceThrower.ClearOldDice();
        if (GhostDiceUIManager.Instance != null)
        {
            GhostDiceUIManager.Instance.ClearAllGhosts();
        }
        // 【新增】广播回合结束，通知链枷等状态自我销毁
        OnPlayerTurnEnd?.Invoke();
        // 进入敌人回合
        StartCoroutine(EnemyTurnRoutine());
    }

    // 敌人回合流程
    IEnumerator EnemyTurnRoutine()
    {
        isPlayerTurn = false;
        endTurnButton.interactable = false;

        Debug.Log("--- 敌人回合开始 ---");
        
        // 1. 结算状态 (如燃烧)
        // 如果这里有怪被烧死了，触发了升级，_isLevelingUp 会变成 true
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] != null) enemies[i].OnTurnStart();
        }
        
        // =========================================================
        // 🚦【核心修复】红绿灯检查
        // =========================================================
        
        // 如果正在升级，协程暂停在这里，直到 _isLevelingUp 变为 false
        if (_isLevelingUp)
        {
            Debug.Log("检测到升级事件，暂停敌人回合...");
            yield return new WaitUntil(() => _isLevelingUp == false);
            Debug.Log("升级完成，恢复敌人回合。");
        }

        // 再次检查战斗锁 (防止在暂停期间战斗已经通过燃烧结束了)
        if (!_isBattleActive) yield break; 

        // =========================================================

        yield return new WaitForSeconds(0.5f);

        // 2. 敌人行动
        var livingEnemies = new List<EnemyTarget>(enemies); 

        foreach (var enemy in livingEnemies)
        {
            // 每次行动前，最好也检查一下（防止多重触发，虽然概率低）
            if (_isLevelingUp) yield return new WaitUntil(() => !_isLevelingUp);
            if (!_isBattleActive) yield break;
            if (PlayerManager.Instance.currentHp <= 0) break;

            if (enemy != null && enemy.gameObject.activeInHierarchy) 
            {
                yield return StartCoroutine(enemy.ExecuteAction());
                yield return new WaitForSeconds(0.5f);
            }
        }
        // 3. 检查玩家死活
        if (PlayerManager.Instance.currentHp <= 0)
        {
            Debug.Log("游戏结束！");
            // ShowGameOverUI(); // 可以在这里调用游戏失败界面
        }
        else
        {
            // 4. 下一回合
            if (_isBattleActive)
            {
                StartNewRound();
            }
        }
    }

    // 升级事件回调
    void HandleLevelUp()
    {
        Debug.Log("战斗中触发升级！开启抽卡...");
        // 1. 标记状态
        _isLevelingUp = true;

        // 2. 启动抽卡，并传入【回调函数】
        GameFlowController.Instance.StartDraftProcess(OnLevelUpDraftFinished);
    }
    // 当升级抽卡结束时调用
    void OnLevelUpDraftFinished()
    {
        Debug.Log("升级抽卡完成。");
        _isLevelingUp = false;

        // 3. 检查是否有挂起的胜利
        if (_isVictoryPending)
        {
            Debug.Log("检测到挂起的胜利，现在结算！");
            _isVictoryPending = false;
            EndBattleVictory(); // 补发胜利结算
        }
    }
    // =========================================================
    // 辅助方法 (Targeting Helpers)
    // =========================================================

    public BattleTarget GetRandomTargetOfTeam(Enum.TargetTeam team, BattleTarget exclusion)
    {
        List<BattleTarget> candidates = new List<BattleTarget>();

        if (team == Enum.TargetTeam.Enemy)
        {
            foreach (var e in enemies) {
                if (e != null && e.currentHp > 0 && e != exclusion) candidates.Add(e);
            }
        }
        else if (team == Enum.TargetTeam.Player)
        {
            var playerTarget = FindObjectOfType<PlayerUITarget>();
            if (playerTarget != null) candidates.Add(playerTarget);
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        
        return null;
    }

    public BattleTarget GetRandomTarget(BattleTarget exclusion)
    {
        // 默认找敌人
        return GetRandomTargetOfTeam(Enum.TargetTeam.Enemy, exclusion);
    }
}