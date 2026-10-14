using UnityEngine;

/// <summary>
/// Tutorial-facing helpers for raft battle (stage 1-1 scripted flow).
/// </summary>
public partial class BattleDirector
{
    public bool TutorialReady => _ready;
    public bool TutorialIsLevelUpOpen => _phase == Phase.LevelUp;
    public bool TutorialIsVictory => _phase == Phase.Victory;
    public bool TutorialIsFight => _phase == Phase.Fight;

    public int TutorialAliveEnemyCount()
    {
        int n = 0;
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i] != null && !_enemies[i].Dead)
            {
                n++;
            }
        }

        return n;
    }

    public bool TutorialHasBuilding(BattleBuildingKind kind)
    {
        return HasBuilding(kind);
    }

    public Transform TutorialFindResourceInRange()
    {
        float limit = _characterRange;
        float best = float.MaxValue;
        ResNode found = null;
        for (int i = 0; i < _resources.Count; i++)
        {
            ResNode res = _resources[i];
            if (!IsGuideResource(res))
            {
                continue;
            }

            float d = Sqr2(res.Pos, _characterPos);
            if (d <= limit * limit && d < best)
            {
                best = d;
                found = res;
            }
        }

        return found != null && found.View != null ? found.View.transform : null;
    }

    public Transform TutorialFindPlayerNet()
    {
        if (_playerNet == null || _playerNet.Dead || _playerNet.View == null)
        {
            return null;
        }

        return _playerNet.View.transform;
    }

    public Transform TutorialFindBuildGhost()
    {
        for (int i = 0; i < _ghostPlanks.Count; i++)
        {
            if (_ghostPlanks[i] != null)
            {
                return _ghostPlanks[i].transform;
            }
        }

        return null;
    }

    public Transform TutorialFindEmptyPlank()
    {
        PlankNode plank = FindEmptyPlank();
        if (plank == null || plank.View == null)
        {
            return null;
        }

        return plank.View.transform;
    }

    public Transform TutorialFindHarvestResource()
    {
        PlankNode tower = FindCaptureTower();
        if (tower == null)
        {
            return null;
        }

        float range = _config != null ? _config.CaptureRange : 3f;
        ResNode res = NearestResource(tower.Logic, range);
        return res != null && res.View != null ? res.View.transform : null;
    }

    public void TutorialEnsureHarvestResource()
    {
        if (!TutorialBattleHooks.Active || !_ready || FindCaptureTower() == null)
        {
            return;
        }

        if (TutorialFindHarvestResource() != null)
        {
            return;
        }

        PlankNode tower = FindCaptureTower();
        float range = _config != null ? _config.CaptureRange : 3f;
        Vector3 pos = tower.Logic + new Vector3(range * 0.55f, 0.35f, 0f);
        SpawnDriftOne(BattleResourceKind.Wood, pos, Vector3.zero);
    }

    public Transform TutorialFindMeat()
    {
        for (int i = 0; i < _resources.Count; i++)
        {
            ResNode res = _resources[i];
            if (res != null && !res.Dead && res.Kind == BattleResourceKind.Meat && res.View != null)
            {
                return res.View.transform;
            }
        }

        return null;
    }

    public Transform TutorialFindFirstEnemy()
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyNode e = _enemies[i];
            if (e != null && !e.Dead && e.View != null)
            {
                return e.View.transform;
            }
        }

        return null;
    }

    public void TutorialSelectNet()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        SelectNet();
    }

    public void TutorialPrepareEconomy()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        // Enough for plank + capture (wood) and attack tower (iron).
        if (_wood < 24)
        {
            _wood = 24;
        }

        if (_iron < 8)
        {
            _iron = 8;
        }

        TouchHud();
    }

    public void TutorialEnsureGuideResource()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        if (TutorialFindResourceInRange() != null)
        {
            TutorialBattleHooks.MarkGuideResourceSpawned();
            return;
        }

        Vector3 pos;
        if (!TryPickVisibleGuidePoint(out pos))
        {
            pos = _characterPos + new Vector3(_characterRange * 0.7f, _characterRange * 0.35f, 0f);
        }

        SpawnDriftOne(BattleResourceKind.Wood, pos, Vector3.zero);
        if (_resources.Count > 0)
        {
            RaiseAbovePlanks(_resources[_resources.Count - 1]);
        }

        TutorialBattleHooks.MarkGuideResourceSpawned();
        TouchHud();
    }

    /// <summary>圈内、画面里，并且整块物资都在木板外面。</summary>
    private bool IsGuideResource(ResNode res)
    {
        if (res == null || res.Dead || res.View == null)
        {
            return false;
        }

        if (res.Kind != BattleResourceKind.Wood && res.Kind != BattleResourceKind.Iron)
        {
            return false;
        }

        if (!IsVisibleInView(res.Pos) || BlockedByPlank(res.Pos))
        {
            return false;
        }

        RaiseAbovePlanks(res);
        return true;
    }

    private bool TryPickVisibleGuidePoint(out Vector3 pos)
    {
        float maxDist = Mathf.Max(1.6f, _characterRange * 0.85f);
        float[] distances = { 1.7f, 2.3f, 2.9f, maxDist };
        for (int d = 0; d < distances.Length; d++)
        {
            for (int i = 0; i < 16; i++)
            {
                float ang = (i * 22.5f + 15f) * Mathf.Deg2Rad;
                float dist = distances[d];
                Vector3 candidate = _characterPos + new Vector3(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist, 0f);
                if (Sqr2(candidate, _characterPos) > _characterRange * _characterRange)
                {
                    continue;
                }

                if (!IsVisibleInView(candidate) || BlockedByPlank(candidate))
                {
                    continue;
                }

                pos = candidate;
                return true;
            }
        }

        pos = _characterPos;
        return false;
    }

    /// <summary>物资本体不能和任何木板重叠，避免被木板盖住或点到木板。</summary>
    private bool BlockedByPlank(Vector3 pos)
    {
        float keep = PlankHalfExtent() + 0.75f;
        float keepSqr = keep * keep;
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead)
            {
                continue;
            }

            if (Sqr2(plank.Logic, pos) <= keepSqr)
            {
                return true;
            }
        }

        return false;
    }

    private static void RaiseAbovePlanks(ResNode res)
    {
        if (res != null && res.View != null)
        {
            res.View.SetSortingOrder(12);
        }
    }

    private bool IsVisibleInView(Vector3 world)
    {
        if (_cam == null)
        {
            return true;
        }

        Vector3 view = _cam.WorldToViewportPoint(world);
        return view.z > 0f
            && view.x > 0.12f && view.x < 0.88f
            && view.y > 0.22f && view.y < 0.82f;
    }

    public void TutorialForceLevelUp()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        if (_phase == Phase.LevelUp)
        {
            return;
        }

        if (_phase != Phase.Fight)
        {
            return;
        }

        int need = _config.XpToNext(_level);
        if (_xp < need)
        {
            _xp = need;
        }

        TryOpenLevelUp();
        if (_phase == Phase.LevelUp)
        {
            TutorialBattleHooks.Raise(TutorialBattleHooks.EvLevelUpOpened);
        }
    }

    public void TutorialSpawnEnemyNearPlayer()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        if (TutorialBattleHooks.GuideEnemy1Spawned && TutorialAliveEnemyCount() > 0)
        {
            return;
        }

        Vector3 pos = _characterPos + new Vector3(_characterRange * 0.7f, _characterRange * 0.15f, 0f);
        SpawnTutorialEnemy(pos, 8f, 1f, 0.45f);
        TutorialBattleHooks.MarkGuideEnemy1Spawned();
    }

    public void TutorialSpawnEnemyNearTower()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        if (TutorialBattleHooks.GuideEnemy2Spawned && TutorialAliveEnemyCount() > 0)
        {
            return;
        }

        Vector3 anchor = _characterPos;
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode p = _planks[i];
            if (p != null && !p.Dead && p.Building == BattleBuildingKind.Attack)
            {
                anchor = p.Logic;
                break;
            }
        }

        Vector3 pos = anchor + new Vector3(2.2f, 1.2f, 0f);
        SpawnTutorialEnemy(pos, 10f, 1f, 0.4f);
        TutorialBattleHooks.MarkGuideEnemy2Spawned();
    }

    public void TutorialRequestVictory()
    {
        if (!TutorialBattleHooks.Active || !_ready)
        {
            return;
        }

        if (_phase == Phase.Victory)
        {
            return;
        }

        // Clear leftover enemies so Win() path is clean, then force win.
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            EnemyNode e = _enemies[i];
            if (e != null && !e.Dead)
            {
                e.Dead = true;
                if (e.View != null)
                {
                    Object.Destroy(e.View.gameObject);
                }

                _enemies.RemoveAt(i);
            }
        }

        _wavesDone = true;
        TutorialBattleHooks.MarkVictoryForced();
        if (_phase == Phase.Fight)
        {
            Win();
        }
    }

    private PlankNode FindCaptureTower()
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank != null && !plank.Dead && plank.Building == BattleBuildingKind.Capture)
            {
                return plank;
            }
        }

        return null;
    }

    private void SpawnTutorialEnemy(Vector3 pos, float hp, float damage, float speed)
    {
        BattleEnemy prefab = PrefabForKind(0);
        if (prefab == null)
        {
            return;
        }

        EnemyNode enemy = new EnemyNode();
        enemy.Pos = pos;
        enemy.Hp = hp;
        enemy.MaxHp = hp;
        enemy.Damage = damage;
        enemy.MoveSpeed = speed;
        enemy.AttackTimer = _config != null ? _config.EnemyAttackInterval : 1.5f;
        enemy.View = Object.Instantiate(prefab, spawnRoot);
        enemy.View.SetBodyScale(0.62f);
        enemy.View.transform.position = pos;
        enemy.View.SetHealth01(1f);
        enemy.View.FaceToward(_characterPos - pos);
        _enemies.Add(enemy);
    }
}
