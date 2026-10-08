using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 战斗流程。挂在 BattleRoot 上。水面、初始木板、角色、HUD 在场景里。运行时只实例化资源、螃蟹、网、矛和后加的木板/塔。
/// </summary>
[DisallowMultipleComponent]
public class BattleDirector : MonoBehaviour
{
    private enum Phase
    {
        Fight,
        LevelUp,
        Victory,
        Defeat
    }

    private enum PressKind
    {
        None,
        Ui,
        Launch,
        Reel,
        Spear
    }

    private class PlankNode
    {
        public PlankView View;
        public int X;
        public int Y;
        public float Hp;
        public bool Dead;
        public Vector3 Logic;
        public Color BaseColor;
        public BattleBuildingKind Building;
        public BattleTower Tower;
        public NetShot Net;
        public float AttackCd;
        public float CaptureCd;
    }

    private class ResNode
    {
        public BattleResource View;
        public BattleResourceKind Kind;
        public Vector3 Pos;
        public Vector3 Velocity;
        public bool Dead;
    }

    private class EnemyNode
    {
        public BattleCrab View;
        public Vector3 Pos;
        public float Hp;
        public float AttackTimer;
        public bool Dead;
    }

    private class NetShot
    {
        public BattleNet View;
        public Vector3 Pos;
        public Vector3 Target;
        public bool Returning;
        public bool Waiting;
        public bool DidCapture;
        public bool ReelHold;
        public bool AutoReel;
        public bool Dead;
        public PlankNode Home;
        public readonly List<ResNode> Held = new List<ResNode>();
    }

    private class SpearShot
    {
        public BattleSpear View;
        public Vector3 Pos;
        public Vector3 Origin;
        public Vector3 Target;
        public float Damage;
        public float Radius;
        public EnemyNode Lock;
        public bool Done;
    }

    private struct ViewBounds
    {
        public float Cx;
        public float Cy;
        public float HalfW;
        public float HalfH;
    }

    [SerializeField] private BattleHudView hud;
    [SerializeField] private Transform water;
    [SerializeField] private Transform character;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private PlankView[] startingPlanks;
    [SerializeField] private PlankView plankPrefab;
    [SerializeField] private BattleResource resourcePrefab;
    [SerializeField] private BattleCrab crabPrefab;
    [SerializeField] private BattleNet netPrefab;
    [SerializeField] private BattleSpear spearPrefab;
    [SerializeField] private BattleTower captureTowerPrefab;
    [SerializeField] private BattleTower attackTowerPrefab;

    private BattleTuningConfig _config;
    private bool _ready;
    private Phase _phase;
    private int _chapter = 1;
    private int _stage = 1;
    private int _level = 1;
    private int _xp;
    private int _pendingNeed;
    private int _wood;
    private int _leaf;
    private int _scrap;
    private int _waveCount = 2;
    private int _waveIndex;
    private int _spawnedInWave;
    private int _crabSerial;
    private float _driftTimer;
    private float _waveTimer;
    private bool _wavesDone;
    private bool _hudDirty;
    private bool _leaving;
    private int _modalArm;
    private PressKind _press;
    private BattleWeapon _weapon = BattleWeapon.Net;
    private Camera _cam;
    private Vector3 _characterPos;
    private float _characterRange = 4f;
    private int _netCapacity = 4;
    private BattleCharacter _characterBody;
    private int _charX;
    private int _charY;
    private bool _walking;
    private int _walkX;
    private int _walkY;
    private CanvasGroup _modalGroup;
    private NetShot _playerNet;

    private readonly List<PlankNode> _planks = new List<PlankNode>();
    private readonly List<ResNode> _resources = new List<ResNode>();
    private readonly List<EnemyNode> _enemies = new List<EnemyNode>();
    private readonly List<SpearShot> _spears = new List<SpearShot>();
    private readonly List<RaycastResult> _hits = new List<RaycastResult>();

    /// <summary>运行时才出现的预制体，由 YooAsset 按地址加载后塞进来。</summary>
    public void SetSpawnPrefabs(PlankView plank, BattleResource resource, BattleCrab crab, BattleNet net, BattleSpear spear, BattleTower capture, BattleTower attack)
    {
        plankPrefab = plank;
        resourcePrefab = resource;
        crabPrefab = crab;
        netPrefab = net;
        spearPrefab = spear;
        captureTowerPrefab = capture;
        attackTowerPrefab = attack;
    }

    public void Begin(BattleTuningConfig config)
    {
        if (_ready || config == null)
        {
            Debug.LogError("[Battle] Begin skipped. Config missing or already started.");
            return;
        }

        if (hud == null || plankPrefab == null || resourcePrefab == null || crabPrefab == null ||
            netPrefab == null || spearPrefab == null || captureTowerPrefab == null || attackTowerPrefab == null)
        {
            Debug.LogError("[Battle] BattleRoot is missing a serialized prefab or HUD reference.");
            return;
        }

        _config = config;
        _cam = Camera.main;
        if (_cam != null)
        {
            _cam.orthographic = true;
            _cam.orthographicSize = _config.CameraSize;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.18f, 0.32f, 1f);
            if (Mathf.Abs(_cam.transform.position.z) < 1f)
            {
                _cam.transform.position = new Vector3(_cam.transform.position.x, _cam.transform.position.y, -10f);
            }
        }

        ResolveStage();
        _characterPos = CellCenter(0, 0);
        if (character != null)
        {
            character.position = _characterPos;
            _characterBody = character.GetComponent<BattleCharacter>();
        }

        _characterRange = _config.CharacterRange;
        _netCapacity = _config.NetCapacity;
        RefreshCharacterRange();

        if (startingPlanks != null)
        {
            for (int i = 0; i < startingPlanks.Length; i++)
            {
                AdoptPlank(startingPlanks[i]);
            }
        }

        _driftTimer = _config.DriftInterval;
        _waveTimer = _config.CrabInterval;
        _ready = true;
        hud.SetWeapon(_weapon);
        TouchHud();
        Debug.Log("[Battle] stage " + _chapter + "-" + _stage + " waves " + _waveCount);
    }

    private void Update()
    {
        if (!_ready)
        {
            return;
        }

        TickModalArm();
        if (_phase != Phase.Fight || _leaving)
        {
            return;
        }

        float dt = Time.deltaTime;
        HandleInput(dt);
        if (_phase != Phase.Fight) return;
        TickCharacter(dt);
        if (_phase != Phase.Fight) return;
        TickResources(dt);
        if (_phase != Phase.Fight) return;
        TickEnemies(dt);
        if (_phase != Phase.Fight) return;
        TickTowers(dt);
        if (_phase != Phase.Fight) return;
        TickNets(dt);
        if (_phase != Phase.Fight) return;
        TickSpears(dt);
        if (_phase != Phase.Fight) return;
        TickDriftSpawn(dt);
        if (_phase != Phase.Fight) return;
        TickWaves(dt);
        if (_phase != Phase.Fight) return;
        TryVictory();
    }

    private void LateUpdate()
    {
        if (!_ready || !_hudDirty || hud == null)
        {
            return;
        }

        _hudDirty = false;
        hud.SetCounts(_wood, _leaf, _scrap);
        hud.SetXp(_level, _xp, _config.XpToNext(_level));
    }

    public void SelectNet()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        _weapon = BattleWeapon.Net;
        hud.SetWeapon(_weapon);
    }

    public void SelectSpear()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        _weapon = BattleWeapon.Spear;
        if (_playerNet != null)
        {
            _playerNet.ReelHold = false;
        }

        if (_press == PressKind.Reel)
        {
            _press = PressKind.None;
        }

        hud.SetWeapon(_weapon);
    }

    public void ChoosePlank()
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        int x;
        int y;
        if (!FindExpansionCell(out x, out y))
        {
            hud.ShowLevelUp(false, FindEmptyPlank() != null);
            return;
        }

        SpawnPlank(x, y);
        CommitLevelUp();
    }

    public void ChooseCapture()
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        PlankNode spot = FindEmptyPlank();
        if (spot == null)
        {
            int dumpX;
            int dumpY;
            hud.ShowLevelUp(FindExpansionCell(out dumpX, out dumpY), false);
            return;
        }

        BuildTower(spot, BattleBuildingKind.Capture);
        CommitLevelUp();
    }

    public void ChooseAttack()
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        PlankNode spot = FindEmptyPlank();
        if (spot == null)
        {
            int dumpX;
            int dumpY;
            hud.ShowLevelUp(FindExpansionCell(out dumpX, out dumpY), false);
            return;
        }

        BuildTower(spot, BattleBuildingKind.Attack);
        CommitLevelUp();
    }

    public void ChooseRange()
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        _characterRange += _config.CharacterRangeStep;
        RefreshCharacterRange();
        CommitLevelUp();
    }

    public void ChooseCapacity()
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        _netCapacity += _config.NetCapacityStep;
        CommitLevelUp();
    }

    public void OnPrimary()
    {
        if (_modalArm > 0)
        {
            return;
        }

        if (_phase == Phase.Victory)
        {
            GoNext();
        }
        else if (_phase == Phase.Defeat)
        {
            LoadSceneSafe(MainUIController.BattleSceneName);
        }
    }

    public void OnBack()
    {
        LoadSceneSafe(LoadSceneController.MainSceneName);
    }

    private void TickModalArm()
    {
        if (_modalArm <= 0)
        {
            return;
        }

        _modalArm--;
        if (_modalArm == 0)
        {
            hud.SetGroupBlocks(_modalGroup, true);
        }
    }

    private void ResolveStage()
    {
        _chapter = 1;
        _stage = 1;
        if (StageSelection.HasSelection)
        {
            StageSelection.Pick pick = StageSelection.Current;
            if (StageDefs.IsValid(pick.chapter, pick.stage))
            {
                _chapter = pick.chapter;
                _stage = pick.stage;
            }
        }

        _level = 1;
        _xp = 0;
        _waveCount = _config.WaveCountFor(_config.StageIndex(_chapter, _stage));
    }

    private void HandleInput(float dt)
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (PointerOverUi())
            {
                _press = PressKind.Ui;
            }
            else
            {
                Vector3 point = MouseWorld();
                PlankNode step = PlankUnder(point);
                if (step != null && !(_playerNet != null && _weapon == BattleWeapon.Spear))
                {
                    BeginWalk(step);
                    _press = PressKind.None;
                }
                else if (_playerNet != null && _weapon == BattleWeapon.Net)
                {
                    _press = PressKind.Reel;
                }
                else if (_weapon == BattleWeapon.Net)
                {
                    if (InsideCharacterRange(point))
                    {
                        LaunchPlayerNet(point);
                        _press = PressKind.Launch;
                    }
                    else
                    {
                        _press = PressKind.None;
                    }
                }
                else if (InsideCharacterRange(point))
                {
                    ThrowPlayerSpear(point);
                    _press = PressKind.Spear;
                }
                else
                {
                    _press = PressKind.None;
                }
            }
        }

        if (_press == PressKind.Reel && _playerNet != null && Input.GetMouseButton(0))
        {
            _playerNet.ReelHold = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (_playerNet != null)
            {
                _playerNet.ReelHold = false;
            }

            _press = PressKind.None;
        }
    }

    private bool PointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData data = new PointerEventData(EventSystem.current);
        data.position = Input.mousePosition;
        _hits.Clear();
        EventSystem.current.RaycastAll(data, _hits);
        return _hits.Count > 0;
    }

    private bool InsideCharacterRange(Vector3 point)
    {
        float limit = _characterRange;
        return Sqr2(point, _characterPos) <= limit * limit;
    }

    private void RefreshCharacterRange()
    {
        if (_characterBody != null)
        {
            _characterBody.SetRangeRadius(_characterRange);
        }
    }
    private Vector3 MouseWorld()
    {
        if (_cam == null)
        {
            return _characterPos;
        }

        Vector3 screen = Input.mousePosition;
        screen.z = Mathf.Abs(_cam.transform.position.z);
        Vector3 world = _cam.ScreenToWorldPoint(screen);
        world.z = 0f;
        return world;
    }

    private void TickResources(float dt)
    {
        ViewBounds view = GetView();
        for (int i = _resources.Count - 1; i >= 0; i--)
        {
            ResNode res = _resources[i];
            if (res.Dead)
            {
                _resources.RemoveAt(i);
                continue;
            }

            res.Pos += res.Velocity * dt;
            bool offLeft = res.Pos.x < view.Cx - view.HalfW - 1.5f;
            bool offBottom = res.Pos.y < view.Cy - view.HalfH - 1.5f;
            bool far = res.Pos.x < view.Cx - view.HalfW - 12f || res.Pos.y < view.Cy - view.HalfH - 12f;
            if ((offLeft && offBottom) || far)
            {
                res.Dead = true;
                if (res.View != null)
                {
                    Destroy(res.View.gameObject);
                }

                _resources.RemoveAt(i);
                continue;
            }

            if (res.View != null)
            {
                res.View.transform.position = res.Pos;
            }
        }
    }

    private void TickDriftSpawn(float dt)
    {
        _driftTimer -= dt;
        if (_driftTimer > 0f)
        {
            return;
        }

        _driftTimer = _config.DriftInterval;
        SpawnResource();
    }

    private void SpawnResource()
    {
        ViewBounds view = GetView();
        float x;
        float y;
        if (Random.value < 0.5f)
        {
            x = Random.Range(view.Cx + view.HalfW * 0.05f, view.Cx + view.HalfW + 2.2f);
            y = view.Cy + view.HalfH + Random.Range(1f, 2.4f);
        }
        else
        {
            x = view.Cx + view.HalfW + Random.Range(1f, 2.4f);
            y = Random.Range(view.Cy + view.HalfH * 0.05f, view.Cy + view.HalfH + 2.2f);
        }

        ResNode res = new ResNode();
        res.Kind = Random.value < 0.5f ? BattleResourceKind.Wood : BattleResourceKind.Leaf;
        res.Pos = new Vector3(x, y, 0f);
        Vector3 dir = new Vector3(-1f, -1f, 0f).normalized;
        float jitter = Random.Range(-0.22f, 0.22f);
        dir = new Vector3(dir.x + jitter, dir.y, 0f).normalized;
        res.Velocity = dir * _config.DriftSpeed;
        res.View = Instantiate(resourcePrefab, spawnRoot);
        res.View.SetKind(res.Kind);
        res.View.SetSortingOrder(12);
        res.View.transform.position = res.Pos;
        _resources.Add(res);
    }

    private void SpawnMeat(Vector3 pos)
    {
        ResNode res = new ResNode();
        res.Kind = BattleResourceKind.Meat;
        res.Pos = pos;
        Vector3 dir = new Vector3(-1f, -1f, 0f).normalized;
        float jitter = Random.Range(-0.22f, 0.22f);
        dir = new Vector3(dir.x + jitter, dir.y, 0f).normalized;
        res.Velocity = dir * (_config.DriftSpeed / 3f);
        res.View = Instantiate(resourcePrefab, spawnRoot);
        res.View.SetKind(res.Kind);
        res.View.SetSortingOrder(12);
        res.View.transform.position = res.Pos;
        _resources.Add(res);
    }
    private void TickWaves(float dt)
    {
        if (_wavesDone)
        {
            return;
        }

        _waveTimer -= dt;
        if (_waveTimer > 0f)
        {
            return;
        }

        if (_waveIndex >= _waveCount)
        {
            _wavesDone = true;
            return;
        }

        SpawnCrab();
        _spawnedInWave++;
        int need = 2 + _waveIndex;
        if (_spawnedInWave >= need)
        {
            _waveIndex++;
            _spawnedInWave = 0;
            if (_waveIndex >= _waveCount)
            {
                _wavesDone = true;
            }
            else
            {
                _waveTimer = _config.WaveGap;
            }
        }
        else
        {
            _waveTimer = _config.CrabInterval;
        }
    }

    private void SpawnCrab()
    {
        ViewBounds view = GetView();
        Vector3 pos;
        if ((_crabSerial & 1) == 0)
        {
            pos = new Vector3(
                view.Cx + view.HalfW + 1.8f,
                Random.Range(view.Cy - view.HalfH * 0.85f, view.Cy + view.HalfH * 0.95f),
                0f);
        }
        else
        {
            pos = new Vector3(
                Random.Range(view.Cx - view.HalfW * 0.1f, view.Cx + view.HalfW * 0.95f),
                view.Cy + view.HalfH + 1.8f,
                0f);
        }

        _crabSerial++;
        if (PlankUnder(pos) != null || Sqr2(pos, _characterPos) <= TouchSqr())
        {
            pos.x += 3f;
            pos.y += 3f;
        }

        EnemyNode enemy = new EnemyNode();
        enemy.Pos = pos;
        enemy.Hp = _config.EnemyHp;
        enemy.AttackTimer = _config.EnemyAttackInterval;
        enemy.View = Instantiate(crabPrefab, spawnRoot);
        enemy.View.SetBodyScale(0.62f);
        enemy.View.transform.position = pos;
        enemy.View.SetHealth01(1f);
        _enemies.Add(enemy);
    }

    private void TickEnemies(float dt)
    {
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            if (_phase != Phase.Fight)
            {
                return;
            }

            TickEnemy(_enemies[i], dt);
        }
    }

    private void TickEnemy(EnemyNode enemy, float dt)
    {
        if (enemy == null || enemy.Dead)
        {
            return;
        }

        PlankNode plank = PlankTouchedByCrab(enemy);
        if (plank != null)
        {
            enemy.AttackTimer -= dt;
            if (enemy.AttackTimer <= 0f)
            {
                enemy.AttackTimer += _config.EnemyAttackInterval;
                DamagePlank(plank, _config.EnemyAttackDamage);
            }

            PlaceEnemy(enemy);
            return;
        }

        enemy.AttackTimer = _config.EnemyAttackInterval;
        if (ReachedCharacter(enemy))
        {
            Lose();
            return;
        }

        enemy.Pos = Vector3.MoveTowards(enemy.Pos, _characterPos, _config.EnemyMoveSpeed * dt);
        if (PlankTouchedByCrab(enemy) != null)
        {
            PlaceEnemy(enemy);
            return;
        }

        if (ReachedCharacter(enemy))
        {
            Lose();
            return;
        }

        PlaceEnemy(enemy);
    }

    private void PlaceEnemy(EnemyNode enemy)
    {
        if (enemy.View == null)
        {
            return;
        }

        enemy.View.transform.position = enemy.Pos;
        float ratio = _config.EnemyHp <= 0f ? 0f : Mathf.Clamp01(enemy.Hp / _config.EnemyHp);
        enemy.View.SetHealth01(ratio);
    }

    private bool ReachedCharacter(EnemyNode enemy)
    {
        if (PlankAt(_charX, _charY) != null)
        {
            return false;
        }

        float reach = CrabRadius(enemy) + PlankHalfExtent();
        return Sqr2(enemy.Pos, CellCenter(_charX, _charY)) <= reach * reach;
    }

    private void BeginWalk(PlankNode plank)
    {
        if (plank == null || plank.Dead)
        {
            return;
        }

        if (!_walking && plank.X == _charX && plank.Y == _charY)
        {
            return;
        }

        _walking = true;
        _walkX = plank.X;
        _walkY = plank.Y;
    }

    private void TickCharacter(float dt)
    {
        if (!_walking)
        {
            return;
        }

        PlankNode dest = PlankAt(_walkX, _walkY);
        if (dest == null)
        {
            _walking = false;
            return;
        }

        const float speed = 3f;
        _characterPos = Vector3.MoveTowards(_characterPos, dest.Logic, speed * dt);
        if (character != null)
        {
            character.position = _characterPos;
        }

        if (Sqr2(_characterPos, dest.Logic) > 0.0004f)
        {
            return;
        }

        _characterPos = dest.Logic;
        if (character != null)
        {
            character.position = _characterPos;
        }

        _charX = dest.X;
        _charY = dest.Y;
        _walking = false;
    }

    private PlankNode PlankTouchedByCrab(EnemyNode enemy)
    {
        float reach = CrabRadius(enemy);
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank.Dead)
            {
                continue;
            }

            float limit = reach + PlankRadius(plank);
            if (Sqr2(plank.Logic, enemy.Pos) <= limit * limit)
            {
                return plank;
            }
        }

        return null;
    }

    private static float CrabRadius(EnemyNode enemy)
    {
        if (enemy != null && enemy.View != null)
        {
            float radius = enemy.View.BodyRadius();
            if (radius > 0.001f)
            {
                return radius;
            }
        }

        return 0.31f;
    }

    private float PlankRadius(PlankNode plank)
    {
        if (plank != null && plank.View != null)
        {
            float radius = plank.View.BodyRadius();
            if (radius > 0.001f)
            {
                return radius;
            }
        }

        return PlankHalfExtent();
    }

    private float PlankHalfExtent()
    {
        return _config.CellSize * 0.45f;
    }

    private void DamageEnemy(EnemyNode enemy, float damage)
    {
        if (enemy == null || enemy.Dead)
        {
            return;
        }

        enemy.Hp -= damage;
        if (enemy.Hp <= 0f)
        {
            KillEnemy(enemy);
            return;
        }

        PlaceEnemy(enemy);
    }

    private void KillEnemy(EnemyNode enemy)
    {
        if (enemy == null || enemy.Dead)
        {
            return;
        }

        enemy.Dead = true;
        enemy.Hp = 0f;
        SpawnMeat(enemy.Pos);
        if (enemy.View != null)
        {
            Destroy(enemy.View.gameObject);
        }

        _enemies.Remove(enemy);
    }

    private void AdoptPlank(PlankView view)
    {
        if (view == null)
        {
            return;
        }

        PlankNode node = new PlankNode();
        node.View = view;
        node.X = view.CellX;
        node.Y = view.CellY;
        node.Hp = _config.PlankHp;
        node.Logic = CellCenter(node.X, node.Y);
        view.transform.position = node.Logic;
        view.SetBodyScale(_config.CellSize * 0.9f);
        view.Paint(_config.WoodPlankColor);
        node.BaseColor = view.BaseColor;
        _planks.Add(node);
    }

    private PlankNode SpawnPlank(int x, int y)
    {
        PlankNode existing = PlankAt(x, y);
        if (existing != null)
        {
            return existing;
        }

        PlankView view = Instantiate(plankPrefab, spawnRoot);
        view.SetCell(x, y);
        AdoptPlank(view);
        return PlankAt(x, y);
    }

    private void BuildTower(PlankNode plank, BattleBuildingKind kind)
    {
        if (plank == null || plank.Dead || plank.Building != BattleBuildingKind.None)
        {
            return;
        }

        plank.Building = kind;
        if (plank.Tower != null)
        {
            Destroy(plank.Tower.gameObject);
        }

        BattleTower prefab = kind == BattleBuildingKind.Capture ? captureTowerPrefab : attackTowerPrefab;
        Transform parent = plank.View != null && plank.View.TowerAnchor != null ? plank.View.TowerAnchor : plank.View.transform;
        BattleTower tower = Instantiate(prefab, parent);
        tower.transform.localPosition = Vector3.zero;
        tower.transform.localScale = Vector3.one * (_config.CellSize * 0.42f);
        Color color = kind == BattleBuildingKind.Capture ? _config.CaptureTowerColor : _config.AttackTowerColor;
        tower.ApplyColor(color);
        float radius = kind == BattleBuildingKind.Capture ? _config.CaptureRange : _config.TowerRange;
        tower.SetRangeRadius(radius);
        plank.Tower = tower;
    }

    private void DamagePlank(PlankNode plank, float damage)
    {
        if (plank == null || plank.Dead)
        {
            return;
        }

        plank.Hp -= damage;
        if (plank.Hp <= 0f)
        {
            RemovePlank(plank);
            return;
        }

        RefreshPlank(plank);
    }

    private void RemovePlank(PlankNode plank)
    {
        if (plank == null || plank.Dead)
        {
            return;
        }

        plank.Dead = true;
        plank.Hp = 0f;
        if (plank.Net != null)
        {
            CancelNet(plank.Net);
        }

        if (plank.View != null)
        {
            Destroy(plank.View.gameObject);
        }

        _planks.Remove(plank);
    }

    private void RefreshPlank(PlankNode plank)
    {
        if (plank == null || plank.Dead || plank.View == null)
        {
            return;
        }

        plank.View.transform.position = plank.Logic;
        float ratio = _config.PlankHp <= 0f ? 0f : Mathf.Clamp01(plank.Hp / _config.PlankHp);
        plank.View.SetHealth01(ratio);
    }

    private PlankNode PlankAt(int x, int y)
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (!plank.Dead && plank.X == x && plank.Y == y)
            {
                return plank;
            }
        }

        return null;
    }

    private PlankNode PlankUnder(Vector3 pos)
    {
        float limit = TouchSqr();
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank.Dead)
            {
                continue;
            }

            if (Sqr2(plank.Logic, pos) <= limit)
            {
                return plank;
            }
        }

        return null;
    }

    private bool FindExpansionCell(out int x, out int y)
    {
        x = 0;
        y = 0;
        var occupied = new HashSet<Vector2Int>();
        var anchors = new List<Vector2Int>();
        for (int i = 0; i < _planks.Count; i++)
        {
            if (_planks[i].Dead)
            {
                continue;
            }

            Vector2Int cell = new Vector2Int(_planks[i].X, _planks[i].Y);
            occupied.Add(cell);
            anchors.Add(cell);
        }

        if (anchors.Count == 0)
        {
            anchors.Add(new Vector2Int(0, 0));
        }

        int[] dx = { 1, 0, -1, 0 };
        int[] dy = { 0, 1, 0, -1 };
        for (int d = 0; d < 4; d++)
        {
            for (int a = 0; a < anchors.Count; a++)
            {
                int nx = anchors[a].x + dx[d];
                int ny = anchors[a].y + dy[d];
                if (!occupied.Contains(new Vector2Int(nx, ny)))
                {
                    x = nx;
                    y = ny;
                    return true;
                }
            }
        }

        return false;
    }

    private PlankNode FindEmptyPlank()
    {
        PlankNode onCharacter = null;
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank.Dead || plank.Building != BattleBuildingKind.None)
            {
                continue;
            }

            if (plank.X == 0 && plank.Y == 0)
            {
                onCharacter = plank;
                continue;
            }

            return plank;
        }

        return onCharacter;
    }

    private void TickTowers(float dt)
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank.Dead)
            {
                continue;
            }

            if (plank.Building == BattleBuildingKind.Attack)
            {
                plank.AttackCd -= dt;
                if (plank.AttackCd <= 0f)
                {
                    EnemyNode target = NearestEnemy(plank.Logic, _config.TowerRange);
                    if (target != null)
                    {
                        ThrowTowerSpear(plank, target);
                        plank.AttackCd = _config.TowerAttackInterval;
                    }
                }
            }
            else if (plank.Building == BattleBuildingKind.Capture)
            {
                plank.CaptureCd -= dt;
                if (plank.CaptureCd <= 0f && plank.Net == null)
                {
                    ResNode res = NearestResource(plank.Logic, _config.CaptureRange);
                    if (res != null)
                    {
                        CreateNet(plank.Logic, res.Pos, plank, true);
                        plank.CaptureCd = _config.CaptureCooldown;
                    }
                }
            }
        }
    }

    private void LaunchPlayerNet(Vector3 point)
    {
        if (_playerNet != null)
        {
            return;
        }

        _playerNet = CreateNet(_characterPos, point, null, false);
    }

    private NetShot CreateNet(Vector3 origin, Vector3 target, PlankNode home, bool autoReel)
    {
        NetShot net = new NetShot();
        net.Pos = origin;
        net.Target = target;
        net.Home = home;
        net.AutoReel = autoReel;
        net.View = Instantiate(netPrefab, spawnRoot);
        float diameter = _config.NetDiameter;
        net.View.transform.localScale = new Vector3(diameter, diameter, 1f);
        net.View.transform.position = origin;
        if (home != null)
        {
            home.Net = net;
        }

        return net;
    }

    private void TickNets(float dt)
    {
        if (_playerNet != null)
        {
            TickOneNet(_playerNet, dt);
        }

        for (int i = 0; i < _planks.Count; i++)
        {
            if (_planks[i].Net != null)
            {
                TickOneNet(_planks[i].Net, dt);
            }
        }
    }

    private void TickOneNet(NetShot net, float dt)
    {
        if (net == null || net.Dead)
        {
            return;
        }

        if (net.Home != null && net.Home.Dead)
        {
            CancelNet(net);
            return;
        }

        Vector3 home = net.Home != null ? net.Home.Logic : _characterPos;
        bool wantReel = net.AutoReel || net.ReelHold;
        if (!net.Returning)
        {
            if (!net.Waiting)
            {
                if (wantReel && !net.AutoReel)
                {
                    net.Returning = true;
                }
                else
                {
                    float fly = _config.NetSpeed * dt;
                    net.Pos = Vector3.MoveTowards(net.Pos, net.Target, fly);
                    if (Sqr2(net.Pos, net.Target) <= 0.0025f)
                    {
                        if (!net.DidCapture)
                        {
                            CaptureAt(net);
                            net.DidCapture = true;
                        }

                        if (net.AutoReel || wantReel)
                        {
                            net.Returning = true;
                        }
                        else
                        {
                            net.Waiting = true;
                        }
                    }
                }
            }

            if (net.Waiting && wantReel)
            {
                net.Waiting = false;
                net.Returning = true;
            }
        }

        bool reeled = false;
        if (net.Returning)
        {
            if (net.AutoReel || net.ReelHold)
            {
                ReelToward(net, home, dt);
                reeled = true;
            }

            net.ReelHold = false;
            if (Sqr2(net.Pos, home) <= 0.004f)
            {
                DeliverNet(net);
                return;
            }
        }

        if (!reeled && (net.Waiting || net.Returning))
        {
            CaptureIdleOverlap(net);
        }

        SyncNetVisual(net);
    }


    private void ReelToward(NetShot net, Vector3 home, float dt)
    {
        float timeLeft = dt;
        const float sliceTime = 0.02f;
        while (timeLeft > 0.000001f && Sqr2(net.Pos, home) > 0.004f)
        {
            float use = timeLeft < sliceTime ? timeLeft : sliceTime;
            float speed = _config.ReelHoldSpeedFor(net.Held.Count);
            if (net.ReelHold && !net.AutoReel)
            {
                speed *= 2f;
            }

            Vector3 from = net.Pos;
            net.Pos = Vector3.MoveTowards(net.Pos, home, speed * use);
            CaptureAlongPath(net, from, net.Pos);
            timeLeft -= use;
            if ((net.Pos - from).sqrMagnitude <= 0.00000001f)
            {
                break;
            }
        }
    }

    private void CaptureIdleOverlap(NetShot net)
    {
        CaptureAt(net);
    }
    private float NetCatchRadius()
    {
        return _config.NetDiameter * 0.5f;
    }

    private void CaptureAlongPath(NetShot net, Vector3 from, Vector3 to)
    {
        float limit = NetCatchRadius();
        limit *= limit;
        for (int i = _resources.Count - 1; i >= 0; i--)
        {
            ResNode res = _resources[i];
            if (res.Dead)
            {
                continue;
            }

            if (DistPointSegmentSqr(res.Pos, from, to) > limit)
            {
                continue;
            }

            TakeResource(net, i);
        }
    }

    private static float DistPointSegmentSqr(Vector3 point, Vector3 a, Vector3 b)
    {
        float abx = b.x - a.x;
        float aby = b.y - a.y;
        float len2 = abx * abx + aby * aby;
        float t = 0f;
        if (len2 > 0.000001f)
        {
            t = ((point.x - a.x) * abx + (point.y - a.y) * aby) / len2;
            if (t < 0f)
            {
                t = 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
            }
        }

        float dx = point.x - (a.x + abx * t);
        float dy = point.y - (a.y + aby * t);
        return dx * dx + dy * dy;
    }
    private void CaptureAt(NetShot net)
    {
        float limit = NetCatchRadius();
        limit *= limit;
        for (int i = _resources.Count - 1; i >= 0; i--)
        {
            ResNode res = _resources[i];
            if (res.Dead)
            {
                continue;
            }

            if (Sqr2(res.Pos, net.Pos) > limit)
            {
                continue;
            }

            TakeResource(net, i);
        }
    }

    private void TakeResource(NetShot net, int index)
    {
        if (net != null && net.Home == null && net.Held.Count >= _netCapacity)
        {
            return;
        }

        ResNode res = _resources[index];
        _resources.RemoveAt(index);
        net.Held.Add(res);
        if (res.View != null)
        {
            res.View.SetSortingOrder(31);
        }

        RefreshNetBadges(net);
    }


    private void RefreshNetBadges(NetShot net)
    {
        if (net == null || net.View == null)
        {
            return;
        }

        int wood = 0;
        int leaf = 0;
        int scrap = 0;
        for (int i = 0; i < net.Held.Count; i++)
        {
            ResNode res = net.Held[i];
            if (res == null)
            {
                continue;
            }

            if (res.Kind == BattleResourceKind.Leaf)
            {
                leaf++;
            }
            else if (res.Kind == BattleResourceKind.Meat)
            {
                scrap++;
            }
            else
            {
                wood++;
            }
        }

        net.View.SetHeldCounts(wood, leaf, scrap);
    }
    private void DeliverNet(NetShot net)
    {
        if (net == null || net.Dead)
        {
            return;
        }

        net.Dead = true;
        int gained = 0;
        for (int i = 0; i < net.Held.Count; i++)
        {
            ResNode res = net.Held[i];
            if (res == null || res.Dead)
            {
                continue;
            }

            AddCount(res.Kind, 1);
            gained++;
            res.Dead = true;
            if (res.View != null)
            {
                Destroy(res.View.gameObject);
            }
        }

        net.Held.Clear();
        if (net.View != null)
        {
            Destroy(net.View.gameObject);
        }

        if (net.Home != null)
        {
            if (net.Home.Net == net)
            {
                net.Home.Net = null;
            }
        }
        else if (_playerNet == net)
        {
            _playerNet = null;
        }

        if (gained > 0)
        {
            AddXp(gained * _config.XpPerResource);
        }
    }

    private void CancelNet(NetShot net)
    {
        if (net == null || net.Dead)
        {
            return;
        }

        net.Dead = true;
        for (int i = 0; i < net.Held.Count; i++)
        {
            ResNode res = net.Held[i];
            if (res == null || res.Dead)
            {
                continue;
            }

            if (res.View != null)
            {
                res.View.SetSortingOrder(12);
            }

            _resources.Add(res);
        }

        net.Held.Clear();
        if (net.View != null)
        {
            Destroy(net.View.gameObject);
        }

        if (net.Home != null && net.Home.Net == net)
        {
            net.Home.Net = null;
        }

        if (_playerNet == net)
        {
            _playerNet = null;
        }
    }

    private void SyncNetVisual(NetShot net)
    {
        if (net.View != null)
        {
            net.View.transform.position = net.Pos;
        }

        for (int i = 0; i < net.Held.Count; i++)
        {
            ResNode res = net.Held[i];
            if (res == null || res.View == null)
            {
                continue;
            }

            float ang = i * 1.15f;
            Vector3 offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.28f;
            res.Pos = net.Pos + offset;
            res.View.transform.position = res.Pos;
        }
    }

    private void ThrowPlayerSpear(Vector3 point)
    {
        SpearShot spear = new SpearShot();
        spear.Pos = _characterPos;
        spear.Origin = _characterPos;
        spear.Target = point;
        spear.Damage = _config.SpearDamage;
        spear.Radius = _config.SpearRadius;
        spear.View = Instantiate(spearPrefab, spawnRoot);
        spear.View.transform.position = spear.Pos;
        _spears.Add(spear);
    }

    private void ThrowTowerSpear(PlankNode plank, EnemyNode enemy)
    {
        SpearShot spear = new SpearShot();
        spear.Pos = plank.Logic;
        spear.Origin = plank.Logic;
        spear.Target = enemy.Pos;
        spear.Damage = _config.TowerDamage;
        spear.Radius = 0f;
        spear.Lock = enemy;
        spear.View = Instantiate(spearPrefab, spawnRoot);
        spear.View.SetTint(_config.TowerSpearColor);
        spear.View.transform.position = spear.Pos;
        _spears.Add(spear);
    }

    private void TickSpears(float dt)
    {
        for (int i = _spears.Count - 1; i >= 0; i--)
        {
            TickSpear(_spears[i], dt);
            if (_spears[i].Done)
            {
                _spears.RemoveAt(i);
            }
        }
    }

    private void TickSpear(SpearShot spear, float dt)
    {
        if (spear == null || spear.Done)
        {
            return;
        }

        spear.Pos = Vector3.MoveTowards(spear.Pos, spear.Target, _config.SpearSpeed * dt);
        if (spear.View != null)
        {
            spear.View.transform.position = spear.Pos;
            Vector3 aim = spear.Target - spear.Pos;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = spear.Target - spear.Origin;
            }

            if (aim.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                spear.View.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            }
        }

        if (Sqr2(spear.Pos, spear.Target) > 0.0025f)
        {
            return;
        }

        spear.Done = true;
        if (spear.Lock != null)
        {
            DamageEnemy(spear.Lock, spear.Damage);
        }
        else
        {
            EnemyNode hit = NearestEnemy(spear.Target, spear.Radius);
            if (hit != null)
            {
                DamageEnemy(hit, spear.Damage);
            }
        }

        if (spear.View != null)
        {
            Destroy(spear.View.gameObject);
        }
    }

    private EnemyNode NearestEnemy(Vector3 pos, float range)
    {
        float best = range * range;
        EnemyNode pick = null;
        for (int i = 0; i < _enemies.Count; i++)
        {
            EnemyNode enemy = _enemies[i];
            if (enemy.Dead)
            {
                continue;
            }

            float dist = Sqr2(enemy.Pos, pos);
            if (dist <= best)
            {
                best = dist;
                pick = enemy;
            }
        }

        return pick;
    }

    private ResNode NearestResource(Vector3 pos, float range)
    {
        float best = range * range;
        ResNode pick = null;
        for (int i = 0; i < _resources.Count; i++)
        {
            ResNode res = _resources[i];
            if (res.Dead)
            {
                continue;
            }

            float dist = Sqr2(res.Pos, pos);
            if (dist <= best)
            {
                best = dist;
                pick = res;
            }
        }

        return pick;
    }

    private void AddCount(BattleResourceKind kind, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (kind == BattleResourceKind.Leaf)
        {
            _leaf += amount;
        }
        else if (kind == BattleResourceKind.Meat)
        {
            _scrap += amount;
        }
        else
        {
            _wood += amount;
        }

        TouchHud();
    }

    private void AddXp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        _xp += amount;
        TouchHud();
        TryOpenLevelUp();
    }

    private void TryOpenLevelUp()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        int need = _config.XpToNext(_level);
        if (_xp < need)
        {
            return;
        }

        int dumpX;
        int dumpY;
        bool canPlank = FindExpansionCell(out dumpX, out dumpY);
        bool canBuild = FindEmptyPlank() != null;
        _pendingNeed = need;
        _phase = Phase.LevelUp;
        hud.ShowLevelUp(canPlank, canBuild);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        ArmModal(hud.LevelGroup);
        TouchHud();
    }

    private void CommitLevelUp()
    {
        _xp -= _pendingNeed;
        if (_xp < 0)
        {
            _xp = 0;
        }

        _level++;
        _phase = Phase.Fight;
        hud.HideLevelUp();
        _modalArm = 0;
        _modalGroup = null;
        TouchHud();
        TryOpenLevelUp();
        if (_phase == Phase.Fight)
        {
            TryVictory();
        }
    }

    private void TryVictory()
    {
        if (_phase != Phase.Fight || !_wavesDone || _enemies.Count > 0)
        {
            return;
        }

        Win();
    }

    private void Win()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        _phase = Phase.Victory;
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save != null)
        {
            bool advanced = save.RecordStageCleared(_chapter, _stage);
            Debug.Log("[Battle] RecordStageCleared " + _chapter + "-" + _stage + " -> " + advanced);
        }
        else
        {
            Debug.LogWarning("[Battle] LocalSaveManager missing. Clear was not saved.");
        }

        ShowResult(true);
    }

    private void Lose()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        _phase = Phase.Defeat;
        ShowResult(false);
    }

    private void ShowResult(bool victory)
    {
        int nextChapter;
        int nextStage;
        bool showPrimary = !victory || TryGetNext(out nextChapter, out nextStage);
        hud.ShowResult(victory, showPrimary);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        ArmModal(hud.ResultGroup);
    }

    private void ArmModal(CanvasGroup group)
    {
        _modalGroup = group;
        hud.SetGroupBlocks(group, false);
        _modalArm = 2;
    }

    private bool TryGetNext(out int chapter, out int stage)
    {
        chapter = _chapter;
        stage = _stage + 1;
        if (stage > StageDefs.StagesPerChapter)
        {
            chapter++;
            stage = 1;
        }

        return StageDefs.IsValid(chapter, stage);
    }

    private void GoNext()
    {
        int chapter;
        int stage;
        if (!TryGetNext(out chapter, out stage) || _leaving)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(MainUIController.BattleSceneName))
        {
            Debug.LogError("[Battle] Scene '" + MainUIController.BattleSceneName + "' is not in Build Settings.");
            return;
        }

        StageSelection.Set(chapter, stage);
        _leaving = true;
        SceneManager.LoadScene(MainUIController.BattleSceneName);
    }

    private void LoadSceneSafe(string sceneName)
    {
        if (_leaving)
        {
            return;
        }

        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[Battle] Scene '" + sceneName + "' is not in Build Settings.");
            return;
        }

        _leaving = true;
        SceneManager.LoadScene(sceneName);
    }

    private void TouchHud()
    {
        _hudDirty = true;
    }


    private ViewBounds GetView()
    {
        ViewBounds view = new ViewBounds();
        view.HalfH = _config.CameraSize;
        view.HalfW = _config.CameraSize * (16f / 9f);
        if (_cam == null)
        {
            return view;
        }

        view.Cx = _cam.transform.position.x;
        view.Cy = _cam.transform.position.y;
        view.HalfH = _cam.orthographicSize;
        float aspect = _cam.aspect;
        if (aspect < 0.2f)
        {
            aspect = 16f / 9f;
        }

        view.HalfW = view.HalfH * aspect;
        return view;
    }

    private Vector3 CellCenter(int x, int y)
    {
        return new Vector3((x - 0.5f) * _config.CellSize, (y - 0.5f) * _config.CellSize, 0f);
    }

    private float TouchSqr()
    {
        float r = _config.CellSize * _config.CellTouchFactor;
        return r * r;
    }

    private static float Sqr2(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return dx * dx + dy * dy;
    }
}