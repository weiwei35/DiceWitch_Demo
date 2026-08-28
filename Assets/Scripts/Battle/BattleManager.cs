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
    public DiceViewMonitor battleDiceViewMonitor;
    public Button endTurnButton;

    [Header("Battle Intro")]
    public Animator battleIntroAnimator;
    
    [Header("Odd Enemy Formation")]
    public Transform enemyLayoutCenter;
    [Min(0.1f)] public float oddHorizontalSpacing = 3.8f;
    [Range(0.1f, 1f)] public float oddCenterScale = 1f;
    [Min(0f)] public float oddScaleStep = 0.2f;
    [Range(0.1f, 1f)] public float oddMinimumScale = 0.75f;

    [Header("Even Enemy Formation")]
    [Min(0.1f)] public float evenHorizontalSpacing = 4.4f;
    [Range(0.1f, 1f)] public float evenCenterScale = 1f;
    [Min(0f)] public float evenScaleStep = 0.1f;
    [Range(0.1f, 1f)] public float evenMinimumScale = 0.95f;
    public float evenVerticalOffset = -0.2f;
    
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
    public bool IsBattleActive => _isBattleActive;

    private ProjectedDiceWeakGuide _projectedDiceGuide;
    private PhysicsDice _guidedDice;
    private string _activeBattleGuideId;
    private GameEnums.TargetTeam _activeGuideTargetTeam;
    private float _guideRefreshBlockedUntil;
    private bool _staticGuideArrowVisible;
    private TargetingArrow _guideTargetingArrow;
    private Coroutine _battleStartCoroutine;
    private bool _isDiceSpellResponding;
    private bool _diceCameraFitted; // 每场战斗只适配一次骰子相机（固定机位）
    private readonly List<MouseParallaxUI> _pausedIntroParallax = new List<MouseParallaxUI>();
    private static readonly System.Random EnemyPlacementRandom = new System.Random();

    void Awake()
    {
        Instance = this;
        _projectedDiceGuide = GetComponent<ProjectedDiceWeakGuide>();
        if (_projectedDiceGuide == null)
            _projectedDiceGuide = gameObject.AddComponent<ProjectedDiceWeakGuide>();
    }

    private void LateUpdate()
    {
        if (!_isBattleActive)
            return;

        RefreshBattleDiceGuide();
        UpdateStaticBattleGuideArrow();
    }

    // --- 新入口：由 GameFlowController 调用 ---
    public void StartNewBattle(BattleRoomSO roomData)
    {
        WeakGuideService.Instance?.ActivateScreen(this);
        _guideRefreshBlockedUntil = 0f;
        ClearCurrentBattleGuide();

        if (_battleStartCoroutine != null)
        {
            StopCoroutine(_battleStartCoroutine);
            _battleStartCoroutine = null;
        }
        ResumeIntroParallax();

        // 1. 清理战场 (这是单场景最重要的一步！)
        CleanUpBattlefield();
        _currentRoomData = roomData;
        
        // 3. 开始战斗逻辑
        if (roomData != null && roomData.enemyWave != null)
        {
            Debug.Log($"<color=orange>开始战斗：{roomData.roomName}</color>");
            MagicCircleDisplay.Instance?.SetSlotIconsVisible(false);
            PauseIntroParallax();
            _battleStartCoroutine = StartCoroutine(StartBattleAfterIntro(roomData.enemyWave));
        }
        else
        {
            Debug.LogError("战斗数据为空！");
        }
        endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    private IEnumerator StartBattleAfterIntro(WaveDataSO waveData)
    {
        Animator animator = battleIntroAnimator;
        if (animator != null && animator.runtimeAnimatorController != null && animator.gameObject.activeInHierarchy)
        {
            animator.enabled = true;
            animator.Play(0, 0, 0f);
            animator.Update(0f);

            yield return null;
            while (animator != null
                && animator.isActiveAndEnabled
                && (animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f))
            {
                yield return null;
            }

            if (animator == null || !animator.gameObject.activeInHierarchy)
            {
                _battleStartCoroutine = null;
                yield break;
            }
        }
        else
        {
            yield return null;
        }

        _battleStartCoroutine = null;
        if (animator != null)
            animator.enabled = false;
        ResumeIntroParallax();
        MagicCircleDisplay.Instance?.RefreshAll();
        MagicCircleDisplay.Instance?.SetSlotIconsVisible(true);
        StartBattle(waveData);
    }

    private void PauseIntroParallax()
    {
        _pausedIntroParallax.Clear();
        if (battleIntroAnimator == null) return;

        foreach (MouseParallaxUI parallax in battleIntroAnimator.GetComponentsInChildren<MouseParallaxUI>(true))
        {
            if (parallax == null || !parallax.enabled) continue;
            _pausedIntroParallax.Add(parallax);
            parallax.enabled = false;
        }
    }

    private void ResumeIntroParallax()
    {
        foreach (MouseParallaxUI parallax in _pausedIntroParallax)
        {
            if (parallax != null)
                parallax.enabled = true;
        }
        _pausedIntroParallax.Clear();
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
        _isBattleActive = false;
        isPlayerTurn = false;
        endTurnButton.interactable = false;
    }

    // =========================================================
    // 战斗入口与出口 (Exploration Interface)
    // =========================================================

    public void StartBattle(WaveDataSO waveData)
    {
        _isBattleActive = true;
        _diceCameraFitted = false; // 每场新战斗重新允许适配一次骰子相机

        // 直到入场动画真正结束才消费下一场战斗 Buff，避免中途退出时丢失。
        currentBattleDamageBonus = PlayerManager.Instance.nextBattleDamageBonus;
        PlayerManager.Instance.nextBattleDamageBonus = 0;

        SpawnEnemies(waveData);
        
        // 开启第一回合
        StartNewRound();
    }

    // 【出口】战斗胜利，清理现场，通知 GameManager
    private void EndBattleVictory()
    {
        if (!_isBattleActive) return;
        _isBattleActive = false;
        ExitBattleGuide();
        
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
            ReturnToMapState();
        }
    }
    private void ReturnToMapState()
    {
        currentBattleDamageBonus = 0;
        GameFlowController.Instance.ChangeState(new MapState());
    }
    // =========================================================
    // 战斗逻辑 (Battle Logic)
    // =========================================================

    // 生成敌人逻辑
    void SpawnEnemies(WaveDataSO waveData)
    {
        // 清理旧列表
        enemies.Clear();

        if (enemyLayoutCenter == null)
        {
            Debug.LogError("Enemy formation center is not configured.");
            return;
        }
        
        // 创建或获取容器
        if (_enemyContainer == null) _enemyContainer = new GameObject("--- Enemies ---").transform;
        
        // 遍历生成
        int count = waveData.enemyPrefabs.Count;
        bool useEvenFormation = count % 2 == 0;
        float spacing = useEvenFormation ? evenHorizontalSpacing : oddHorizontalSpacing;
        float centerScale = Mathf.Min(1f, useEvenFormation ? evenCenterScale : oddCenterScale);
        float scaleStep = useEvenFormation ? evenScaleStep : oddScaleStep;
        float minimumScale = Mathf.Min(centerScale, useEvenFormation ? evenMinimumScale : oddMinimumScale);
        List<int> placementOrder = BuildEnemyPlacementOrder(waveData.enemyPrefabs);
        EnemyTarget[] targetsByWaveOrder = new EnemyTarget[count];

        for (int placementIndex = 0; placementIndex < count; placementIndex++)
        {
            int sourceIndex = placementOrder[placementIndex];
            GameObject prefab = waveData.enemyPrefabs[sourceIndex];
            float slot = GetSymmetricEnemySlot(placementIndex, count);
            float distanceFromCenter = Mathf.Max(0f, Mathf.Abs(slot) - (count % 2 == 0 ? 0.5f : 0f));
            float layoutScale = Mathf.Clamp(centerScale - distanceFromCenter * scaleStep, minimumScale, 1f);
            Vector3 spawnPosition = enemyLayoutCenter.position
                + enemyLayoutCenter.right * (slot * spacing)
                + enemyLayoutCenter.up * (useEvenFormation ? evenVerticalOffset : 0f);

            // 实例化
            GameObject enemyObj = Instantiate(prefab, spawnPosition, enemyLayoutCenter.rotation);
            enemyObj.transform.localScale *= layoutScale;
            enemyObj.transform.SetParent(_enemyContainer);
            
            EnemyTarget target = enemyObj.GetComponent<EnemyTarget>();
            if (target != null)
                targetsByWaveOrder[sourceIndex] = target;
        }

        foreach (EnemyTarget target in targetsByWaveOrder)
            if (target != null)
                enemies.Add(target);
    }

    private static List<int> BuildEnemyPlacementOrder(List<GameObject> prefabs)
    {
        int count = prefabs.Count;
        var order = new List<int>(count);
        var tiers = new int[count];
        var maxHealth = new int[count];
        var randomTieBreakers = new int[count];

        for (int i = 0; i < count; i++)
        {
            order.Add(i);
            EnemyTarget target = prefabs[i] != null ? prefabs[i].GetComponent<EnemyTarget>() : null;
            tiers[i] = target != null ? (int)target.tier : int.MinValue;
            maxHealth[i] = target != null ? target.maxHp : int.MinValue;
            randomTieBreakers[i] = EnemyPlacementRandom.Next();
        }

        order.Sort((a, b) =>
        {
            int comparison = tiers[b].CompareTo(tiers[a]);
            if (comparison != 0) return comparison;

            comparison = maxHealth[b].CompareTo(maxHealth[a]);
            if (comparison != 0) return comparison;

            comparison = randomTieBreakers[a].CompareTo(randomTieBreakers[b]);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });

        return order;
    }

    private static float GetSymmetricEnemySlot(int index, int count)
    {
        if (count % 2 == 1)
        {
            if (index == 0) return 0f;
            int distance = (index + 1) / 2;
            return index % 2 == 1 ? -distance : distance;
        }

        float halfSlot = index / 2 + 0.5f;
        return index % 2 == 0 ? -halfSlot : halfSlot;
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
        if (PlayerManager.Instance.nextBattleFixedDiceValue > 0 && newDeck.Count > 0)
        {
            int fixedValue = Mathf.Clamp(PlayerManager.Instance.nextBattleFixedDiceValue, 1, 6);
            int fixedDiceIndex = Random.Range(0, newDeck.Count);
            newDeck[fixedDiceIndex].forcedResultValue = fixedValue;
            PlayerManager.Instance.nextBattleFixedDiceValue = 0;
            Debug.Log($"<color=cyan>【节点Buff生效】本次战斗第 {fixedDiceIndex + 1} 颗骰子将被拨动为 {fixedValue}</color>");
        }
        diceThrower.SpawnAndThrow(newDeck);

        // 骰子相机固定机位：每场战斗只在首次生成骰子时按初始数量适配一次
        if (!_diceCameraFitted && battleDiceViewMonitor != null)
        {
            battleDiceViewMonitor.FitCameraToWorldBounds(
                diceThrower.GetLayoutCenterWorld(),
                diceThrower.GetLayoutBoundsSize(newDeck.Count));
            _diceCameraFitted = true;
        }

        Debug.Log("--- 玩家回合开始 ---");
    }

    // 点击结束回合按钮
    public void OnEndTurnClicked()
    {
        if (!_isBattleActive) return; // 如果战斗结束了，按钮无效
        if (!isPlayerTurn) return;
        if (_isDiceSpellResponding) return;

        WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.BattleEndTurn);
        ClearCurrentBattleGuide();
        
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
            ExitBattleGuide();
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

    public void SetDiceSpellResponseActive(bool active)
    {
        _isDiceSpellResponding = active;
        if (endTurnButton != null)
            endTurnButton.interactable = !active && _isBattleActive && isPlayerTurn;
    }

    public void NotifyPlayerDiceTargeted(BattleTarget target)
    {
        if (target == null) return;

        if (target.team == GameEnums.TargetTeam.Player)
            WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.BattleThrowToSelf);
        else if (target.team == GameEnums.TargetTeam.Enemy)
            WeakGuideService.Instance?.CompleteGuide(WeakGuideIds.BattleThrowToEnemy);

        _guideRefreshBlockedUntil = Time.unscaledTime + 0.45f;
        ClearCurrentBattleGuide();
    }

    public void ExitBattleGuide()
    {
        if (_battleStartCoroutine != null)
        {
            StopCoroutine(_battleStartCoroutine);
            _battleStartCoroutine = null;
        }
        if (battleIntroAnimator != null)
            battleIntroAnimator.enabled = false;
        ResumeIntroParallax();

        ClearCurrentBattleGuide();
        WeakGuideService.Instance?.DeactivateScreen(this);
    }

    private void RefreshBattleDiceGuide()
    {
        WeakGuideService service = WeakGuideService.Instance;
        if (service == null
            || diceThrower == null
            || battleDiceViewMonitor == null
            || Time.unscaledTime < _guideRefreshBlockedUntil)
            return;

        if (isPlayerTurn
            && endTurnButton != null
            && endTurnButton.interactable
            && diceThrower.GetValidDiceCount() == 0)
        {
            ShowEndTurnGuide(service);
            return;
        }

        string nextGuideId;
        GameEnums.TargetTeam nextTargetTeam;
        if (!service.IsCompleted(WeakGuideIds.BattleThrowToSelf))
        {
            nextGuideId = WeakGuideIds.BattleThrowToSelf;
            nextTargetTeam = GameEnums.TargetTeam.Player;
        }
        else if (!service.IsCompleted(WeakGuideIds.BattleThrowToEnemy))
        {
            nextGuideId = WeakGuideIds.BattleThrowToEnemy;
            nextTargetTeam = GameEnums.TargetTeam.Enemy;
        }
        else
        {
            ClearCurrentBattleGuide();
            return;
        }

        PhysicsDice nextDice = diceThrower.GetFirstAvailableBattleDice();
        if (nextDice == null)
        {
            ClearCurrentBattleGuide();
            return;
        }

        if (_activeBattleGuideId == nextGuideId && _guidedDice == nextDice)
            return;

        _guidedDice = nextDice;
        _activeBattleGuideId = nextGuideId;
        _activeGuideTargetTeam = nextTargetTeam;
        _projectedDiceGuide.Bind(battleDiceViewMonitor, nextDice);
        _projectedDiceGuide.Show(this, nextGuideId);
    }

    private void ShowEndTurnGuide(WeakGuideService service)
    {
        if (service.IsCompleted(WeakGuideIds.BattleEndTurn))
        {
            ClearCurrentBattleGuide();
            return;
        }

        if (_activeBattleGuideId == WeakGuideIds.BattleEndTurn)
            return;

        _projectedDiceGuide?.Hide();
        _guidedDice = null;
        _activeBattleGuideId = WeakGuideIds.BattleEndTurn;
        HideStaticGuideArrow();

        RectTransform buttonRect = endTurnButton.transform as RectTransform;
        service.ShowGuide(
            this,
            WeakGuideIds.BattleEndTurn,
            buttonRect,
            endTurnButton.targetGraphic);
    }

    private void UpdateStaticBattleGuideArrow()
    {
        if (string.IsNullOrWhiteSpace(_activeBattleGuideId)
            || _projectedDiceGuide == null
            || !_projectedDiceGuide.IsAvailable
            || Time.unscaledTime < _guideRefreshBlockedUntil)
        {
            HideStaticGuideArrow();
            return;
        }

        BattleTarget target = GetGuideArrowTarget();
        TargetingArrow guideArrow = EnsureGuideTargetingArrow();
        if (target == null || guideArrow == null)
        {
            HideStaticGuideArrow();
            return;
        }

        Vector3 arrowStart = _projectedDiceGuide.GetArrowStartWorldPosition()
            + new Vector3(0f, 0f, -2f);
        guideArrow.Show(arrowStart, target.transform.position);
        _staticGuideArrowVisible = true;
    }

    private TargetingArrow EnsureGuideTargetingArrow()
    {
        if (_guideTargetingArrow != null)
            return _guideTargetingArrow;
        if (TargetingArrow.Instance == null)
            return null;

        Color guideColor = WeakGuideService.Instance != null
            ? WeakGuideService.Instance.glowColor
            : new Color(1f, 0.96f, 0.8f, 1f);
        guideColor.a = 0.72f;
        _guideTargetingArrow = TargetingArrow.Instance.CreateVisualCopy(
            "WeakGuideTargetArrow",
            transform,
            guideColor);
        return _guideTargetingArrow;
    }

    private BattleTarget GetGuideArrowTarget()
    {
        if (_activeGuideTargetTeam == GameEnums.TargetTeam.Player)
            return PlayerUITarget.Instance;

        foreach (EnemyTarget enemy in enemies)
        {
            if (enemy != null && enemy.currentHp > 0)
                return enemy;
        }
        return null;
    }

    private void ClearCurrentBattleGuide()
    {
        WeakGuideService.Instance?.ClearGuide(this);
        _projectedDiceGuide?.Hide();
        _guidedDice = null;
        _activeBattleGuideId = null;
        HideStaticGuideArrow();
    }

    private void HideStaticGuideArrow()
    {
        if (_guideTargetingArrow != null)
            _guideTargetingArrow.Hide();
        _staticGuideArrowVisible = false;
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
                // if (DamageNumberManager.Instance != null)
                // {
                //     DamageNumberManager.Instance.ShowDamage(currentInfo.Defender.transform.position, currentInfo.FinalDamage);
                // }

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
