using UnityEngine;

/// <summary>
/// BattleScenes 的教程目标解析：
/// - battle.enemy.first / battle.enemy.last：当前存活的第一只 / 最后一只敌人（运行时生成）
/// - battle.die.0 ~ battle.die.4：第 N 颗骰子（世界空间物体）
/// 条件：battle.idle = 玩家回合空闲（BattleController.TutorialIdle）
///       battle.chartOpen / battle.chartClosed = 骰子图鉴面板是否打开（用于图鉴步骤的容错跳步）
/// </summary>
public class BattleTutorialTargets : TutorialTargetProvider
{
    [SerializeField] private BattleController battle;

    public override Transform Resolve(string key)
    {
        if (battle == null || string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (key == "battle.enemy.first" || key == "battle.enemy.last")
        {
            return FindEnemy(key == "battle.enemy.last");
        }

        const string diePrefix = "battle.die.";
        if (key.StartsWith(diePrefix))
        {
            int index;
            if (int.TryParse(key.Substring(diePrefix.Length), out index) && battle.dice != null
                && index >= 0 && index < battle.dice.Length && battle.dice[index] != null)
            {
                return battle.dice[index].transform;
            }
        }

        return null;
    }

    public override bool CheckCondition(string key)
    {
        if (key == "battle.idle")
        {
            return battle != null && battle.TutorialIdle;
        }

        if (key == "battle.chartOpen")
        {
            return IsChartOpen();
        }

        if (key == "battle.chartClosed")
        {
            return !IsChartOpen();
        }

        return true;
    }

    private bool IsChartOpen()
    {
        return battle != null && battle.chart != null && battle.chart.panel != null && battle.chart.panel.activeInHierarchy;
    }

    private Transform FindEnemy(bool last)
    {
        BattleUnit[] enemies = battle.enemies;
        if (enemies == null)
        {
            return null;
        }

        Transform found = null;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].Alive && enemies[i].gameObject.activeInHierarchy)
            {
                found = enemies[i].transform;
                if (!last)
                {
                    break;
                }
            }
        }

        return found;
    }
}
