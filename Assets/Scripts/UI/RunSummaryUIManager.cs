using UnityEngine;
using TMPro;
using System.Text;

public class RunSummaryUIManager : MonoBehaviour
{
    public GameObject summaryPanelRoot;
    public TextMeshProUGUI titleText; // "探险胜利" 或 "探险失败"
    public TextMeshProUGUI statsText; // 统计文本
    public TextMeshProUGUI diceText;  // 骰子阵容总览

    public void ShowSummary()
    {
        summaryPanelRoot.SetActive(true);
        var tracker = RunTracker.Instance;

        // 1. 标题
        titleText.text = tracker.isVictory ? "<color=#00FF00>探险胜利！</color>" : "<color=#FF0000>探险失败</color>";

        // 2. 基础数据统计
        StringBuilder stats = new StringBuilder();
        stats.AppendLine($"<b>到达房间数：</b> <color=yellow>{tracker.roomsVisited}</color>");
        stats.AppendLine($"<b>击杀普通怪物：</b> {tracker.normalKills}");
        stats.AppendLine($"<b>击杀精英怪物：</b> <color=#FFAA00>{tracker.eliteKills}</color>");
        stats.AppendLine($"<b>击杀首领怪物：</b> <color=#FF00FF>{tracker.bossKills}</color>");

        if (!tracker.isVictory)
        {
            stats.AppendLine($"\n<b>死因：</b> 止步于 <color=cyan>[{tracker.deathRoomName}]</color>，被 <color=red><b>{tracker.killerName}</b></color> 无情击败。");
        }
        statsText.text = stats.ToString();

        // 3. 骰子阵容盘点
        StringBuilder diceInfo = new StringBuilder();
        foreach (var slot in PlayerProgressionManager.Instance.magicSlots)
        {
            if (slot.isUnlocked && slot.currentDice != null)
            {
                string dName = slot.currentDice.diceName;
                diceInfo.Append($"■ <b>{dName}</b> ");

                // 是否有法术附魔？
                if (slot.currentDice.boundAbility != null)
                    diceInfo.Append($"  <color=yellow>★ {slot.currentDice.boundAbility.abilityName}</color>");
                
                // 是否有魔法阵等级强化？
                if (slot.currentAttribute != null && slot.currentAttribute.data != null)
                    diceInfo.Append($"  <color=#00FF00>(Lv.{slot.currentAttribute.level} {slot.currentAttribute.data.attributeName})</color>");
                
                diceInfo.AppendLine();
            }
        }
        
        if (diceInfo.Length == 0) diceInfo.Append("你没有携带任何骰子...");
        diceText.text = diceInfo.ToString();
    }

    // 绑定给 UI 上的“返回主菜单”或“重新开始”按钮
    public void OnRestartClicked()
    {
        // 重新加载场景 (需引入 UnityEngine.SceneManagement)
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}