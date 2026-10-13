using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds BgmRuntime (BgmManager + AudioSource) to LoadScene and assigns
/// Assets/music/export_ofoct.com (2)(2).mp3 as looping BGM.
/// Menu: Tools/Template/Add BgmManager To LoadScene
/// One-shot: BgmSceneSetup.run → BgmSceneSetup.ok / .err
/// </summary>
[InitializeOnLoad]
public static class BgmSceneSetup
{
    private const string LogTag = "[BgmSceneSetup]";
    private const string LoadScenePath = "Assets/Scenes/LoadScene.unity";
    private const string ClipPath = "Assets/music/export_ofoct.com (2)(2).mp3";
    private const string YooAssetObjectName = "YooAssetRuntime";
    public const string RuntimeObjectName = "BgmRuntime";

    private static string MarkerPath => Path.Combine(Directory.GetCurrentDirectory(), "BgmSceneSetup.run");
    private static string OkPath => Path.Combine(Directory.GetCurrentDirectory(), "BgmSceneSetup.ok");
    private static string ErrPath => Path.Combine(Directory.GetCurrentDirectory(), "BgmSceneSetup.err");

    static BgmSceneSetup()
    {
        if (!File.Exists(MarkerPath))
        {
            return;
        }

        EditorApplication.delayCall += TryAutoRun;
    }

    private static void TryAutoRun()
    {
        if (!File.Exists(MarkerPath))
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
            File.Delete(MarkerPath);
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " marker delete failed: " + e.Message);
            return;
        }

        Debug.Log(LogTag + " Auto-run.");
        Run();
    }

    [MenuItem("Tools/Template/Add BgmManager To LoadScene")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run();
    }

    public static void Run()
    {
        try
        {
            string report = WireLoadScene();
            Debug.Log(LogTag + " Done. " + report);
            File.WriteAllText(OkPath, DateTime.Now.ToString("o") + " " + report + "\n");
            if (File.Exists(ErrPath))
            {
                File.Delete(ErrPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError(LogTag + " " + e);
            try
            {
                File.WriteAllText(ErrPath, e.ToString());
            }
            catch
            {
            }
        }
    }

    private static string WireLoadScene()
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
        {
            throw new InvalidOperationException("BGM clip not found at " + ClipPath);
        }

        Scene scene = SceneManager.GetSceneByPath(LoadScenePath);
        bool openedHere = false;
        if (!(scene.IsValid() && scene.isLoaded))
        {
            scene = EditorSceneManager.OpenScene(LoadScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        try
        {
            BgmManager existing = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                existing = roots[i].GetComponentInChildren<BgmManager>(true);
                if (existing != null)
                {
                    break;
                }
            }

            GameObject go;
            BgmManager mgr;
            if (existing != null)
            {
                go = existing.gameObject;
                mgr = existing;
            }
            else
            {
                go = new GameObject(RuntimeObjectName);
                SceneManager.MoveGameObjectToScene(go, scene);
                mgr = go.AddComponent<BgmManager>();
                if (go.GetComponent<AudioSource>() == null)
                {
                    go.AddComponent<AudioSource>();
                }

                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == YooAssetObjectName)
                    {
                        go.transform.SetSiblingIndex(roots[i].transform.GetSiblingIndex() + 1);
                        break;
                    }
                }
            }

            AudioSource source = go.GetComponent<AudioSource>();
            if (source == null)
            {
                source = go.AddComponent<AudioSource>();
            }

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.7f;
            source.clip = clip;

            SerializedObject so = new SerializedObject(mgr);
            so.FindProperty("bgmClip").objectReferenceValue = clip;
            so.FindProperty("volume").floatValue = 0.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(go);
            EditorUtility.SetDirty(mgr);
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Failed to save " + LoadScenePath);
            }

            AssetDatabase.SaveAssets();
            return existing != null
                ? ("Updated existing BgmManager on '" + go.name + "' clip=" + ClipPath)
                : ("Added " + RuntimeObjectName + " clip=" + ClipPath);
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}