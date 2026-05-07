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
    // 定义战斗结果事件，供外部（GameFlowController）监听
    public event Action OnBattleVictoryEvent;
    public event Action OnBattleDefeatEvent;
    public event Action<int> OnPlayerUseDice;
    private BattleRoomSO _currentRoomData;
    public BattleRoomSO CurrentRoomData => _currentRoomData; // 【新增】对外暴露属性
    public int currentBattleDamageBonus = 0;
    public int diceUsedThisTurn = 0; // 记录本回合使用了几颗骰子
    void Awake() { Instance = this; }

    // --- 新入口：由 GameFlowController 调用 ---
    public void StartNewBattle(BattleRoomSO roomData)
    {
        // 1. 清理战场 (这是单场景最重要的一步！)
        CleanUpBattlefield();
        _currentRoomData = roomData; 
        // =========================================================
        // 继承全局伤害 Buff 到本场战斗 (持续一整场)
        // =========================================================
        currentBattleDamageBonus = PlayerManager.Instance.nextBattleDamageBonus;
        PlayerManager.Instance.nextBattleDamageBonus = 0; // 提取后清空
        
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
        // endTurnButton.interactable = true;
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
        if (_currentRoomData != null && _currentRoomData.rewardAbilityDraft)
        {
            Debug.Log("<color=yellow>触发房间通关奖励：骰子附魔抽卡三选一！</color>");
            // 呼叫 GameFlowController 弹出抽卡，传入回调：抽完后回大地图
            // GameFlowController.Instance.StartDraftProcess(ReturnToMapState);
            OnBattleVictoryEvent?.Invoke();
        }
        else
        {
            Debug.Log("该房间没有附魔奖励，直接返回大地图。");
            // ReturnToMapState();
        }
    }
    // private void ReturnToMapState()
    // {
    //     // 可以在这里加一个清理本场战斗临时数据的逻辑
    //     currentBattleDamageBonus = 0; 
    //     
    //     GameFlowController.Instance.EnterMapState();
    // }
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
            ResourceManager.Instance.AddManaDust(enemy.manaDustReward);

            // 2. 移除列表
            enemies.Remove(enemy);
            OnEnemyKilledEvent?.Invoke();
        }

        // 3. 检查战斗是否结束
        if (enemies.Count == 0)
        {
            EndBattleVictory();
        }
    }

    // 开始新回合
    public void StartNewRound()
    {
        if (!_isBattleActive) return; 
        isPlayerTurn = true;
        endTurnButton.interactable = true;
        diceUsedThisTurn = 0; // 【新增】每回合重置计数

        PlayerManager.Instance.ResetArmor();
        // =========================================================
        // 【新增】跨场次护甲 Buff 结算 (因为执行完就归0了，所以只在第一回合生效！)
        // =========================================================
        if (PlayerManager.Instance.nextBattleArmorBonus > 0)
        {
            PlayerManager.Instance.AddArmor(PlayerManager.Instance.nextBattleArmorBonus);
            Debug.Log($"<color=green>【节点Buff生效】开局获得 {PlayerManager.Instance.nextBattleArmorBonus} 点额外护甲！</color>");
            PlayerManager.Instance.nextBattleArmorBonus = 0; // 消耗掉
        }
        // 敌人预告意图
        foreach (var enemy in enemies)
        {
            if(enemy != null) enemy.PlanNextMove();
        }

        // 从养成系统获取骰子数据
        var newDeck = MagicCircleManager.Instance.GetBattleDeck();
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
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (enemies[i] != null) enemies[i].OnTurnStart();
        }

        // 再次检查战斗锁 (防止在暂停期间战斗已经通过燃烧结束了)
        if (!_isBattleActive) yield break; 

        // =========================================================

        yield return new WaitForSeconds(0.5f);

        // 2. 敌人行动
        var livingEnemies = new List<EnemyTarget>(enemies); 

        foreach (var enemy in livingEnemies)
        {
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
            // GameFlowController.Instance.ShowRunSummary(false); // 呼出结算界面
            OnBattleDefeatEvent?.Invoke();
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

    public BattleTarget GetRandomTargetOfTeam(GameEnums.TargetTeam team, BattleTarget exclusion)
    {
        List<BattleTarget> candidates = new List<BattleTarget>();

        if (team == GameEnums.TargetTeam.Enemy)
        {
            foreach (var e in enemies) {
                if (e != null && e.currentHp > 0 && e != exclusion) candidates.Add(e);
            }
        }
        else if (team == GameEnums.TargetTeam.Player)
        {
            var playerTarget = PlayerUITarget.Instance;
            if (playerTarget != null) candidates.Add(playerTarget);
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        
        return null;
    }

    public BattleTarget GetRandomTarget(BattleTarget exclusion)
    {
        // 默认找敌人
        return GetRandomTargetOfTeam(GameEnums.TargetTeam.Enemy, exclusion);
    }
    public void TriggerPlayerUseDice()
    {
        diceUsedThisTurn++; // 记录使用次数
        OnPlayerUseDice?.Invoke(1);
    }
    // 汇总场上所有敌人的光环，对玩家的伤害进行最终修饰
    public int ProcessGlobalDamageModifiers(int rawDamage, int usedOrder, int remainingAtThrow)
    {
        int finalDamage = rawDamage;
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.currentHp > 0)
            {
                finalDamage = enemy.ProcessGlobalDamageModifiers(finalDamage, usedOrder, remainingAtThrow);
            }
        }
        return finalDamage;
    }
    // 广播玩家获得护甲的事件给所有怪物
    public void TriggerPlayerGainArmor(int amount)
    {
        if (!_isBattleActive) return;

        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.currentHp > 0)
            {
                enemy.HandlePlayerGainArmor(amount);
            }
        }
    }
    // =========================================================
    // 【新增】核心战斗管线引擎 (Damage Pipeline Engine)
    // =========================================================
    
    // 伤害事件队列（极其重要：防止连环反伤导致栈溢出死循环！）
    private Queue<DamageInfo> _damageQueue = new Queue<DamageInfo>();
    private bool _isProcessingDamage = false;

    // 管线入口：所有攻击、反伤、毒伤，全部把包裹扔进这里！
    public void ProcessDamage(DamageInfo info)
    {
        _damageQueue.Enqueue(info);
        
        // 如果当前流水线闲着，就启动它；如果正在处理别的伤害，就乖乖排队
        if (!_isProcessingDamage)
        {
            ProcessDamageQueue();
        }
    }

    // 管线处理车间
    private void ProcessDamageQueue()
    {
        _isProcessingDamage = true;

        while (_damageQueue.Count > 0)
        {
            // 1. 从队列拿出一个伤害包裹
            DamageInfo currentInfo = _damageQueue.Dequeue();

            // 2. 【攻击前钩子】触发攻击者身上的 Buff（如：力量、虚弱）
            if (currentInfo.Attacker != null)
            {
                currentInfo.Attacker.TriggerBeforeAttack(currentInfo);
            }

            // 3. 【受击前钩子】触发防御者身上的 Buff（如：护甲、反伤、锁血）
            if (currentInfo.Defender != null)
            {
                currentInfo.Defender.TriggerBeforeDefend(currentInfo);
            }

            // 4. 【结算与执行】经过双方 Buff 的神仙打架后，确认最终伤害
            if (currentInfo.Defender != null && currentInfo.FinalDamage > 0)
            {
                // 真正执行硬扣血
                currentInfo.Defender.ExecuteDamage(currentInfo.FinalDamage);
                
                // 触发数字飘字体验 (如果你有的话)
                if (DamageNumberManager.Instance != null)
                {
                    // DamageNumberManager.Instance.Show(currentInfo.Defender.transform.position, currentInfo.FinalDamage);
                }

                // 5. 【受击后钩子】触发次生效应（如：受伤回怒、链枷灵魂分摊）
                currentInfo.Defender.TriggerAfterTakeDamage(currentInfo);
            }
            else if (currentInfo.FinalDamage <= 0)
            {
                Debug.Log($"<color=gray>包裹结算完毕，{currentInfo.Defender?.name} 的护甲或机制将伤害完全归零！</color>");
            }
        }

        // 队列清空，流水线停机休息
        _isProcessingDamage = false;
    }
}