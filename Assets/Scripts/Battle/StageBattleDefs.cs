using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把 StageRosterConfig 里的五套基础关卡，按选关套上轮换倍率。
/// 套数 = 序号 % 套数，轮次 = 序号 / 套数，敌人倍率 = 1 + statMulPerCycle × 轮次。
/// 倍率只乘敌人血量、伤害、移速。波次、间隔、开局资源不乘。
/// </summary>
public static class StageBattleDefs
{
    public struct EnemyBase
    {
        public float hp;
        public float damage;
        public float speed;

        public EnemyBase(float hp, float damage, float speed)
        {
            this.hp = hp;
            this.damage = damage;
            this.speed = speed;
        }
    }

    public struct SpawnSlot
    {
        public int kind;
        public EnemyBase stat;
    }

    public class ResolvedWave
    {
        public float spawnInterval;
        public SpawnSlot[] slots = new SpawnSlot[0];
    }

    public class Resolved
    {
        public int template;
        public int cycle;
        public float statMul = 1f;
        public float waveGap = 18f;
        public int startWood;
        public int startIron;
        public ResolvedWave[] waves = new ResolvedWave[0];
        public int WaveCount => waves == null ? 0 : waves.Length;
    }

    /// <summary>整条战役从 0 起算：1-1 = 0，1-6 = 5，2-1 = 10。</summary>
    public static int Ordinal(int chapter, int stage)
    {
        return BattleTuning.StageIndex(chapter, stage);
    }

    public static Resolved Resolve(StageRosterConfig config, int chapter, int stage)
    {
        Resolved resolved = new Resolved();
        if (config == null || config.sets == null || config.sets.Length == 0)
        {
            Debug.LogError("[Battle] StageRosterConfig is missing. No waves will spawn.");
            return resolved;
        }

        int ordinal = Ordinal(chapter, stage);
        if (ordinal < 0)
        {
            ordinal = 0;
        }

        int template = ordinal % config.sets.Length;
        int cycle = ordinal / config.sets.Length;
        float step = config.statMulPerCycle;
        if (step < 0f)
        {
            step = 0f;
        }

        float mul = 1f + step * cycle;
        StageSetConfig src = config.sets[template];
        resolved.template = template;
        resolved.cycle = cycle;
        resolved.statMul = mul;
        if (src == null)
        {
            Debug.LogError("[Battle] Stage set " + (template + 1) + " is empty.");
            return resolved;
        }

        resolved.waveGap = src.waveGap;
        resolved.startWood = src.startWood;
        resolved.startIron = src.startIron;
        EnemyBase enemy1 = Scale(src.enemy1, mul);
        EnemyBase enemy2 = Scale(src.enemy2, mul);
        EnemyBase enemy3 = Scale(src.enemy3, mul);
        float hpPerWave = config.hpMulPerWave;
        if (hpPerWave <= 0f)
        {
            hpPerWave = 1f;
        }

        resolved.waves = ExpandWaves(src.waves, enemy1, enemy2, enemy3, hpPerWave);
        return resolved;
    }

    private static ResolvedWave[] ExpandWaves(StageWaveConfig[] waves, EnemyBase enemy1, EnemyBase enemy2, EnemyBase enemy3, float hpPerWave)
    {
        if (waves == null || waves.Length == 0)
        {
            return new ResolvedWave[0];
        }

        ResolvedWave[] expanded = new ResolvedWave[waves.Length];
        for (int i = 0; i < waves.Length; i++)
        {
            StageWaveConfig wave = waves[i];
            ResolvedWave entry = new ResolvedWave();
            entry.spawnInterval = wave != null ? wave.spawnInterval : 1.5f;
            float hpMul = Mathf.Pow(hpPerWave, i);
            entry.slots = ExpandGroups(wave, enemy1, enemy2, enemy3, hpMul);
            expanded[i] = entry;
        }

        return expanded;
    }

    private static SpawnSlot[] ExpandGroups(StageWaveConfig wave, EnemyBase enemy1, EnemyBase enemy2, EnemyBase enemy3, float hpMul)
    {
        if (wave == null || wave.groups == null || wave.groups.Length == 0)
        {
            return new SpawnSlot[0];
        }

        List<SpawnSlot> slots = new List<SpawnSlot>();
        for (int g = 0; g < wave.groups.Length; g++)
        {
            StageWaveGroup group = wave.groups[g];
            if (group == null || group.count <= 0)
            {
                continue;
            }

            int kind = 0;
            EnemyBase stat = enemy1;
            if (group.kind == StageEnemyKind.Enemy2)
            {
                kind = 1;
                stat = enemy2;
            }
            else if (group.kind == StageEnemyKind.Enemy3)
            {
                kind = 2;
                stat = enemy3;
            }

            stat.hp *= hpMul;
            for (int n = 0; n < group.count; n++)
            {
                SpawnSlot slot = new SpawnSlot();
                slot.kind = kind;
                slot.stat = stat;
                slots.Add(slot);
            }
        }

        return slots.ToArray();
    }

    private static EnemyBase Scale(StageEnemyStat src, float mul)
    {
        if (src == null)
        {
            return new EnemyBase(1f, 1f, 0.5f);
        }

        return new EnemyBase(src.hp * mul, src.damage * mul, src.speed * mul);
    }
}
