using UnityEngine;

public class RunTracker : MonoBehaviour
{
    public static RunTracker Instance;

    [Header("统计数据")]
    public int roomsVisited = 0;
    public int normalKills = 0;
    public int eliteKills = 0;
    public int bossKills = 0;

    [Header("死亡信息")]
    public bool isVictory = false;
    public string deathRoomName = "";
    public string killerName = "";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddKill(Enum.EnemyTier tier)
    {
        switch (tier)
        {
            case Enum.EnemyTier.Normal: normalKills++; break;
            case Enum.EnemyTier.Elite: eliteKills++; break;
            case Enum.EnemyTier.Boss: bossKills++; break;
        }
    }

    public void RecordDeath(string roomName, string killer)
    {
        isVictory = false;
        deathRoomName = roomName;
        killerName = killer;
    }

    public void ResetRun()
    {
        roomsVisited = 0;
        normalKills = eliteKills = bossKills = 0;
        isVictory = false;
        deathRoomName = killerName = "";
    }
}