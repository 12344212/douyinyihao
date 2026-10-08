using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 把 LocalSaveManager 挂到 LoadScene：新建根节点 LocalSaveRuntime（排在 YooAssetRuntime 后面）；场景里已有 LocalSaveManager 时跳过。
/// 菜单：Tools/Template/Add LocalSaveManager To LoadScene
/// 自动执行：项目根目录存在 LocalSaveSceneSetup.run 标记文件时，编辑器编译加载后执行一次并删除该标记文件。
/// </summary>
[InitializeOnLoad]
public static class LocalSaveSceneSetup
{
    private const string LogTag = "[LocalSaveSceneSetup]";
    private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";
    private const string YooAssetObjectName = "YooAssetRuntime";
    public const string RuntimeObjectName = "LocalSaveRuntime";

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "LocalSaveSceneSetup.run");

    static LocalSaveSceneSetup()
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
            EditorApplication.delayCall += TryAutoRun;
            return;
        }

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
        AddToLoadScene(false);
    }

    [MenuItem("Tools/Template/Add LocalSaveManager To LoadScene")]
    public static void AddToLoadSceneMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        AddToLoadScene(true);
    }

    /// <summary>在当前活动场景创建 LocalSaveRuntime 节点（TemplateSceneSetup 生成 LoadScene 时也会调用）。</summary>
    public static GameObject CreateRuntimeObject()
    {
        GameObject go = new GameObject(RuntimeObjectName);
        go.AddComponent<LocalSaveManager>();
        return go;
    }

    private static void AddToLoadScene(bool interactive)
    {
        try
        {
            if (!File.Exists(LoadScenePath))
            {
                Debug.LogError($"{LogTag} {LoadScenePath} not found. Create template scenes first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(LoadScenePath);
            bool openedHere = false;
            if (scene.IsValid() && scene.isLoaded)
            {
                if (scene.isDirty && !interactive)
                {
                    Debug.LogWarning($"{LogTag} LoadScene has unsaved changes, auto-run skipped. Save it and use Tools/Template/Add LocalSaveManager To LoadScene.");
                    return;
                }
            }
            else
            {
                scene = EditorSceneManager.OpenScene(LoadScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    LocalSaveManager existing = roots[i].GetComponentInChildren<LocalSaveManager>(true);
                    if (existing != null)
                    {
                        Debug.Log($"{LogTag} LoadScene already has LocalSaveManager on '{existing.gameObject.name}', skipped.");
                        return;
                    }
                }

                GameObject go = CreateRuntimeObject();
                SceneManager.MoveGameObjectToScene(go, scene);

                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == YooAssetObjectName)
                    {
                        go.transform.SetSiblingIndex(roots[i].transform.GetSiblingIndex() + 1);
                        break;
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} Added {RuntimeObjectName} (LocalSaveManager) to {LoadScenePath} and saved.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save {LoadScenePath}.");
                }
            }
            finally
            {
                if (openedHere && SceneManager.sceneCount > 1)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} Failed: {e}");
        }
    }
}