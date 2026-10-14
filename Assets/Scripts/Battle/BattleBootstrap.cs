using System.Collections;
using UnityEngine;

/// <summary>
/// 閹稿倸婀?BattleScene 閻?BattleRoot 娑撳鈧倹鎸夐棃顫偓浣稿灥婵娲撻弽鍏兼躬閺夎￥鈧浇顫楅懝灞傗偓涓燯D 瀹稿弶鏂侀崷銊ユ簚閺咁垶鍣烽妴?/// 閺堫剛琚崣顏堚偓姘崇箖 YooAsset 閸旂姾娴囬弫鏉库偓闂寸瑢閹存ɑ鏋熸稉顓熷娴兼氨鏁撻幋鎰畱妫板嫬鍩楁担鎾扁偓?/// 娑撳秴鐤勯悳?BattleWorld閿涙稐绡冩稉宥呮躬鏉╂劘顢戦弮璺哄灡瀵ゆ椽绮拋銈嗘寜闂堫潿鈧礁鍨垫慨瀣躬閺夋寧鍨ㄧ憴鎺曞閵?/// </summary>
[DisallowMultipleComponent]
public class BattleBootstrap : MonoBehaviour
{
    public const string ConfigAddress = "BattleTuningConfig";
    public const string RosterAddress = "StageRosterConfig";
    public const string PlankAddress = "BattlePlank";
    public const string WoodAddress = "ZD-MTP";
    public const string IronAddress = "ZD-STP";
    public const string MeatAddress = "ZD-SRP";
    public const string Enemy1Address = "Enemy1";
    public const string Enemy2Address = "Enemy2";
    public const string Enemy3Address = "Enemy3";
    public const string EnemyBoltAddress = "Bulie3";
    public const string NetAddress = "BattleNet";
    public const string SpearAddress = "BattleSpear";
    public const string CaptureAddress = "BattleCaptureTower";
    public const string AttackAddress = "BattleAttackTower";
    public const string AttractorAddress = "BattleAttractor";
    public const string RopeAddress = "ZB-SZ1";
    public const string AttackBoltAddress = "bulie1";
    public const string AttractorBoltAddress = "bulie2";
    public const string DamakeAddress = "Damake";

    [SerializeField] private BattleDirector director;
    [SerializeField] private YooAssetManager yooAssetManager;

    private IEnumerator Start()
    {
        YooAssetManager yoo = YooAssetManager.Instance != null ? YooAssetManager.Instance : yooAssetManager;
        if (yoo == null)
        {
            Debug.LogError("[Battle] YooAssetManager is missing from BattleScene.");
            yield break;
        }

        if (!yoo.IsReady)
        {
            yield return yoo.InitializeAsync();
        }

        if (!yoo.IsReady)
        {
            Debug.LogError("[Battle] DefaultPackage failed to initialize.");
            yield break;
        }

        if (director == null)
        {
            Debug.LogError("[Battle] BattleDirector is not assigned on BattleRoot.");
            yield break;
        }

        BattleTuningConfig config = null;
        yield return yoo.LoadConfigAsync<BattleTuningConfig>(ConfigAddress, loaded => config = loaded);
        StageRosterConfig roster = null;
        yield return yoo.LoadConfigAsync<StageRosterConfig>(RosterAddress, loaded => roster = loaded);

        if (GameItemDefs.Current == null)
        {
            ItemConfig items = null;
            yield return yoo.LoadConfigAsync<ItemConfig>(GameItemDefs.ConfigAddress, loaded => items = loaded);
            if (items != null)
            {
                GameItemDefs.Bind(items);
            }
        }

        PlankView plank = null;
        BattleResource wood = null;
        BattleResource iron = null;
        BattleResource meat = null;
        BattleEnemy enemy1 = null;
        BattleEnemy enemy2 = null;
        BattleEnemy enemy3 = null;
        BattleNet net = null;
        BattleSpear spear = null;
        BattleTower capture = null;
        BattleTower attack = null;
        BattleTower attractor = null;
        yield return LoadPrefab(yoo, PlankAddress, go => plank = go != null ? go.GetComponent<PlankView>() : null);
        yield return LoadPrefab(yoo, WoodAddress, go => wood = go != null ? go.GetComponent<BattleResource>() : null);
        yield return LoadPrefab(yoo, IronAddress, go => iron = go != null ? go.GetComponent<BattleResource>() : null);
        yield return LoadPrefab(yoo, MeatAddress, go => meat = go != null ? go.GetComponent<BattleResource>() : null);
        yield return LoadPrefab(yoo, Enemy1Address, go => enemy1 = go != null ? go.GetComponent<BattleEnemy>() : null);
        yield return LoadPrefab(yoo, Enemy2Address, go => enemy2 = go != null ? go.GetComponent<BattleEnemy>() : null);
        yield return LoadPrefab(yoo, Enemy3Address, go => enemy3 = go != null ? go.GetComponent<BattleEnemy>() : null);
        yield return LoadPrefab(yoo, NetAddress, go => net = go != null ? go.GetComponent<BattleNet>() : null);
        yield return LoadPrefab(yoo, SpearAddress, go => spear = go != null ? go.GetComponent<BattleSpear>() : null);
        yield return LoadPrefab(yoo, CaptureAddress, go => capture = go != null ? go.GetComponent<BattleTower>() : null);
        yield return LoadPrefab(yoo, AttackAddress, go => attack = go != null ? go.GetComponent<BattleTower>() : null);
        yield return LoadPrefab(yoo, AttractorAddress, go => attractor = go != null ? go.GetComponent<BattleTower>() : null);

        if (config == null || roster == null || plank == null || wood == null || iron == null || meat == null ||
            enemy1 == null || enemy2 == null || enemy3 == null ||
            net == null || spear == null || capture == null || attack == null || attractor == null)
        {
            Debug.LogError("[Battle] Failed to load battle config or runtime prefabs through YooAsset.");
            yield break;
        }

        Sprite rope = null;
        yield return yoo.LoadSpriteAsync(RopeAddress, loaded => rope = loaded);
        if (rope == null)
        {
            Debug.LogError("[Battle] Failed to load net rope sprite " + RopeAddress + ".");
        }

        BattleSpear attackBolt = null;
        BattleSpear attractorBolt = null;
        BattleSpear enemyBolt = null;
        yield return LoadPrefab(yoo, AttackBoltAddress, go => attackBolt = go != null ? go.GetComponent<BattleSpear>() : null);
        yield return LoadPrefab(yoo, AttractorBoltAddress, go => attractorBolt = go != null ? go.GetComponent<BattleSpear>() : null);
        yield return LoadPrefab(yoo, EnemyBoltAddress, go => enemyBolt = go != null ? go.GetComponent<BattleSpear>() : null);
        if (attackBolt == null)
        {
            Debug.LogError("[Battle] Failed to load attack bolt " + AttackBoltAddress + ".");
        }

        if (attractorBolt == null)
        {
            Debug.LogError("[Battle] Failed to load attractor bolt " + AttractorBoltAddress + ".");
        }

        if (enemyBolt == null)
        {
            Debug.LogError("[Battle] Failed to load enemy bolt " + EnemyBoltAddress + ".");
        }

        DamakePopup damake = null;
        yield return LoadPrefab(yoo, DamakeAddress, go => damake = go != null ? go.GetComponent<DamakePopup>() : null);
        if (damake == null)
        {
            Debug.LogError("[Battle] Failed to load damage float " + DamakeAddress + ".");
        }

        director.SetSpawnPrefabs(plank, wood, iron, meat, enemy1, enemy2, enemy3, net, spear, capture, attack, attractor);
        director.SetRopeSprite(rope);
        director.SetTowerBolts(attackBolt, attractorBolt);
        director.SetEnemyBolt(enemyBolt);
        director.SetDamakePrefab(damake);
        director.SetStageRoster(roster);
        director.Begin(config);
    }

    private static IEnumerator LoadPrefab(YooAssetManager yoo, string address, System.Action<GameObject> onDone)
    {
        GameObject prefab = null;
        yield return yoo.LoadPrefabAsync(address, loaded => prefab = loaded);
        onDone(prefab);
    }
}
