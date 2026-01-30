using UnityEngine;

public class GameManager: MonoBehaviour
{
    public static GameManager Instance;

    [Header("Managers")]
    public BattleManager battleManager;

    [Header("UI")]
    public GameObject battleUI; // 骰子盘、魔法阵等
    public GameObject victoryUI; // 胜利结算界面

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Invoke(nameof(StartGameLoop), 0.1f);
    }

    void StartGameLoop()
    {
        // MapManager.Instance.LoadNextRandomRoom();
    }

    // --- 战斗胜利回调 ---
    public void OnBattleVictory()
    {
        Debug.Log("GameManager: 收到胜利消息，展示结算！");
        
        // 1. 隐藏战斗 UI (可选，或者保留背景)
        // battleUI.SetActive(false); 

        // 2. 显示胜利面板
        if (victoryUI) victoryUI.SetActive(true);

        // 3. 后续逻辑：点击胜利面板的“继续”按钮 -> 返回大地图 Scene
    }
    public void OnNextRoomButtonClicked()
    {
        // 清理上一局的残留 (比如销毁敌人尸体，如果有的话)
        // ...

        // 加载下一个房间
        // MapManager.Instance.LoadNextRandomRoom();
    }

    // --- 处理房间进入逻辑 ---
    public void ProcessRoomEnter(RoomDataSO room)
    {
        // 1. 重置 UI 状态
        if (victoryUI) victoryUI.SetActive(false);
        if (battleUI) battleUI.SetActive(true);

        // 2. 根据类型分发
        switch (room.roomType)
        {
            case Enum.RoomType.Battle:
            case Enum.RoomType.Elite:
            case Enum.RoomType.Boss:
                // 强转为 BattleRoomSO 获取怪物数据
                BattleRoomSO battleRoom = room as BattleRoomSO;
                if (battleRoom != null)
                {
                    StartCombat(battleRoom.enemyWave);
                }
                break;

            case Enum.RoomType.Treasure:
                // TODO: 打开宝箱界面
                Debug.Log("进入宝箱房（暂未实现）");
                // 暂时直接跳过
                OnBattleVictory(); 
                break;
                
            // ... 其他类型 ...
        }
    }
    void StartCombat(WaveDataSO waveData)
    {
        // 调用 BattleManager 开始战斗
        battleManager.StartBattle(waveData);
    }

}