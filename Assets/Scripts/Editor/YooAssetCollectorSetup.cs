using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

/// <summary>
/// 一键创建 / 修复 YooAsset 收集器配置（Assets/BundleCollectorSetting.asset）。
/// 包名取 YooAssetManager.DefaultPackageName，收集目录 Assets/UI、Assets/Config、Assets/Prefab，
/// 规则与参考项目一致：AddressByFileName + PackDirectory + CollectAll，EnableAddressable 关闭。
/// 菜单：Tools/YooAsset/Setup Collector Setting
/// </summary>
public static class YooAssetCollectorSetup
{
    private const string GroupName = "Default Group";

    private static readonly string[] CollectPaths =
    {
        "Assets/UI",
        "Assets/Config",
        "Assets/Prefab"
    };

    [MenuItem("Tools/YooAsset/Setup Collector Setting")]
    public static void Setup()
    {
        // 1. 确保收集目录存在
        for (int i = 0; i < CollectPaths.Length; i++)
        {
            EnsureFolder(CollectPaths[i]);
        }

        AssetDatabase.Refresh();

        // 2. 获取配置（不存在则由 YooAsset 自动创建于 Assets/BundleCollectorSetting.asset）
        BundleCollectorSetting setting = BundleCollectorSettingData.Setting;
        setting.ShowPackageView = true;
        setting.UniqueBundleName = true;

        // 3. 资源包
        string packageName = YooAssetManager.DefaultPackageName;
        BundleCollectorPackage package = setting.Packages.Find(p => p.PackageName == packageName);
        if (package == null)
        {
            package = BundleCollectorSettingData.CreatePackage(packageName);
        }

        package.EnableAddressable = false;
        package.SupportExtensionless = true;
        package.LocationToLower = false;
        package.IncludeAssetGUID = false;
        package.AutoCollectShaders = true;
        package.IgnoreRuleName = nameof(NormalIgnoreRule);

        // 4. 分组
        BundleCollectorGroup group = package.Groups.Find(g => g.GroupName == GroupName);
        if (group == null)
        {
            group = BundleCollectorSettingData.CreateGroup(package, GroupName);
        }

        group.ActiveRuleName = nameof(EnableGroup);

        // 5. 收集器（GUID 以当前工程的目录 GUID 为准，避免从其它工程拷贝导致失效）
        for (int i = 0; i < CollectPaths.Length; i++)
        {
            string path = CollectPaths[i];
            BundleCollector collector = group.Collectors.Find(c => c.CollectPath == path);
            if (collector == null)
            {
                collector = new BundleCollector
                {
                    CollectPath = path,
                    CollectorType = ECollectorType.MainAssetCollector,
                    AddressRuleName = nameof(AddressByFileName),
                    PackRuleName = nameof(PackDirectory),
                    FilterRuleName = nameof(CollectAll)
                };
                BundleCollectorSettingData.CreateCollector(group, collector);
            }

            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(path);
            BundleCollectorSettingData.ModifyCollector(group, collector);
        }

        BundleCollectorSettingData.ModifyPackage(package);
        BundleCollectorSettingData.SaveFile();

        try
        {
            setting.CheckPackageConfigError(packageName);
            Debug.Log("[YooAssetCollectorSetup] Collector setting is ready. Package: " + packageName +
                      ", collect paths: " + string.Join(", ", CollectPaths));
        }
        catch (System.Exception e)
        {
            Debug.LogError("[YooAssetCollectorSetup] Config error: " + e.Message);
        }

        Selection.activeObject = setting;
        EditorGUIUtility.PingObject(setting);
    }

    private static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath))
        {
            return;
        }

        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}