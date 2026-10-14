using System;
using UnityEngine;

/// <summary>
/// 五套基础关卡。地址 StageRosterConfig（YooAsset AddressByFileName）。
/// 在 Inspector 里改波次、波间隔、每一波的敌人类型和数量、波内出怪间隔。
/// 重复出现时的敌人倍率仍按关卡序号计算，不写进每一波。
/// </summary>
[CreateAssetMenu(fileName = "StageRosterConfig", menuName = "Template/Stage Roster")]
public class StageRosterConfig : ScriptableObject
{
    public const string Address = "StageRosterConfig";

    [Tooltip("每过 5 关，敌人血量、伤害、移速加上的倍率。默认 0.2，所以 1-6 是 ×1.2。")]
    public float statMulPerCycle = 0.2f;

    [Tooltip("同一关里，后一波敌人血量相对前一波的倍率。第一波是底数，第二波 ×这个数，第三波再乘一次。")]
    public float hpMulPerWave = 1.2f;

    [Tooltip("基础关卡，下标 0 是第一套。选关按这个数组的长度轮换。")]
    public StageSetConfig[] sets;
}

public enum StageEnemyKind
{
    Enemy1 = 0,
    Enemy2 = 1,
    Enemy3 = 2
}

[Serializable]
public class StageEnemyStat
{
    public float hp = 12f;
    public float damage = 3f;
    public float speed = 0.65f;
}

/// <summary>一波里连续出的同一种敌人。</summary>
[Serializable]
public class StageWaveGroup
{
    [Tooltip("这一组出哪种敌人。")]
    public StageEnemyKind kind = StageEnemyKind.Enemy1;

    [Tooltip("这一组出几只。按数组顺序出完一组再出下一组。")]
    public int count = 1;
}

/// <summary>一波。波次数量就是套装里这个数组的长度。</summary>
[Serializable]
public class StageWaveConfig
{
    [Tooltip("这一波里，两只敌人之间的间隔（秒）。")]
    public float spawnInterval = 1.5f;

    [Tooltip("这一波出哪些敌人、各几只。")]
    public StageWaveGroup[] groups;
}

/// <summary>一套基础关卡。血量、伤害、移速是 ×1.0 的底数，轮换时再乘倍率。</summary>
[Serializable]
public class StageSetConfig
{
    [Tooltip("只在 Inspector 里辨认，不显示给玩家。")]
    public string label = "Set";

    [Tooltip("这一套两波之间的间隔（秒）。")]
    public float waveGap = 18f;

    public int startWood;
    public int startIron;

    public StageEnemyStat enemy1 = new StageEnemyStat();
    public StageEnemyStat enemy2 = new StageEnemyStat();
    public StageEnemyStat enemy3 = new StageEnemyStat();

    public StageWaveConfig[] waves;
}
