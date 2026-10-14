using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 鎴樻枟娴佺▼銆傛寕鍦?BattleRoot 涓娿€傛按闈€佸垵濮嬫湪鏉裤€佽鑹层€丠UD 鍦ㄥ満鏅噷銆傝繍琛屾椂鍙疄渚嬪寲璧勬簮銆佽瀮锜广€佺綉銆佺煕鍜屽悗鍔犵殑鏈ㄦ澘/濉斻€?
/// </summary>
[DisallowMultipleComponent]
public partial class BattleDirector : MonoBehaviour
{
    private enum Phase
    {
        Fight,
        LevelUp,
        Build,
        Victory,
        Defeat
    }

    private enum BuildKind
    {
        None,
        Plank,
        Capture,
        Attack,
        Attractor
    }

    private enum PressKind
    {
        None,
        Ui,
        Launch,
        Reel,
        Spear
    }

    private enum PendingThrow
    {
        None,
        Net,
        Spear
    }

    private class PlankNode
    {
        public PlankView View;
        public int X;
        public int Y;
        public bool Dead;
        public Vector3 Logic;
        public Color BaseColor;
        public float Hp;
        public float MaxHp;
        public BattleBuildingKind Building;
        public BattleTower Tower;
        public NetShot Net;
        public ResNode CaptureTarget;
        public float AttackCd;
        public float CaptureCd;
        public float AttractorCd;
    }

    private class ResNode
    {
        public BattleResource View;
        public BattleResourceKind Kind;
        public Vector3 Pos;
        public Vector3 Velocity;
        public bool Dead;
        public int CollectibleIndex = -1;
        public SpriteRenderer CollectibleRenderer;
    }

    private class EnemyNode
    {
        public BattleEnemy View;
        public Vector3 Pos;
        public float Hp;
        public float MaxHp;
        public float Damage;
        public float MoveSpeed;
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
        public SpriteRenderer Rope;
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
        public BattleDamageKind Kind;
    }

    /// <summary>Enemy bolt (Bulie3); damages target plank on impact.</summary>
    private class EnemyBolt
    {
        public BattleSpear View;
        public Vector3 Pos;
        public Vector3 Origin;
        public Vector3 Target;
        public float Damage;
        public PlankNode TargetPlank;
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
    [Tooltip("Optional. CanvasPanel/Reslut BattleResultPanel; falls back to HUD ResultPanel.")]
    [SerializeField] private BattleResultPanel resultPanel;
    [SerializeField] private Transform water;
    [SerializeField] private Transform character;
    [SerializeField] private Transform spawnRoot;
    [SerializeField] private PlankView[] startingPlanks;
    [SerializeField] private PlankView plankPrefab;
    [SerializeField] private BattleResource woodPrefab;
    [SerializeField] private BattleResource ironPrefab;
    [SerializeField] private BattleResource meatPrefab;
    [SerializeField] private BattleEnemy crabPrefab;
    private BattleEnemy enemy2Prefab;
    private BattleEnemy enemy3Prefab;
    [SerializeField] private BattleNet netPrefab;
    [SerializeField] private BattleSpear spearPrefab;
    [SerializeField] private BattleTower captureTowerPrefab;
    [SerializeField] private BattleTower attackTowerPrefab;
    [SerializeField] private BattleTower attractorTowerPrefab;

    private BattleTuningConfig _config;
    private bool _ready;
    private Phase _phase;
    private int _chapter = 1;
    private int _stage = 1;
    private int _level = 1;
    private int _xp;
    private int _pendingNeed;
    private int _wood;
    private int _iron;
    private float _playerHp;
    private float _playerHpMax;
    private int _plankBuys;
    private int _attackBuys;
    private int _attractorBuys;
    private int _captureBuys;
    private int _healBuys;
    private bool _buildFromShop;
    private int _waveCount = 2;
    private StageRosterConfig _stageRoster;
    private StageBattleDefs.Resolved _roster;
    private float _spearDamageBase;
    private float _playerSpearCd;
    private int _waveIndex;
    private int _spawnedInWave;
    private float _woodDriftTimer;
    private float _ironDriftTimer;
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
    private float _heroDamageBonus;
    private float _netSizeBonus;
    private float _netSpeedMul = 1f;
    private float _captureRangeBonus;
    private int _captureNetCapacity = 4;
    private float _captureSizeBonus;
    private float _captureCooldownMul = 1f;
    private float _attackRangeBonus;
    private float _attackDamageBonus;
    private float _attractorRangeBonus;
    private float _attractorIntervalMul = 1f;
    private int _collectibleIndex = -1;
    private float _collectibleTimer;
    private bool _collectibleSpawned;
    private Sprite _collectibleSprite;
    private readonly BattleUpgradeKind[] _levelOffers = new BattleUpgradeKind[3];
    private readonly List<BattleUpgradeKind> _offerPool = new List<BattleUpgradeKind>(16);
    private BattleCharacter _characterBody;
    private int _charX;
    private int _charY;
    private bool _walking;
    private int _walkX;
    private int _walkY;
    private CanvasGroup _modalGroup;
    private NetShot _playerNet;
    private Sprite _ropeSprite;
    private SpriteRenderer _playerRope;
    private BuildKind _buildKind;
    private BattleSpear _attackBoltPrefab;
    private BattleSpear _attractorBoltPrefab;
    private BattleSpear _enemyBoltPrefab;
    private DamakePopup _damakePrefab;
    private Vector3 _camRestPos;
    private float _shakeTimer;
    private const float ShakeDuration = 0.16f;
    private const float ShakeStrength = 0.08f;
    private PendingThrow _pendingThrow;
    private Vector3 _pendingThrowPoint;
    private SpriteRenderer _buildMask;
    private static Sprite _buildMaskSprite;

    private readonly List<PlankNode> _planks = new List<PlankNode>();
    private readonly List<ResNode> _resources = new List<ResNode>();
    private readonly List<EnemyNode> _enemies = new List<EnemyNode>();
    private readonly List<SpearShot> _spears = new List<SpearShot>();
    private readonly List<EnemyBolt> _enemyBolts = new List<EnemyBolt>();
    private readonly List<RaycastResult> _hits = new List<RaycastResult>();
    private readonly List<Vector2Int> _expandCells = new List<Vector2Int>();
    private readonly List<PlankView> _ghostPlanks = new List<PlankView>();
    private readonly List<PlankNode> _raisedPlanks = new List<PlankNode>();
    /// <summary>杩愯鏃舵墠鍑虹幇鐨勯鍒朵綋锛岀敱 YooAsset 鎸夊湴鍧€鍔犺浇鍚庡杩涙潵銆?/summary>
    public void SetSpawnPrefabs(PlankView plank, BattleResource wood, BattleResource iron, BattleResource meat, BattleEnemy enemy1, BattleEnemy enemy2, BattleEnemy enemy3, BattleNet net, BattleSpear spear, BattleTower capture, BattleTower attack, BattleTower attractor)
    {
        plankPrefab = plank;
        woodPrefab = wood;
        ironPrefab = iron;
        meatPrefab = meat;
        crabPrefab = enemy1;
        enemy2Prefab = enemy2;
        enemy3Prefab = enemy3;
        netPrefab = net;
        spearPrefab = spear;
        captureTowerPrefab = capture;
        attackTowerPrefab = attack;
        attractorTowerPrefab = attractor;
    }

    /// <summary>瑙掕壊缃戠怀锛屽湴鍧€ ZB-SZ1銆?/summary>
    public void SetRopeSprite(Sprite sprite)
    {
        _ropeSprite = sprite;
    }

    /// <summary>鍩虹鏀诲嚮濉?bulie1锛岀浜屼釜鏀诲嚮濉?bulie2銆?/summary>
    public void SetTowerBolts(BattleSpear attackBolt, BattleSpear attractorBolt)
    {
        _attackBoltPrefab = attackBolt;
        _attractorBoltPrefab = attractorBolt;
    }

    /// <summary>Enemy ranged bolt prefab, YooAsset address Bulie3.</summary>
    public void SetEnemyBolt(BattleSpear enemyBolt)
    {
        _enemyBoltPrefab = enemyBolt;
    }

    /// <summary>Damage float prefab, YooAsset address Damake.</summary>
    public void SetDamakePrefab(DamakePopup damake)
    {
        _damakePrefab = damake;
    }

    /// <summary>浜斿鍩虹鍏冲崱锛屽湴鍧€ StageRosterConfig銆?/summary>
    public void SetStageRoster(StageRosterConfig roster)
    {
        _stageRoster = roster;
    }

    private void OnDestroy()
    {
        BattleTime.Reset();
    }

    public void Begin(BattleTuningConfig config)
    {
        if (!BattleTime.IsHeld)
        {
            Time.timeScale = 1f;
        }

        if (_ready || config == null)
        {
            Debug.LogError("[Battle] Begin skipped. Config missing or already started.");
            return;
        }

        if (hud == null || plankPrefab == null || woodPrefab == null || ironPrefab == null || meatPrefab == null || crabPrefab == null || enemy2Prefab == null || enemy3Prefab == null ||
            netPrefab == null || spearPrefab == null || captureTowerPrefab == null || attackTowerPrefab == null ||
            attractorTowerPrefab == null)
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

            _camRestPos = _cam.transform.position;
        }

        _shakeTimer = 0f;

        ResolveStage();
        TutorialBattleHooks.BeginBattle(BattleTuning.StageIndex(_chapter, _stage));
        if (TutorialBattleHooks.Active)
        {
            _waveCount = 0;
            _wavesDone = true;
            _waveTimer = 0f;
            if (_wood < 24) { _wood = 24; }
            if (_iron < 8) { _iron = 8; }
        }

        _characterPos = CellCenter(0, 0);
        if (character != null)
        {
            character.position = _characterPos;
            _characterBody = character.GetComponent<BattleCharacter>();
            if (_characterBody != null)
            {
                _characterBody.SetThrowListener(ReleasePlayerThrow);
            }
        }

        _characterRange = _config.CharacterRange;
        _netCapacity = _config.NetCapacity;
        _captureNetCapacity = _config.CaptureNetCapacity;
        _heroDamageBonus = 0f;
        _spearDamageBase = _config.SpearDamage;
        _netSizeBonus = 0f;
        _netSpeedMul = 1f;
        _captureRangeBonus = 0f;
        _captureSizeBonus = 0f;
        _captureCooldownMul = 1f;
        _attackRangeBonus = 0f;
        _attackDamageBonus = 0f;
        _attractorRangeBonus = 0f;
        _attractorIntervalMul = 1f;
        ApplyMetaStats();
        RefreshCharacterRange();

        if (startingPlanks != null)
        {
            for (int i = 0; i < startingPlanks.Length; i++)
            {
                AdoptPlank(startingPlanks[i]);
            }
        }

        _playerHpMax = ComputePlayerMaxHp();
        _playerHp = _playerHpMax;
        _woodDriftTimer = WoodDriftInterval();
        _ironDriftTimer = IronDriftInterval();
        PrefillDriftResources();
        ScheduleCollectible();
        if (_roster.WaveCount <= 0)
        {
            _wavesDone = true;
            _waveTimer = 0f;
        }
        else
        {
            _waveTimer = Mathf.Max(0f, _roster.waves[0].spawnInterval);
        }
        _ready = true;
        hud.SetWeapon(_weapon);
        TouchHud();
        Debug.Log("[Battle] stage " + _chapter + "-" + _stage + " set " + (_roster.template + 1) + " x" + _roster.statMul.ToString("0.0") + " waves " + _waveCount);
    }

    private void Update()
    {
        if (!_ready)
        {
            return;
        }

        TickModalArm();
        if (_leaving)
        {
            return;
        }

        if (_phase == Phase.Build)
        {
            HandleBuildInput();
            return;
        }

        if (_phase != Phase.Fight)
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
        TickEnemyBolts(dt);
        if (_phase != Phase.Fight) return;
        TickDriftSpawn(dt);
        if (_phase != Phase.Fight) return;
        TickCollectible(dt);
        if (_phase != Phase.Fight) return;
        TickWaves(dt);
        if (_phase != Phase.Fight) return;
        TryVictory();
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        TickCameraShake();
        if (!_hudDirty || hud == null)
        {
            return;
        }

        _hudDirty = false;
        hud.SetCounts(_wood, _iron);
        hud.SetHp(_playerHp, _playerHpMax);
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
        TutorialBattleHooks.Raise(TutorialBattleHooks.EvWeaponSpear);
    }

    /// <summary>HUD 涓夊紶鍗℃Ы鍥炶皟锛?/1/2锛夈€?/summary>
    public void ChooseOfferSlot(int slot)
    {
        if (_phase != Phase.LevelUp || _modalArm > 0)
        {
            return;
        }

        if (slot < 0 || slot >= _levelOffers.Length)
        {
            return;
        }

        ApplyUpgrade(_levelOffers[slot]);
        TutorialBattleHooks.Raise(TutorialBattleHooks.EvUpgradePicked);
    }

    public void ChoosePlank() { ChooseOfferSlot(0); }
    public void ChooseCapture() { ChooseOfferSlot(1); }
    public void ChooseAttack() { ChooseOfferSlot(2); }
    public void ChooseRange() { ChooseOfferSlot(0); }
    public void ChooseCapacity() { ChooseOfferSlot(1); }

    public int NextPlankWoodCost => BattleTuning.ShopPlankWood + _plankBuys * BattleTuning.ShopPlankWoodStep;
    public int NextAttackIronCost => BattleTuning.ShopAttackIron + _attackBuys * BattleTuning.ShopAttackIronStep;
    public int NextAttractorIronCost => BattleTuning.ShopAttractorIron + _attractorBuys * BattleTuning.ShopAttractorIronStep;
    public int NextCaptureWoodCost => BattleTuning.ShopCaptureWood + _captureBuys * BattleTuning.ShopCaptureWoodStep;
    public int NextHealWoodCost => BattleTuning.ShopHealWood + _healBuys * BattleTuning.ShopHealWoodStep;

    public bool CanBuyPlank()
    {
        return _phase == Phase.Fight && _wood >= NextPlankWoodCost && HasPlankSpace();
    }

    public bool CanBuyAttack()
    {
        return _phase == Phase.Fight && _iron >= NextAttackIronCost && FindEmptyPlank() != null;
    }

    public bool CanBuyAttractor()
    {
        return _phase == Phase.Fight && _iron >= NextAttractorIronCost && FindEmptyPlank() != null;
    }

    public bool CanBuyCapture()
    {
        return _phase == Phase.Fight && _wood >= NextCaptureWoodCost && FindEmptyPlank() != null;
    }

    public bool CanBuyHeal()
    {
        return _phase == Phase.Fight && _playerHp < _playerHpMax - 0.01f && _wood >= NextHealWoodCost;
    }

    public bool TryBuyPlank()
    {
        if (!CanBuyPlank())
        {
            return false;
        }

        _wood -= NextPlankWoodCost;
        _plankBuys++;
        TouchHud();
        _buildFromShop = true;
        EnterBuildMode(BuildKind.Plank);
        return true;
    }

    public bool TryBuyAttack()
    {
        if (!CanBuyAttack())
        {
            return false;
        }

        _iron -= NextAttackIronCost;
        _attackBuys++;
        TouchHud();
        _buildFromShop = true;
        EnterBuildMode(BuildKind.Attack);
        return true;
    }

    public bool TryBuyAttractor()
    {
        if (!CanBuyAttractor())
        {
            return false;
        }

        _iron -= NextAttractorIronCost;
        _attractorBuys++;
        TouchHud();
        _buildFromShop = true;
        EnterBuildMode(BuildKind.Attractor);
        return true;
    }

    public bool TryBuyCapture()
    {
        if (!CanBuyCapture())
        {
            return false;
        }

        _wood -= NextCaptureWoodCost;
        _captureBuys++;
        TouchHud();
        _buildFromShop = true;
        EnterBuildMode(BuildKind.Capture);
        return true;
    }

    public bool TryBuyHeal()
    {
        if (!CanBuyHeal())
        {
            return false;
        }

        _wood -= NextHealWoodCost;
        _healBuys++;
        _playerHp += BattleTuning.ShopHealAmount;
        if (_playerHp > _playerHpMax)
        {
            _playerHp = _playerHpMax;
        }

        SyncPlankHpFromPlayer();
        RefreshAllPlankHealth();
        TouchHud();
        return true;
    }

    private bool HasPlankSpace()
    {
        CollectExpansionCells(_expandCells);
        return _expandCells.Count > 0;
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
        // Ensure battle tutorial completes even if Home onClick loads Main before TutorialManager.Advance.
        if (TutorialBattleHooks.Active && LocalSaveManager.Instance != null
            && !LocalSaveManager.Instance.IsTutorialDone(TutorialSession.BattleKey))
        {
            LocalSaveManager.Instance.CompleteTutorial(TutorialSession.BattleKey);
            Debug.Log("[Battle] CompleteTutorial(battle) on return home");
        }

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
            if (hud != null)
            {
                hud.SetGroupBlocks(_modalGroup, true);
            }
            else if (_modalGroup != null)
            {
                _modalGroup.blocksRaycasts = true;
                _modalGroup.interactable = true;
            }
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
        _roster = StageBattleDefs.Resolve(_stageRoster, _chapter, _stage);
        _waveCount = _roster.WaveCount;
        _wood = _roster.startWood;
        _iron = _roster.startIron;
    }

    /// <summary>寮€灞€闀跨煕浼ゅ銆佸皠绋嬨€佹敹缃戦€熷害鍚冭鑹茬瓑绾у拰宸茬┛瑁呭銆傚眬鍐呭崌绾у崱涔嬪悗鍐嶅姞銆?/summary>
    private void ApplyMetaStats()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int level = save != null ? save.CharacterLevel : CharacterUpgradeDefs.MinLevel;
        _spearDamageBase = CharacterUpgradeDefs.AttackAtLevel(level);
        if (save == null)
        {
            return;
        }

        string spearId = save.GetEquippedItemId(EquipItemType.Spear);
        _spearDamageBase += EquipSpecialEffects.GetAttackBonus(spearId);

        string hoodId = save.GetEquippedItemId(EquipItemType.Hood);
        _characterRange += Mathf.Max(0f, EquipSpecialEffects.GetStatBonusValue(hoodId));

        string shoesId = save.GetEquippedItemId(EquipItemType.Shoes);
        _netSpeedMul += Mathf.Max(0f, EquipSpecialEffects.GetStatBonusValue(shoesId));
    }

    private void HandleInput(float dt)
    {
        if (_playerSpearCd > 0f)
        {
            _playerSpearCd = Mathf.Max(0f, _playerSpearCd - dt);
        }

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
                // Plank click always walks (net and spear), same as net-mode plank move.
                if (step != null)
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
                    Vector3 land = point;
                    if (!InsideCharacterRange(point))
                    {
                        Vector3 offset = point - _characterPos;
                        offset.z = 0f;
                        land = _characterPos + offset.normalized * _characterRange;
                    }

                    LaunchPlayerNet(land);
                    TutorialBattleHooks.Raise(TutorialBattleHooks.EvNetLaunched);
                    _press = PressKind.Launch;
                }
                else if (_playerSpearCd <= 0f)
                {
                    Vector3 land = point;
                    if (!InsideCharacterRange(point))
                    {
                        Vector3 offset = point - _characterPos;
                        offset.z = 0f;
                        land = _characterPos + offset.normalized * _characterRange;
                    }

                    ThrowPlayerSpear(land);
                    _playerSpearCd = BattleTuning.PlayerSpearInterval;
                    _press = PressKind.Spear;
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
                DestroyRes(res);

                _resources.RemoveAt(i);
                continue;
            }

            PlaceRes(res);
        }
    }

    private void TickDriftSpawn(float dt)
    {
        _woodDriftTimer -= dt;
        if (_woodDriftTimer <= 0f)
        {
            _woodDriftTimer = WoodDriftInterval();
            SpawnDriftKind(BattleResourceKind.Wood);
        }

        _ironDriftTimer -= dt;
        if (_ironDriftTimer <= 0f)
        {
            _ironDriftTimer = IronDriftInterval();
            SpawnDriftKind(BattleResourceKind.Iron);
        }
    }

    /// <summary>鏈ㄥご/閾佸悇鑷粷瀵瑰埛鏂伴棿闅旓紝涓嶅彈 DriftCountMul 褰卞搷銆?/summary>
    private float WoodDriftInterval()
    {
        return _config.WoodDriftInterval;
    }

    private float IronDriftInterval()
    {
        return _config.IronDriftInterval;
    }

    /// <summary>鍗囩骇鐖嗗彂鏁伴噺鍊嶇巼锛涢濉笌瀹氭椂鍒锋柊鐢ㄧ粷瀵瑰€笺€?/summary>
    private static int ScaledDriftCount(int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Mathf.Max(1, Mathf.RoundToInt(count * BattleTuning.DriftCountMul));
    }

    private void PrefillDriftResources()
    {
        // 寮€灞€棰勫～缁濆鍊硷紝涓嶄箻 DriftCountMul
        SpawnDriftBurst(_config.DriftPrefillCount);
    }

    /// <summary>鍦ㄦ紓娴佽蛋寤婁笂鍒蜂竴鎵规湪/閾侊紙寮€鎴橀濉€佸崌绾х垎鍙戝叡鐢級銆?/summary>
    private void SpawnDriftBurst(int count)
    {
        if (count <= 0)
        {
            return;
        }

        ViewBounds view = GetView();
        float travel = (view.HalfW + view.HalfH) * 1.55f;
        for (int i = 0; i < count; i++)
        {
            Vector3 origin = PickDriftSpawnOrigin(view);
            Vector3 dir = PickDriftDirection();
            float u = (i + Random.Range(0.2f, 0.8f)) / count;
            float t = Mathf.Lerp(0.06f, 0.88f, u);
            BattleResourceKind kind = Random.value < 0.5f ? BattleResourceKind.Wood : BattleResourceKind.Iron;
            SpawnDriftOne(kind, origin + dir * (travel * t), dir * _config.DriftSpeed);
        }
    }

    private void SpawnDriftKind(BattleResourceKind kind)
    {
        ViewBounds view = GetView();
        Vector3 origin = PickDriftSpawnOrigin(view);
        Vector3 dir = PickDriftDirection();
        SpawnDriftOne(kind, origin, dir * _config.DriftSpeed);
    }

    private static Vector3 PickDriftSpawnOrigin(ViewBounds view)
    {
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

        return new Vector3(x, y, 0f);
    }

    private static Vector3 PickDriftDirection()
    {
        Vector3 dir = new Vector3(-1f, -1f, 0f).normalized;
        float jitter = Random.Range(-0.22f, 0.22f);
        return new Vector3(dir.x + jitter, dir.y, 0f).normalized;
    }

    private BattleResource PrefabFor(BattleResourceKind kind)
    {
        if (kind == BattleResourceKind.Iron)
        {
            return ironPrefab;
        }

        if (kind == BattleResourceKind.Meat)
        {
            return meatPrefab;
        }

        return woodPrefab;
    }

    private void SpawnDriftOne(BattleResourceKind kind, Vector3 pos, Vector3 velocity)
    {
        ResNode res = new ResNode();
        res.Kind = kind;
        res.Pos = pos;
        res.Velocity = velocity;
        res.View = Instantiate(PrefabFor(kind), spawnRoot);
        res.View.SetKind(kind);
        res.View.SetSortingOrder(1);
        res.View.transform.position = res.Pos;
        res.View.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
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
        res.Velocity = dir * (_config.DriftSpeed / 6f);
        res.View = Instantiate(meatPrefab, spawnRoot);
        res.View.SetKind(res.Kind);
        res.View.SetSortingOrder(1);
        res.View.transform.position = res.Pos;
        res.View.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _resources.Add(res);
    }

    /// <summary>浼樺厛婕傛湭鏀堕泦鐨勮棌鍝侊紱閮芥湁浜嗗氨闅忔満涓€浠躲€?/summary>
    private int PickCollectibleIndex()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int unowned = 0;
        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            if (save == null || !save.IsCollectibleOwned(i))
            {
                unowned++;
            }
        }

        if (unowned <= 0)
        {
            return Random.Range(0, CollectionDefs.Count);
        }

        int pick = Random.Range(0, unowned);
        for (int i = 0; i < CollectionDefs.Count; i++)
        {
            if (save != null && save.IsCollectibleOwned(i))
            {
                continue;
            }

            if (pick == 0)
            {
                return i;
            }

            pick--;
        }

        return 0;
    }

    private void ScheduleCollectible()
    {
        _collectibleIndex = PickCollectibleIndex();
        _collectibleSpawned = false;
        _collectibleSprite = null;
        float span = EstimateFightSeconds();
        float latest = Mathf.Min(22f, Mathf.Max(12f, span * 0.35f));
        _collectibleTimer = Random.Range(6f, latest);
        StartCoroutine(LoadCollectibleSprite());
    }

    private float EstimateFightSeconds()
    {
        if (_roster == null || _roster.WaveCount <= 0)
        {
            return 12f;
        }

        float time = Mathf.Max(0f, _roster.waves[0].spawnInterval);
        for (int wave = 0; wave < _roster.WaveCount; wave++)
        {
            StageBattleDefs.ResolvedWave entry = _roster.waves[wave];
            int crabs = entry == null || entry.slots == null ? 0 : entry.slots.Length;
            if (crabs > 1)
            {
                time += (crabs - 1) * Mathf.Max(0f, entry.spawnInterval);
            }

            if (wave < _roster.WaveCount - 1)
            {
                time += _roster.waveGap;
            }
        }

        return time;
    }

    private IEnumerator LoadCollectibleSprite()
    {
        int index = _collectibleIndex;
        YooAssetManager yoo = YooAssetManager.Instance;
        if (yoo == null || index < 0)
        {
            yield break;
        }

        if (!yoo.IsReady)
        {
            yield return yoo.InitializeAsync();
        }

        if (!yoo.IsReady)
        {
            Debug.LogError("[Battle] YooAsset is not ready. Collectible sprite was not loaded.");
            yield break;
        }

        Sprite sprite = null;
        yield return yoo.LoadSpriteAsync(CollectionDefs.OwnedAddress(index), loaded => sprite = loaded);
        if (index != _collectibleIndex)
        {
            yield break;
        }

        _collectibleSprite = sprite;
        if (sprite == null)
        {
            Debug.LogError("[Battle] Failed to load collectible " + CollectionDefs.OwnedAddress(index) + ".");
        }
    }

    private void TickCollectible(float dt)
    {
        if (_collectibleSpawned || _collectibleIndex < 0)
        {
            return;
        }

        _collectibleTimer -= dt;
        if (_collectibleTimer > 0f || _collectibleSprite == null)
        {
            return;
        }

        SpawnCollectible();
    }

    private void SpawnCollectible()
    {
        if (_collectibleSpawned || _collectibleSprite == null)
        {
            return;
        }

        _collectibleSpawned = true;
        ViewBounds view = GetView();
        Vector3 origin = PickDriftSpawnOrigin(view);
        Vector3 dir = PickDriftDirection();
        ResNode res = new ResNode();
        res.CollectibleIndex = _collectibleIndex;
        res.Kind = BattleResourceKind.Wood;
        res.Pos = origin;
        res.Velocity = dir * _config.DriftSpeed;
        GameObject go = new GameObject(CollectionDefs.OwnedAddress(_collectibleIndex));
        go.transform.SetParent(spawnRoot, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = _collectibleSprite;
        renderer.sortingOrder = 12;
        float longest = Mathf.Max(_collectibleSprite.bounds.size.x, _collectibleSprite.bounds.size.y);
        float scale = longest > 0.01f ? 2.7f / longest : 1f;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position = res.Pos;
        res.CollectibleRenderer = renderer;
        _resources.Add(res);
    }

    private static void PlaceRes(ResNode res)
    {
        if (res.View != null)
        {
            res.View.transform.position = res.Pos;
        }
        else if (res.CollectibleRenderer != null)
        {
            res.CollectibleRenderer.transform.position = res.Pos;
        }
    }

    private void DestroyRes(ResNode res)
    {
        if (res.View != null)
        {
            Destroy(res.View.gameObject);
            res.View = null;
        }

        if (res.CollectibleRenderer != null)
        {
            Destroy(res.CollectibleRenderer.gameObject);
            res.CollectibleRenderer = null;
        }
    }

    private static void SetResOrder(ResNode res, int order)
    {
        if (res.View != null)
        {
            res.View.SetSortingOrder(order);
        }
        else if (res.CollectibleRenderer != null)
        {
            res.CollectibleRenderer.sortingOrder = order;
        }
    }

    private void GrantCollectible(int index)
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        if (save == null)
        {
            Debug.LogWarning("[Battle] LocalSaveManager missing. Collectible was not saved.");
            return;
        }

        save.SetCollectibleOwned(index, true);
    }

    private void TickWaves(float dt)
    {
        if (TutorialBattleHooks.Scripted)
        {
            return;
        }
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
            _collectibleTimer = 0f;
            return;
        }

        StageBattleDefs.ResolvedWave wave = _roster.waves[_waveIndex];
        int need = wave == null || wave.slots == null ? 0 : wave.slots.Length;
        if (need <= 0 || _spawnedInWave >= need)
        {
            AdvanceWave();
            return;
        }

        SpawnEnemy(wave.slots[_spawnedInWave]);
        _spawnedInWave++;
        if (_spawnedInWave >= need)
        {
            AdvanceWave();
        }
        else
        {
            _waveTimer = Mathf.Max(0f, wave.spawnInterval);
        }
    }

    private void AdvanceWave()
    {
        _waveIndex++;
        _spawnedInWave = 0;
        if (_waveIndex >= _waveCount)
        {
            _wavesDone = true;
            _collectibleTimer = 0f;
        }
        else
        {
            _waveTimer = _roster.waveGap;
        }
    }

    private void SpawnEnemy(StageBattleDefs.SpawnSlot slot)
    {
        ViewBounds view = GetView();
        Vector3 pos = PickEnemySpawnOrigin(view);
        if (PlankUnder(pos) != null || Sqr2(pos, _characterPos) <= TouchSqr())
        {
            Vector3 toward = new Vector3(view.Cx - pos.x, view.Cy - pos.y, 0f);
            if (toward.sqrMagnitude < 0.0001f)
            {
                toward = Vector3.left;
            }

            pos += toward.normalized * 3f;
        }

        BattleEnemy prefab = PrefabForKind(slot.kind);
        if (prefab == null)
        {
            return;
        }

        EnemyNode enemy = new EnemyNode();
        enemy.Pos = pos;
        enemy.Hp = slot.stat.hp;
        enemy.MaxHp = slot.stat.hp;
        enemy.Damage = slot.stat.damage;
        enemy.MoveSpeed = slot.stat.speed;
        enemy.AttackTimer = _config.EnemyAttackInterval;
        enemy.View = Instantiate(prefab, spawnRoot);
        enemy.View.SetBodyScale(0.62f);
        enemy.View.transform.position = pos;
        enemy.View.SetHealth01(1f);
        Vector3 spawnFace = _characterPos - pos;
        if (spawnFace.sqrMagnitude < 0.0001f)
        {
            spawnFace = new Vector3(view.Cx - pos.x, view.Cy - pos.y, 0f);
        }

        enemy.View.FaceToward(spawnFace);
        _enemies.Add(enemy);
    }

    /// <summary>Random off-screen spawn on left / right / top / bottom.</summary>
    private static Vector3 PickEnemySpawnOrigin(ViewBounds view)
    {
        const float margin = 1.8f;
        int side = Random.Range(0, 4);
        float x;
        float y;
        switch (side)
        {
            case 0: // right
                x = view.Cx + view.HalfW + margin;
                y = Random.Range(view.Cy - view.HalfH * 0.85f, view.Cy + view.HalfH * 0.95f);
                break;
            case 1: // left
                x = view.Cx - view.HalfW - margin;
                y = Random.Range(view.Cy - view.HalfH * 0.85f, view.Cy + view.HalfH * 0.95f);
                break;
            case 2: // top
                x = Random.Range(view.Cx - view.HalfW * 0.95f, view.Cx + view.HalfW * 0.95f);
                y = view.Cy + view.HalfH + margin;
                break;
            default: // bottom
                x = Random.Range(view.Cx - view.HalfW * 0.95f, view.Cx + view.HalfW * 0.95f);
                y = view.Cy - view.HalfH - margin;
                break;
        }

        return new Vector3(x, y, 0f);
    }

    private BattleEnemy PrefabForKind(int kind)
    {
        if (kind == 2 && enemy3Prefab != null)
        {
            return enemy3Prefab;
        }

        if (kind == 1 && enemy2Prefab != null)
        {
            return enemy2Prefab;
        }

        return crabPrefab;
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

        bool ranged = enemy.View != null && enemy.View.IsRanged;
        if (ranged)
        {
            TickRangedEnemy(enemy, dt);
            return;
        }

        PlankNode plank = NearestPlank(enemy.Pos);
        if (plank == null)
        {
            enemy.AttackTimer = _config.EnemyAttackInterval;
            if (ReachedCharacter(enemy))
            {
                Lose();
                return;
            }

            Vector3 toChar = _characterPos - enemy.Pos;
            enemy.Pos = Vector3.MoveTowards(enemy.Pos, _characterPos, enemy.MoveSpeed * dt);
            if (ReachedCharacter(enemy))
            {
                Lose();
                return;
            }

            PlaceEnemy(enemy, toChar);
            return;
        }

        float reach = CrabRadius(enemy) + PlankRadius(plank);
        Vector3 toPlank = plank.Logic - enemy.Pos;
        if (Sqr2(plank.Logic, enemy.Pos) <= reach * reach)
        {
            enemy.AttackTimer -= dt;
            if (enemy.AttackTimer <= 0f)
            {
                enemy.AttackTimer += _config.EnemyAttackInterval;
                if (enemy.View != null)
                {
                    enemy.View.PlayMeleeImpact(toPlank);
                }

                DamagePlank(plank, enemy.Damage);
            }

            PlaceEnemy(enemy, toPlank);
            return;
        }

        enemy.AttackTimer = _config.EnemyAttackInterval;
        enemy.Pos = Vector3.MoveTowards(enemy.Pos, plank.Logic, enemy.MoveSpeed * dt);
        PlaceEnemy(enemy, toPlank);
    }

    /// <summary>Ranged: move to nearest plank, stop nearby, fire Bulie3 at that plank.</summary>
    private void TickRangedEnemy(EnemyNode enemy, float dt)
    {
        PlankNode plank = NearestPlank(enemy.Pos);
        if (plank == null)
        {
            enemy.AttackTimer = _config.EnemyAttackInterval;
            PlaceEnemy(enemy, _characterPos - enemy.Pos);
            return;
        }

        float stop = Mathf.Max(2.5f, _config.CharacterRange);
        float dist = Vector3.Distance(enemy.Pos, plank.Logic);
        Vector3 toPlank = plank.Logic - enemy.Pos;
        if (dist > stop)
        {
            enemy.AttackTimer = _config.EnemyAttackInterval;
            enemy.Pos = Vector3.MoveTowards(enemy.Pos, plank.Logic, enemy.MoveSpeed * dt);
            PlaceEnemy(enemy, toPlank);
            return;
        }

        enemy.AttackTimer -= dt;
        if (enemy.AttackTimer <= 0f)
        {
            enemy.AttackTimer += _config.EnemyAttackInterval;
            FireEnemyBolt(enemy, plank);
        }

        PlaceEnemy(enemy, toPlank);
    }

    private void PlaceEnemy(EnemyNode enemy, Vector3 faceDir)
    {
        if (enemy.View == null)
        {
            return;
        }

        enemy.View.transform.position = enemy.Pos;
        float ratio = enemy.MaxHp <= 0f ? 0f : Mathf.Clamp01(enemy.Hp / enemy.MaxHp);
        enemy.View.SetHealth01(ratio);
        if (faceDir.sqrMagnitude > 0.0001f)
        {
            enemy.View.FaceToward(faceDir);
        }
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

    private void DamageEnemy(EnemyNode enemy, float damage, BattleDamageKind kind)
    {
        if (enemy == null || enemy.Dead)
        {
            return;
        }

        if (damage < 0f)
        {
            damage = 0f;
        }

        Vector3 floatPos = enemy.Pos;
        if (enemy.View != null)
        {
            floatPos = enemy.View.transform.position;
        }

        enemy.Hp -= damage;
        SpawnDamake(floatPos, damage, kind);
        if (enemy.Hp <= 0f)
        {
            KillEnemy(enemy);
            return;
        }

        PlaceEnemy(enemy, Vector3.zero);
    }

    private void SpawnDamake(Vector3 worldPos, float damage, BattleDamageKind kind)
    {
        if (_damakePrefab == null || damage <= 0f)
        {
            return;
        }

        Transform parent = spawnRoot != null ? spawnRoot : transform;
        DamakePopup popup = Instantiate(_damakePrefab, parent);
        popup.transform.position = worldPos + new Vector3(Random.Range(-0.15f, 0.15f), 0.55f, 0f);
        popup.Play(damage, ColorForDamage(kind));
    }

    private static Color ColorForDamage(BattleDamageKind kind)
    {
        switch (kind)
        {
            case BattleDamageKind.AttackTower:
                return new Color(1f, 0.45f, 0.2f, 1f);
            case BattleDamageKind.Attractor:
                return new Color(0.55f, 0.35f, 1f, 1f);
            case BattleDamageKind.Player:
                // Distinct from enemy floats: soft red/white.
                return new Color(1f, 0.45f, 0.45f, 1f);
            case BattleDamageKind.Plank:
                return new Color(0.85f, 0.55f, 0.25f, 1f);
            case BattleDamageKind.Spear:
            default:
                return new Color(1f, 0.92f, 0.25f, 1f);
        }
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
        TutorialBattleHooks.OnEnemyKilled();
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
        node.Logic = CellCenter(node.X, node.Y);
        // Planks share player HP; MaxHp/Hp mirror player for display only (not a lose condition).
        node.MaxHp = _playerHpMax > 0f ? _playerHpMax : (_config != null ? _config.PlankHp : BattleTuning.PlankHp);
        node.Hp = _playerHpMax > 0f ? _playerHp : node.MaxHp;
        view.transform.position = node.Logic;
        float adoptRatio = node.MaxHp <= 0f ? 1f : Mathf.Clamp01(node.Hp / node.MaxHp);
        view.SetHealth01(adoptRatio);
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

        BattleTower prefab = captureTowerPrefab;
        float radius = _config.CaptureRange + _captureRangeBonus;
        if (kind == BattleBuildingKind.Attack)
        {
            prefab = attackTowerPrefab;
            radius = _config.TowerRange + _attackRangeBonus;
        }
        else if (kind == BattleBuildingKind.Attractor)
        {
            prefab = attractorTowerPrefab;
            radius = _config.AttractorRange + _attractorRangeBonus;
            plank.AttractorCd = 0f;
        }

        Transform parent = plank.View != null && plank.View.TowerAnchor != null ? plank.View.TowerAnchor : plank.View.transform;
        BattleTower tower = Instantiate(prefab, parent);
        tower.transform.localPosition = Vector3.zero;
        tower.SetRangeRadius(radius);
        tower.ShowRangeBriefly();
        plank.Tower = tower;
    }

    private void DamagePlayer(float damage)
    {
        Vector3 floatPos = _characterPos;
        if (character != null)
        {
            floatPos = character.position;
        }

        ApplySharedPlayerDamage(damage, floatPos, BattleDamageKind.Player);
    }

    /// <summary>
    /// Enemy hits a plank: shared player HP (not per-plank), float at the hit plank, shake every plank.
    /// Lose only when player HP hits 0 (same path as DamagePlayer).
    /// </summary>
    private void DamagePlank(PlankNode plank, float damage)
    {
        if (plank == null || plank.Dead || damage <= 0f || _phase != Phase.Fight)
        {
            return;
        }

        Vector3 floatPos = plank.Logic;
        if (plank.View != null)
        {
            floatPos = plank.View.transform.position;
        }

        ShakeAllPlanks();
        ApplySharedPlayerDamage(damage, floatPos, BattleDamageKind.Plank);
    }

    private void ApplySharedPlayerDamage(float damage, Vector3 floatPos, BattleDamageKind kind)
    {
        if (_phase != Phase.Fight || damage <= 0f || _playerHp <= 0f)
        {
            return;
        }

        _playerHp -= damage;
        if (_playerHp < 0f)
        {
            _playerHp = 0f;
        }

        // Keep unused per-plank Hp mirrors in sync with shared player HP (display / legacy).
        SyncPlankHpFromPlayer();

        SpawnDamake(floatPos, damage, kind);
        BeginCameraShake();
        if (hud != null)
        {
            hud.PlayHurtFlash();
        }

        RefreshAllPlankHealth();
        TouchHud();
        if (_playerHp <= 0f)
        {
            Lose();
        }
    }

    private void ShakeAllPlanks()
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead || plank.View == null)
            {
                continue;
            }

            plank.View.PlayHitShake();
        }
    }

    private void SyncPlankHpFromPlayer()
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead)
            {
                continue;
            }

            plank.MaxHp = _playerHpMax;
            plank.Hp = _playerHp;
        }
    }

    private void RefreshAllPlankHealth()
    {
        float ratio = _playerHpMax <= 0f ? 0f : Mathf.Clamp01(_playerHp / _playerHpMax);
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead || plank.View == null)
            {
                continue;
            }

            plank.View.transform.position = plank.Logic;
            plank.View.SetHealth01(ratio);
        }
    }

    private void RemovePlank(PlankNode plank)
    {
        if (plank == null || plank.Dead)
        {
            return;
        }

        // Do not Lose() when a plank under the character is removed - shared player HP owns defeat.
        plank.Dead = true;
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
        float ratio = _playerHpMax <= 0f ? 0f : Mathf.Clamp01(_playerHp / _playerHpMax);
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
        CollectExpansionCells(_expandCells);
        if (_expandCells.Count == 0)
        {
            x = 0;
            y = 0;
            return false;
        }

        x = _expandCells[0].x;
        y = _expandCells[0].y;
        return true;
    }

    private void CollectExpansionCells(List<Vector2Int> cells)
    {
        cells.Clear();
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
        var seen = new HashSet<Vector2Int>();
        for (int a = 0; a < anchors.Count; a++)
        {
            for (int d = 0; d < 4; d++)
            {
                Vector2Int next = new Vector2Int(anchors[a].x + dx[d], anchors[a].y + dy[d]);
                if (!occupied.Contains(next) && seen.Add(next))
                {
                    cells.Add(next);
                }
            }
        }
    }

    private void EnterBuildMode(BuildKind kind)
    {
        _buildKind = kind;
        _phase = Phase.Build;
        _modalArm = 0;
        _modalGroup = null;
        _press = PressKind.None;
        hud.HideLevelUp();
        ClearBuildVisuals();
        EnsureBuildMask();
        hud.SetBattleInputEnabled(false);

        if (kind == BuildKind.Plank)
        {
            ShowPlankGhosts();
        }
        else
        {
            ShowTowerBuildTargets();
        }
    }

    private void HandleBuildInput()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Vector3 point = MouseWorld();
        if (_buildKind == BuildKind.Plank)
        {
            Vector2Int cell;
            if (!TryGhostCellUnder(point, out cell))
            {
                return;
            }

            SpawnPlank(cell.x, cell.y);
            TutorialBattleHooks.Raise(TutorialBattleHooks.EvPlankBuilt);
            FinishBuildMode();
            return;
        }

        if (_buildKind == BuildKind.Capture || _buildKind == BuildKind.Attack || _buildKind == BuildKind.Attractor)
        {
            PlankNode plank = EmptyPlankUnder(point);
            if (plank == null)
            {
                return;
            }

            BattleBuildingKind towerKind = BattleBuildingKind.Capture;
            if (_buildKind == BuildKind.Attack)
            {
                towerKind = BattleBuildingKind.Attack;
            }
            else if (_buildKind == BuildKind.Attractor)
            {
                towerKind = BattleBuildingKind.Attractor;
            }

            BuildTower(plank, towerKind);
            if (towerKind == BattleBuildingKind.Capture)
            {
                TutorialBattleHooks.Raise(TutorialBattleHooks.EvCaptureBuilt);
            }
            else if (towerKind == BattleBuildingKind.Attack)
            {
                TutorialBattleHooks.Raise(TutorialBattleHooks.EvAttackBuilt);
            }
            FinishBuildMode();
        }
    }

    private void FinishBuildMode()
    {
        ClearBuildVisuals();
        if (hud != null)
        {
            hud.SetBattleInputEnabled(true);
        }

        _buildKind = BuildKind.None;
        if (_buildFromShop)
        {
            _buildFromShop = false;
            _phase = Phase.Fight;
            TouchHud();
            TryOpenLevelUp();
            return;
        }

        CommitLevelUp();
    }

    private void ClearBuildVisuals()
    {
        for (int i = 0; i < _ghostPlanks.Count; i++)
        {
            if (_ghostPlanks[i] != null)
            {
                _ghostPlanks[i].StopGhostBreathe();
                Destroy(_ghostPlanks[i].gameObject);
            }
        }

        _ghostPlanks.Clear();

        for (int i = 0; i < _raisedPlanks.Count; i++)
        {
            PlankNode plank = _raisedPlanks[i];
            if (plank != null && plank.View != null)
            {
                plank.View.StopGhostBreathe();
                plank.View.RestoreSorting();
            }
        }

        _raisedPlanks.Clear();

        if (_buildMask != null)
        {
            Destroy(_buildMask.gameObject);
            _buildMask = null;
        }
    }

    private void ShowPlankGhosts()
    {
        CollectExpansionCells(_expandCells);
        for (int i = 0; i < _expandCells.Count; i++)
        {
            Vector2Int cell = _expandCells[i];
            PlankView ghost = Instantiate(plankPrefab, spawnRoot);
            ghost.SetCell(cell.x, cell.y);
            ghost.transform.position = CellCenter(cell.x, cell.y);
            ghost.ApplyGhostLook();
            _ghostPlanks.Add(ghost);
        }
    }

    private bool TryGhostCellUnder(Vector3 point, out Vector2Int cell)
    {
        cell = default(Vector2Int);
        float limit = TouchSqr();
        for (int i = 0; i < _ghostPlanks.Count; i++)
        {
            PlankView ghost = _ghostPlanks[i];
            if (ghost == null)
            {
                continue;
            }

            Vector3 logic = CellCenter(ghost.CellX, ghost.CellY);
            if (Sqr2(logic, point) <= limit)
            {
                cell = new Vector2Int(ghost.CellX, ghost.CellY);
                return true;
            }
        }

        return false;
    }

    private void ShowTowerBuildTargets()
    {
        // 绌烘湪鏉挎姮楂?sorting锛屽帇鍦ㄩ伄缃╀笂闈紝浠嶇劧鍙互鐐归€?
        _raisedPlanks.Clear();
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead || plank.Building != BattleBuildingKind.None || plank.View == null)
            {
                continue;
            }

            plank.View.BoostSorting(55);
            plank.View.StartTargetBreathe();
            _raisedPlanks.Add(plank);
        }
    }

    private PlankNode EmptyPlankUnder(Vector3 pos)
    {
        float limit = TouchSqr();
        for (int i = 0; i < _raisedPlanks.Count; i++)
        {
            PlankNode plank = _raisedPlanks[i];
            if (plank == null || plank.Dead || plank.Building != BattleBuildingKind.None)
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

    private void EnsureBuildMask()
    {
        if (_buildMask != null)
        {
            return;
        }

        ViewBounds view = GetView();
        GameObject go = new GameObject("BuildMask");
        go.transform.SetParent(spawnRoot != null ? spawnRoot : transform, false);
        go.transform.position = new Vector3(view.Cx, view.Cy, 0f);
        float w = Mathf.Max(2f, view.HalfW * 2.6f);
        float h = Mathf.Max(2f, view.HalfH * 2.6f);
        go.transform.localScale = new Vector3(w, h, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBuildMaskSprite();
        sr.color = new Color(0f, 0f, 0f, 200f / 255f);
        sr.sortingOrder = 40;
        _buildMask = sr;
    }

    private static Sprite GetBuildMaskSprite()
    {
        if (_buildMaskSprite != null)
        {
            return _buildMaskSprite;
        }

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        _buildMaskSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        _buildMaskSprite.hideFlags = HideFlags.HideAndDontSave;
        return _buildMaskSprite;
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

    private void TickCaptureTower(PlankNode plank, float dt)
    {
        if (plank.Net != null || plank.Tower == null)
        {
            return;
        }

        float range = _config.CaptureRange + _captureRangeBonus;
        ResNode res = plank.CaptureTarget;
        if (!IsLiveResource(res) || Sqr2(res.Pos, plank.Logic) > range * range)
        {
            res = NearestResource(plank.Logic, range);
            plank.CaptureTarget = res;
        }

        if (res == null)
        {
            return;
        }

        if (!plank.Tower.AimToward(res.Pos, dt))
        {
            return;
        }

        CreateNet(plank.Tower.FirePosition, res.Pos, plank, true);
        plank.CaptureTarget = null;
    }

    private bool IsLiveResource(ResNode res)
    {
        return res != null && !res.Dead && _resources.Contains(res);
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
                EnemyNode target = NearestEnemy(plank.Logic, _config.TowerRange + _attackRangeBonus);
                if (target != null && plank.Tower != null)
                {
                    plank.Tower.AimToward(target.Pos, dt);
                }

                plank.AttackCd -= dt;
                if (plank.AttackCd <= 0f && target != null)
                {
                    ThrowTowerSpear(plank, target, _config.TowerDamage + _attackDamageBonus, _attackBoltPrefab, BattleDamageKind.AttackTower);
                    plank.AttackCd = _config.TowerAttackInterval;
                }
            }
            else if (plank.Building == BattleBuildingKind.Capture)
            {
                TickCaptureTower(plank, dt);
            }
            else if (plank.Building == BattleBuildingKind.Attractor)
            {
                EnemyNode target = NearestEnemy(plank.Logic, _config.AttractorRange + _attractorRangeBonus);
                if (target != null && plank.Tower != null)
                {
                    plank.Tower.AimToward(target.Pos, dt);
                }

                plank.AttractorCd -= dt;
                if (plank.AttractorCd <= 0f && target != null)
                {
                    ThrowTowerSpear(plank, target, _config.AttractorDamage, _attractorBoltPrefab, BattleDamageKind.Attractor);
                    float interval = _config.AttractorInterval * _attractorIntervalMul;
                    if (interval < _config.AttractorIntervalMin)
                    {
                        interval = _config.AttractorIntervalMin;
                    }

                    plank.AttractorCd = interval;
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

        _pendingThrow = PendingThrow.Net;
        _pendingThrowPoint = point;
        if (_characterBody != null)
        {
            _characterBody.PlayThrow();
        }
        else
        {
            ReleasePlayerThrow();
        }
    }

    private NetShot CreateNet(Vector3 origin, Vector3 target, PlankNode home, bool autoReel)
    {
        NetShot net = new NetShot();
        net.Pos = origin;
        net.Target = target;
        net.Home = home;
        net.AutoReel = autoReel;
        net.View = Instantiate(netPrefab, spawnRoot);
        float diameter = _config.NetDiameter + (home == null ? _netSizeBonus : _captureSizeBonus);
        if (diameter < 0.4f)
        {
            diameter = 0.4f;
        }

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

        Vector3 motionStart = net.Pos;
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
                    float speedMul = net.Home == null ? _netSpeedMul : 1f;
                    float fly = _config.NetSpeed * speedMul * dt;
                    net.Pos = Vector3.MoveTowards(net.Pos, net.Target, fly);
                    if (Sqr2(net.Pos, net.Target) <= 0.0025f)
                    {
                        if (!net.DidCapture)
                        {
                            CaptureAt(net);
                            net.DidCapture = true;
                        if (net.Home == null && net.Held.Count > 0)
                        {
                            TutorialBattleHooks.OnPlayerCaptured();
                        }
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

        if (net.View != null)
        {
            float travel = Vector2.Distance(
                new Vector2(motionStart.x, motionStart.y),
                new Vector2(net.Pos.x, net.Pos.y));
            net.View.TickFloat(dt, travel);
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
    private float NetCatchRadius(NetShot net)
    {
        float diameter = _config.NetDiameter + (net != null && net.Home != null ? _captureSizeBonus : _netSizeBonus);
        if (diameter < 0.4f)
        {
            diameter = 0.4f;
        }

        return diameter * 0.5f;
    }

    private void CaptureAlongPath(NetShot net, Vector3 from, Vector3 to)
    {
        float limit = NetCatchRadius(net);
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
        float limit = NetCatchRadius(net);
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
        ResNode res = _resources[index];
        if (net != null && res.CollectibleIndex < 0)
        {
            int cap = net.Home == null ? _netCapacity : _captureNetCapacity;
            if (net.Held.Count >= cap)
            {
                return;
            }
        }

        _resources.RemoveAt(index);
        net.Held.Add(res);
        SetResOrder(res, 31);

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

            if (res.CollectibleIndex >= 0)
            {
                continue;
            }

            if (res.Kind == BattleResourceKind.Iron)
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
        bool fromCaptureTower = net.Home != null && net.Home.Building == BattleBuildingKind.Capture;
        bool fromPlayer = net.Home == null;
        int heldCount = net.Held.Count;
        bool heldMeat = false;
        for (int hi = 0; hi < net.Held.Count; hi++)
        {
            if (net.Held[hi] != null && net.Held[hi].Kind == BattleResourceKind.Meat)
            {
                heldMeat = true;
                break;
            }
        }
        int xpGained = 0;
        for (int i = 0; i < net.Held.Count; i++)
        {
            ResNode res = net.Held[i];
            if (res == null || res.Dead)
            {
                continue;
            }

            if (res.CollectibleIndex >= 0)
            {
                GrantCollectible(res.CollectibleIndex);
            }
            else if (res.Kind == BattleResourceKind.Meat)
            {
                xpGained += _config.XpFor(res.Kind);
            }
            else
            {
                AddCount(res.Kind, 1);
                xpGained += _config.XpFor(res.Kind);
            }

            res.Dead = true;
            DestroyRes(res);
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

            DestroyNetRope(net);
        }
        else if (_playerNet == net)
        {
            _playerNet = null;
            HideNetRope(net);
        }

        if (xpGained > 0)
        {
            AddXp(xpGained);
        }

        if (TutorialBattleHooks.Active && heldCount > 0)
        {
            if (fromPlayer)
            {
                TutorialBattleHooks.OnPlayerDelivered();
                if (heldMeat)
                {
                    TutorialBattleHooks.OnMeatCollected();
                }
            }
            else if (fromCaptureTower)
            {
                TutorialBattleHooks.OnCaptureHarvested();
            }
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

            SetResOrder(res, 1);

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

        if (net.Home != null)
        {
            DestroyNetRope(net);
        }

        if (_playerNet == net)
        {
            _playerNet = null;
            HideNetRope(net);
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
            if (res == null || (res.View == null && res.CollectibleRenderer == null))
            {
                continue;
            }

            float ang = i * 1.15f;
            Vector3 offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.28f;
            res.Pos = net.Pos + offset;
            PlaceRes(res);
        }

        SyncNetRope(net);
    }

    private Vector3 GetNetRopeOrigin(NetShot net)
    {
        if (net != null && net.Home != null)
        {
            if (net.Home.Tower != null)
            {
                return net.Home.Tower.FirePosition;
            }

            return net.Home.Logic;
        }

        return _characterBody != null ? _characterBody.FirePosition : _characterPos;
    }

    private SpriteRenderer EnsureNetRope(NetShot net)
    {
        if (net == null)
        {
            return null;
        }

        if (net.Rope != null)
        {
            return net.Rope;
        }

        if (net.Home == null && _playerRope != null)
        {
            net.Rope = _playerRope;
            return net.Rope;
        }

        GameObject ropeObject = new GameObject("NetRope");
        ropeObject.transform.SetParent(spawnRoot, false);
        SpriteRenderer rope = ropeObject.AddComponent<SpriteRenderer>();
        rope.sprite = _ropeSprite;
        rope.drawMode = SpriteDrawMode.Tiled;
        rope.sortingOrder = 21;
        net.Rope = rope;
        if (net.Home == null)
        {
            _playerRope = rope;
        }

        return rope;
    }

    private void SyncNetRope(NetShot net)
    {
        if (net == null || net.Dead || _ropeSprite == null)
        {
            HideNetRope(net);
            return;
        }

        Vector3 hand = GetNetRopeOrigin(net);
        Vector3 delta = net.Pos - hand;
        delta.z = 0f;
        float length = delta.magnitude;
        if (length < 0.05f)
        {
            HideNetRope(net);
            return;
        }

        SpriteRenderer rope = EnsureNetRope(net);
        if (rope == null)
        {
            return;
        }

        float width = _ropeSprite.bounds.size.x;
        if (width < 0.01f)
        {
            width = 0.3f;
        }

        rope.enabled = true;
        rope.size = new Vector2(width, length);
        Vector3 mid = (hand + net.Pos) * 0.5f;
        mid.z = 0f;
        rope.transform.position = mid;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
        rope.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void HideNetRope(NetShot net)
    {
        if (net != null && net.Rope != null)
        {
            net.Rope.enabled = false;
        }
        else if (_playerRope != null)
        {
            _playerRope.enabled = false;
        }
    }

    private void DestroyNetRope(NetShot net)
    {
        if (net == null)
        {
            return;
        }

        if (net.Rope != null)
        {
            if (net.Rope == _playerRope)
            {
                _playerRope.enabled = false;
            }
            else
            {
                Destroy(net.Rope.gameObject);
            }

            net.Rope = null;
        }
    }
    private void ThrowPlayerSpear(Vector3 point)
    {
        _pendingThrow = PendingThrow.Spear;
        _pendingThrowPoint = point;
        if (_characterBody != null)
        {
            _characterBody.PlayThrow();
        }
        else
        {
            ReleasePlayerThrow();
        }
    }

    private void ReleasePlayerThrow()
    {
        PendingThrow kind = _pendingThrow;
        Vector3 point = _pendingThrowPoint;
        _pendingThrow = PendingThrow.None;
        if (kind == PendingThrow.Net)
        {
            if (_playerNet == null)
            {
                Vector3 origin = _characterBody != null ? _characterBody.FirePosition : _characterPos;
                _playerNet = CreateNet(origin, point, null, false);
            }
        }
        else if (kind == PendingThrow.Spear)
        {
            SpawnPlayerSpear(point);
        }
    }

    private void SpawnPlayerSpear(Vector3 point)
    {
        Vector3 origin = _characterBody != null ? _characterBody.FirePosition : _characterPos;
        SpearShot spear = new SpearShot();
        spear.Pos = origin;
        spear.Origin = origin;
        spear.Target = point;
        spear.Damage = _spearDamageBase + _heroDamageBonus;
        spear.Radius = _config.SpearRadius;
        spear.Kind = BattleDamageKind.Spear;
        spear.View = Instantiate(spearPrefab, spawnRoot);
        spear.View.transform.position = spear.Pos;
        spear.View.AimHead(spear.Target - spear.Origin);
        _spears.Add(spear);
    }

    private void ThrowTowerSpear(PlankNode plank, EnemyNode enemy, float damage, BattleSpear boltPrefab, BattleDamageKind kind)
    {
        Vector3 origin = plank.Tower != null ? plank.Tower.FirePosition : plank.Logic;
        BattleSpear prefab = boltPrefab != null ? boltPrefab : spearPrefab;
        SpearShot spear = new SpearShot();
        spear.Pos = origin;
        spear.Origin = origin;
        spear.Target = enemy.Pos;
        spear.Damage = damage;
        spear.Radius = 0f;
        spear.Lock = enemy;
        spear.Kind = kind;
        spear.View = Instantiate(prefab, spawnRoot);
        spear.View.transform.position = spear.Pos;
        spear.View.AimHead(spear.Target - spear.Origin);
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

            spear.View.AimHead(aim);
        }

        if (Sqr2(spear.Pos, spear.Target) > 0.0025f)
        {
            return;
        }

        spear.Done = true;
        if (spear.Lock != null)
        {
            DamageEnemy(spear.Lock, spear.Damage, spear.Kind);
        }
        else
        {
            EnemyNode hit = NearestEnemy(spear.Target, spear.Radius);
            if (hit != null)
            {
                DamageEnemy(hit, spear.Damage, spear.Kind);
            }
        }

        if (spear.View != null)
        {
            Destroy(spear.View.gameObject);
        }
    }

    private void FireEnemyBolt(EnemyNode enemy, PlankNode plank)
    {
        if (enemy == null || enemy.View == null || _enemyBoltPrefab == null || plank == null || plank.Dead)
        {
            return;
        }

        Vector3 origin = enemy.View.MuzzleWorldPos();
        Vector3 target = plank.Logic;
        if (plank.View != null)
        {
            target = plank.View.transform.position;
        }

        enemy.View.PlayShootRecoil(target - origin);

        EnemyBolt bolt = new EnemyBolt();
        bolt.Pos = origin;
        bolt.Origin = origin;
        bolt.Target = target;
        bolt.Damage = enemy.Damage;
        bolt.TargetPlank = plank;
        bolt.View = Instantiate(_enemyBoltPrefab, spawnRoot);
        bolt.View.transform.position = bolt.Pos;
        bolt.View.AimHead(bolt.Target - bolt.Origin);
        _enemyBolts.Add(bolt);
    }

    private void TickEnemyBolts(float dt)
    {
        for (int i = _enemyBolts.Count - 1; i >= 0; i--)
        {
            TickEnemyBolt(_enemyBolts[i], dt);
            if (_enemyBolts[i].Done)
            {
                _enemyBolts.RemoveAt(i);
            }
        }
    }

    private void TickEnemyBolt(EnemyBolt bolt, float dt)
    {
        if (bolt == null || bolt.Done)
        {
            return;
        }

        bolt.Pos = Vector3.MoveTowards(bolt.Pos, bolt.Target, _config.SpearSpeed * dt);
        if (bolt.View != null)
        {
            bolt.View.transform.position = bolt.Pos;
            Vector3 aim = bolt.Target - bolt.Pos;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = bolt.Target - bolt.Origin;
            }

            bolt.View.AimHead(aim);
        }

        if (Sqr2(bolt.Pos, bolt.Target) > 0.0025f)
        {
            return;
        }

        bolt.Done = true;
        PlankNode hit = bolt.TargetPlank;
        if (hit == null || hit.Dead)
        {
            hit = NearestPlank(bolt.Target);
        }

        if (hit != null && !hit.Dead)
        {
            DamagePlank(hit, bolt.Damage);
        }

        if (bolt.View != null)
        {
            Destroy(bolt.View.gameObject);
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

    private PlankNode NearestPlank(Vector3 pos)
    {
        float best = float.PositiveInfinity;
        PlankNode pick = null;
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead)
            {
                continue;
            }

            float dist = Sqr2(plank.Logic, pos);
            if (dist < best)
            {
                best = dist;
                pick = plank;
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

        if (kind == BattleResourceKind.Iron)
        {
            _iron += amount;
        }
        else if (kind == BattleResourceKind.Wood)
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

        _pendingNeed = need;
        _phase = Phase.LevelUp;
        DrawLevelOffers();
        hud.ShowLevelUpOffers(_levelOffers);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        ArmModal(hud.LevelGroup);
        TouchHud();
    }

    private void DrawLevelOffers()
    {
        _offerPool.Clear();
        bool hasCapture = HasBuilding(BattleBuildingKind.Capture);
        bool hasAttack = HasBuilding(BattleBuildingKind.Attack);
        bool hasAttractor = HasBuilding(BattleBuildingKind.Attractor);

        _offerPool.Add(BattleUpgradeKind.HeroRange);
        _offerPool.Add(BattleUpgradeKind.HeroCapacity);
        _offerPool.Add(BattleUpgradeKind.HeroDamage);
        _offerPool.Add(BattleUpgradeKind.NetSize);
        _offerPool.Add(BattleUpgradeKind.NetSpeed);
        _offerPool.Add(BattleUpgradeKind.GainIron);
        _offerPool.Add(BattleUpgradeKind.GainWood);
        _offerPool.Add(BattleUpgradeKind.RestoreHealth);

        if (hasCapture)
        {
            _offerPool.Add(BattleUpgradeKind.CaptureRange);
            _offerPool.Add(BattleUpgradeKind.CaptureCapacity);
            _offerPool.Add(BattleUpgradeKind.CaptureSize);
            _offerPool.Add(BattleUpgradeKind.CaptureSpeed);
        }

        if (hasAttack)
        {
            _offerPool.Add(BattleUpgradeKind.AttackRange);
            _offerPool.Add(BattleUpgradeKind.AttackDamage);
        }

        if (hasAttractor)
        {
            _offerPool.Add(BattleUpgradeKind.AttractorRange);
            _offerPool.Add(BattleUpgradeKind.AttractorFrequency);
        }

        // 娲楃墝鍚庡彇鍓?3
        for (int i = _offerPool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            BattleUpgradeKind tmp = _offerPool[i];
            _offerPool[i] = _offerPool[j];
            _offerPool[j] = tmp;
        }

        int take = Mathf.Min(3, _offerPool.Count);
        for (int i = 0; i < 3; i++)
        {
            _levelOffers[i] = i < take ? _offerPool[i] : BattleUpgradeKind.HeroRange;
        }
    }

    private bool HasBuilding(BattleBuildingKind kind)
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            if (!_planks[i].Dead && _planks[i].Building == kind)
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyUpgrade(BattleUpgradeKind kind)
    {
        switch (kind)
        {
            case BattleUpgradeKind.Plank:
                CollectExpansionCells(_expandCells);
                if (_expandCells.Count == 0)
                {
                    DrawLevelOffers();
                    hud.ShowLevelUpOffers(_levelOffers);
                    return;
                }

                EnterBuildMode(BuildKind.Plank);
                return;
            case BattleUpgradeKind.PlaceCapture:
                if (FindEmptyPlank() == null)
                {
                    DrawLevelOffers();
                    hud.ShowLevelUpOffers(_levelOffers);
                    return;
                }

                EnterBuildMode(BuildKind.Capture);
                return;
            case BattleUpgradeKind.PlaceAttack:
                if (FindEmptyPlank() == null)
                {
                    DrawLevelOffers();
                    hud.ShowLevelUpOffers(_levelOffers);
                    return;
                }

                EnterBuildMode(BuildKind.Attack);
                return;
            case BattleUpgradeKind.PlaceAttractor:
                if (FindEmptyPlank() == null)
                {
                    DrawLevelOffers();
                    hud.ShowLevelUpOffers(_levelOffers);
                    return;
                }

                EnterBuildMode(BuildKind.Attractor);
                return;
            case BattleUpgradeKind.HeroRange:
                _characterRange += _config.CharacterRangeStep;
                RefreshCharacterRange();
                break;
            case BattleUpgradeKind.HeroCapacity:
                _netCapacity += _config.NetCapacityStep;
                break;
            case BattleUpgradeKind.HeroDamage:
                _heroDamageBonus += _config.HeroDamageStep;
                break;
            case BattleUpgradeKind.NetSize:
                _netSizeBonus += _config.NetDiameterStep;
                break;
            case BattleUpgradeKind.NetSpeed:
                _netSpeedMul += _config.NetSpeedMulStep;
                break;
            case BattleUpgradeKind.CaptureRange:
                _captureRangeBonus += _config.CaptureRangeStep;
                RefreshTowerRanges(BattleBuildingKind.Capture);
                break;
            case BattleUpgradeKind.CaptureCapacity:
                _captureNetCapacity += _config.CaptureCapacityStep;
                ShowTowerRanges(BattleBuildingKind.Capture);
                break;
            case BattleUpgradeKind.CaptureSize:
                _captureSizeBonus += _config.CaptureDiameterStep;
                ShowTowerRanges(BattleBuildingKind.Capture);
                break;
            case BattleUpgradeKind.CaptureSpeed:
                _captureCooldownMul *= Mathf.Max(0.5f, 1f - (_config.CaptureCooldownStep / Mathf.Max(0.01f, _config.CaptureCooldown)));
                ShowTowerRanges(BattleBuildingKind.Capture);
                break;
            case BattleUpgradeKind.AttackRange:
                _attackRangeBonus += _config.AttackRangeStep;
                RefreshTowerRanges(BattleBuildingKind.Attack);
                break;
            case BattleUpgradeKind.AttackDamage:
                _attackDamageBonus += _config.AttackDamageStep;
                ShowTowerRanges(BattleBuildingKind.Attack);
                break;
            case BattleUpgradeKind.AttractorRange:
                _attractorRangeBonus += _config.AttractorRangeStep;
                RefreshTowerRanges(BattleBuildingKind.Attractor);
                break;
            case BattleUpgradeKind.AttractorFrequency:
                _attractorIntervalMul *= Mathf.Max(0.5f, 1f - (_config.AttractorIntervalStep / Mathf.Max(0.01f, _config.AttractorInterval)));
                ShowTowerRanges(BattleBuildingKind.Attractor);
                break;
            case BattleUpgradeKind.GainIron:
                AddCount(BattleResourceKind.Iron, BattleTuning.OfferIronAmount);
                break;
            case BattleUpgradeKind.GainWood:
                AddCount(BattleResourceKind.Wood, BattleTuning.OfferWoodAmount);
                break;
            case BattleUpgradeKind.RestoreHealth:
                _playerHp = _playerHpMax;
                SyncPlankHpFromPlayer();
                RefreshAllPlankHealth();
                TouchHud();
                break;
        }

        CommitLevelUp();
    }

    private float ComputePlayerMaxHp()
    {
        LocalSaveManager save = LocalSaveManager.Instance;
        int level = save != null ? save.CharacterLevel : CharacterUpgradeDefs.MinLevel;
        float hp = CharacterUpgradeDefs.HpAtLevel(level);
        if (save != null)
        {
            string armorId = save.GetEquippedItemId(EquipItemType.Armor);
            GameItemDefs.Def armor = GameItemDefs.Get(armorId);
            if (armor != null)
            {
                hp += Mathf.Max(0f, armor.statBonusValue);
            }
        }

        return hp < 1f ? 1f : hp;
    }

    private void RefreshTowerRanges(BattleBuildingKind flashKind)
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead || plank.Tower == null)
            {
                continue;
            }

            float radius = _config.TowerRange + _attackRangeBonus;
            if (plank.Building == BattleBuildingKind.Capture)
            {
                radius = _config.CaptureRange + _captureRangeBonus;
            }
            else if (plank.Building == BattleBuildingKind.Attractor)
            {
                radius = _config.AttractorRange + _attractorRangeBonus;
            }

            plank.Tower.SetRangeRadius(radius);
            if (plank.Building == flashKind)
            {
                plank.Tower.ShowRangeBriefly();
            }
        }
    }

    private void ShowTowerRanges(BattleBuildingKind kind)
    {
        for (int i = 0; i < _planks.Count; i++)
        {
            PlankNode plank = _planks[i];
            if (plank == null || plank.Dead || plank.Tower == null || plank.Building != kind)
            {
                continue;
            }

            plank.Tower.ShowRangeBriefly();
        }
    }

    private void CommitLevelUp()
    {
        _xp -= _pendingNeed;
        if (_xp < 0)
        {
            _xp = 0;
        }

        _level++;
        // 鍗囩骇鍚庢按闈㈠鍒蜂竴浜涙紓娴佹湪鍙讹紙闅忕瓑绾х暐澧烇紝鑲変粛鍙潵鑷瀮锜癸級
        int burst = _config.DriftLevelUpBurstCount + Mathf.Min(_level - 1, 3);
        SpawnDriftBurst(ScaledDriftCount(burst));
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

        // Scripted newbie tutorial owns victory timing via TutorialRequestVictory.
        // Begin() marks _wavesDone so roster waves stay suppressed; without this
        // guard an empty field would Win()/ShowResult on the first Fight tick.
        if (TutorialBattleHooks.Scripted && !TutorialBattleHooks.VictoryForced)
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
        TutorialBattleHooks.OnVictory();
    }

    private void Lose()
    {
        if (_phase != Phase.Fight)
        {
            return;
        }

        _phase = Phase.Defeat;
        ShowResult(false);
        TutorialBattleHooks.OnDefeat();
    }

    private void ShowResult(bool victory)
    {
        int nextChapter;
        int nextStage;
        bool showPrimary = !victory || TryGetNext(out nextChapter, out nextStage);
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        // Prefer scene Reslut panel; fall back to HUD ResultPanel.
        if (resultPanel == null)
        {
            resultPanel = FindObjectOfType<BattleResultPanel>(true);
        }

        if (resultPanel != null)
        {
            resultPanel.Show(victory, _chapter, _stage, showPrimary);
            ArmModal(resultPanel.CanvasGroup);
            return;
        }

        if (hud != null)
        {
            hud.ShowResult(victory, showPrimary);
            ArmModal(hud.ResultGroup);
        }
    }

    private void ArmModal(CanvasGroup group)
    {
        _modalGroup = group;
        if (hud != null)
        {
            hud.SetGroupBlocks(group, false);
        }
        else if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

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
        BattleTime.Reset();
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
        BattleTime.Reset();
        SceneManager.LoadScene(sceneName);
    }

    private void BeginCameraShake()
    {
        if (_cam == null)
        {
            return;
        }

        // Keep rest pose if a prior shake is still mid-offset.
        if (_shakeTimer <= 0f)
        {
            _camRestPos = _cam.transform.position;
        }

        _shakeTimer = ShakeDuration;
    }

    private void TickCameraShake()
    {
        if (_cam == null || _shakeTimer <= 0f)
        {
            return;
        }

        _shakeTimer -= Time.deltaTime;
        if (_shakeTimer <= 0f)
        {
            _shakeTimer = 0f;
            _cam.transform.position = _camRestPos;
            return;
        }

        float t = _shakeTimer / ShakeDuration;
        float mag = ShakeStrength * t;
        _cam.transform.position = _camRestPos + new Vector3(
            Random.Range(-mag, mag),
            Random.Range(-mag, mag),
            0f);
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
