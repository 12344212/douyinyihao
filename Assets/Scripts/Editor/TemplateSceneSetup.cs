using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一键生成模板空场景骨架：LoadScene(0) / MainScene(1) / BattleScene(2)，并注册到 Build Settings。
/// - LoadScene / MainScene：Main Camera(正交，作为 UI 相机) + Canvas(Screen Space - Camera, 1920x1080, Expand) + EventSystem(StandaloneInputModule)
/// - LoadScene 额外挂 YooAssetRuntime(YooAssetManager，packageName 保持默认 DefaultPackage)
/// - LoadScene 额外挂 LocalSaveRuntime(LocalSaveManager，本地存档)
/// - LoadScene 的 Canvas 下生成 Loading UI 并挂 LoadSceneController（见 LoadSceneUISetup）
/// - MainScene 生成主界面 / CanvasPanel 面板 / CanvasMax 顶栏并挂 MainUIController（见 MainSceneUISetup）
/// - BattleScene：仅 Main Camera
/// - Player Settings：横屏（Default Orientation = Landscape Left）
/// - SampleScene 若为未修改的默认场景则删除
/// 菜单：Tools/Template/Create Template Scenes
/// 自动执行：项目根目录存在 TemplateSceneSetup.run 标记文件时，编辑器加载后执行一次并删除该标记文件。
/// </summary>
[InitializeOnLoad]
public static class TemplateSceneSetup
{
    private const string LogTag = "[TemplateSceneSetup]";
    private const string SceneFolder = "Assets/Scenes";
    private const string LoadScenePath = SceneFolder + "/LoadScene.unity";
    private const string MainScenePath = SceneFolder + "/MainScene.unity";
    private const string BattleScenePath = SceneFolder + "/BattleScene.unity";
    private const string SampleScenePath = SceneFolder + "/SampleScene.unity";

    // 未修改的 Unity 2022.3 默认 SampleScene.unity 的 SHA256（仅在完全一致时才删除）
    private const string DefaultSampleSceneSha256 = "6BA4BB647064E63BAE95BADC79AF95CB2B33589E0DBAA29DD6E382D43657F9C7";

    private const string ManagerObjectName = "YooAssetRuntime";
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // 一次性自动执行的标记文件（位于项目根目录，Assets 之外）
    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "TemplateSceneSetup.run");

    static TemplateSceneSetup()
    {
        if (!File.Exists(MarkerFilePath))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerFilePath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            // 稍后重试，标记文件保留
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

        // 先删除标记，保证只执行一次
        try
        {
            File.Delete(MarkerFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed to delete marker file, auto-run aborted: {e.Message}");
            return;
        }

        Debug.Log($"{LogTag} Auto-run triggered by marker file.");
        CreateAll(false);
    }

    [MenuItem("Tools/Template/Create Template Scenes")]
    public static void CreateTemplateScenesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        CreateAll(true);
    }

    private static void CreateAll(bool interactive)
    {
        try
        {
            EnsureFolder(SceneFolder);

            bool anyExists = File.Exists(LoadScenePath) || File.Exists(MainScenePath) || File.Exists(BattleScenePath);
            if (anyExists)
            {
                if (!interactive)
                {
                    Debug.LogWarning($"{LogTag} Template scenes already exist, auto-run will not overwrite them.");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Create Template Scenes",
                        "One or more template scenes already exist in Assets/Scenes. Overwrite them?", "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            // 以 Additive 方式新建并保存场景，不影响当前打开的场景（包括其未保存的修改）
            // 1. LoadScene
            {
                Scene scene = BeginScene();
                Camera cam = CreateUICamera();
                Canvas loadCanvas = CreateCanvas(cam);
                CreateEventSystem();

                GameObject manager = new GameObject(ManagerObjectName);
                YooAssetManager yoo = manager.AddComponent<YooAssetManager>(); // packageName 保持默认 DefaultPackage
                GameObject saveGo = LocalSaveSceneSetup.CreateRuntimeObject(); // LocalSaveRuntime(LocalSaveManager)
                LoadSceneUISetup.BuildUI(loadCanvas, yoo, saveGo.GetComponent<LocalSaveManager>()); // Loading UI + LoadSceneController（挂在 Canvas 上）

                EndScene(scene, LoadScenePath);
            }

            // 2. MainScene
            {
                Scene scene = BeginScene();
                Camera cam = CreateUICamera();
                Canvas mainCanvas = CreateCanvas(cam);
                CreateEventSystem();
                MainSceneUISetup.BuildUI(mainCanvas); // 主界面 + CanvasPanel 面板 + CanvasMax 顶栏（TopBarPrefab）
                EndScene(scene, MainScenePath);
            }

            // 3. BattleScene：仅 Main Camera
            {
                Scene scene = BeginScene();
                CreateMainCamera();
                EndScene(scene, BattleScenePath);
            }

            // 4. Build Settings 顺序：LoadScene(0) / MainScene(1) / BattleScene(2)，其它已有场景保留在后面
            RegisterBuildScenes();

            // 5. 横屏
            ApplyLandscapeOrientation();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6. 菜单模式：打开 LoadScene，再删除未修改的默认 SampleScene
            //    自动模式：不切换用户当前场景；SampleScene 若仍处于打开状态则保留
            if (interactive)
            {
                EditorSceneManager.OpenScene(LoadScenePath, OpenSceneMode.Single);
            }

            TryDeleteDefaultSampleScene();

            AssetDatabase.SaveAssets();
            Debug.Log($"{LogTag} Done. Build scenes: {string.Join(", ", GetBuildScenePaths())}; defaultOrientation={PlayerSettings.defaultInterfaceOrientation}");
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed: {e}");
        }
    }

    private static Camera CreateMainCamera()
    {
        GameObject go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 0f, -10f);
        Camera cam = go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        return cam;
    }

    private static Camera CreateUICamera()
    {
        Camera cam = CreateMainCamera();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;
        return cam;
    }

    private static Canvas CreateCanvas(Camera uiCamera)
    {
        GameObject go = new GameObject("Canvas", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCamera;
        canvas.planeDistance = 100f;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.referencePixelsPerUnit = 100f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private static Scene _previousActiveScene;

    private static Scene BeginScene()
    {
        _previousActiveScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene); // 新建的 GameObject 进入该场景
        return scene;
    }

    private static void EndScene(Scene scene, string path)
    {
        bool saved = EditorSceneManager.SaveScene(scene, path);

        if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(_previousActiveScene);
        }

        if (SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        if (!saved)
        {
            throw new Exception("Failed to save scene: " + path);
        }

        Debug.Log($"{LogTag} Saved {path}");
    }

    private static void RegisterBuildScenes()
    {
        string[] ordered = { LoadScenePath, MainScenePath, BattleScenePath };
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        for (int i = 0; i < ordered.Length; i++)
        {
            list.Add(new EditorBuildSettingsScene(ordered[i], true));
        }

        foreach (EditorBuildSettingsScene old in EditorBuildSettings.scenes)
        {
            if (Array.IndexOf(ordered, old.path) >= 0 || old.path == SampleScenePath)
            {
                continue;
            }

            list.Add(old);
        }

        EditorBuildSettings.scenes = list.ToArray();
    }

    private static List<string> GetBuildScenePaths()
    {
        List<string> paths = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            paths.Add(s.path);
        }

        return paths;
    }

    private static void ApplyLandscapeOrientation()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        // 若之后切换为 Auto Rotation，也只允许两个横屏方向
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
    }

    private static void TryDeleteDefaultSampleScene()
    {
        if (!File.Exists(SampleScenePath))
        {
            return;
        }

        Scene opened = SceneManager.GetSceneByPath(SampleScenePath);
        if (opened.IsValid() && opened.isLoaded)
        {
            Debug.Log($"{LogTag} SampleScene is currently open in the editor, kept. Close it and delete it manually if unused.");
            return;
        }

        string hash;
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(SampleScenePath))
        {
            hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty);
        }

        if (!string.Equals(hash, DefaultSampleSceneSha256, StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"{LogTag} SampleScene has been modified, kept.");
            return;
        }

        if (AssetDatabase.DeleteAsset(SampleScenePath))
        {
            Debug.Log($"{LogTag} Deleted untouched default {SampleScenePath}");
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
