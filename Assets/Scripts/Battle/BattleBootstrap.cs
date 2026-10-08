using System.Collections;
using UnityEngine;

/// <summary>
/// 挂在 BattleScene 的 BattleRoot 上。水面、初始四格木板、角色和 HUD 已放在场景里。
/// 这里只通过 YooAsset 加载数值，以及开局之后才会生成的预制体。
/// 不实例化 BattleWorld，也不在运行时生成默认水面、初始木板或角色。
/// </summary>
[DisallowMultipleComponent]
public class BattleBootstrap : MonoBehaviour
{
    public const string ConfigAddress = "BattleTuningConfig";
    public const string PlankAddress = "BattlePlank";
    public const string ResourceAddress = "BattleResource";
    public const string CrabAddress = "BattleCrab";
    public const string NetAddress = "BattleNet";
    public const string SpearAddress = "BattleSpear";
    public const string CaptureAddress = "BattleCaptureTower";
    public const string AttackAddress = "BattleAttackTower";

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

        PlankView plank = null;
        BattleResource resource = null;
        BattleCrab crab = null;
        BattleNet net = null;
        BattleSpear spear = null;
        BattleTower capture = null;
        BattleTower attack = null;
        yield return LoadPrefab(yoo, PlankAddress, go => plank = go != null ? go.GetComponent<PlankView>() : null);
        yield return LoadPrefab(yoo, ResourceAddress, go => resource = go != null ? go.GetComponent<BattleResource>() : null);
        yield return LoadPrefab(yoo, CrabAddress, go => crab = go != null ? go.GetComponent<BattleCrab>() : null);
        yield return LoadPrefab(yoo, NetAddress, go => net = go != null ? go.GetComponent<BattleNet>() : null);
        yield return LoadPrefab(yoo, SpearAddress, go => spear = go != null ? go.GetComponent<BattleSpear>() : null);
        yield return LoadPrefab(yoo, CaptureAddress, go => capture = go != null ? go.GetComponent<BattleTower>() : null);
        yield return LoadPrefab(yoo, AttackAddress, go => attack = go != null ? go.GetComponent<BattleTower>() : null);

        if (config == null || plank == null || resource == null || crab == null || net == null || spear == null || capture == null || attack == null)
        {
            Debug.LogError("[Battle] Failed to load battle config or runtime prefabs through YooAsset.");
            yield break;
        }

        director.SetSpawnPrefabs(plank, resource, crab, net, spear, capture, attack);
        director.Begin(config);
    }

    private static IEnumerator LoadPrefab(YooAssetManager yoo, string address, System.Action<GameObject> onDone)
    {
        GameObject prefab = null;
        yield return yoo.LoadPrefabAsync(address, loaded => prefab = loaded);
        onDone(prefab);
    }
}