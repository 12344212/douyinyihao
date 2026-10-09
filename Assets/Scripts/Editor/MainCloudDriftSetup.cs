using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 MainScene 的主 Canvas 下建 CloudLayer，把 yun1/yun2 收进对象池，挂上 MainCloudDrift。
/// 层级：Tiankong(天) → CloudLayer(云) → bg(海/角色) → 按钮，云不挡点击。
/// 菜单：Tools/Template/Build Main Cloud Drift
/// 自动执行：项目根目录存在 MainCloudDriftSetup.run 时执行一次。
/// </summary>
[InitializeOnLoad]
public static class MainCloudDriftSetup
{
    private const string LogTag = "[MainCloudDriftSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string CloudLayerName = "CloudLayer";
    private const int PoolSize = 10;
    private const string Yun1Guid = "8c7245e0227399a47868d4d07d95a487";
    private const string Yun2Guid = "4348a8ef261287e4fb2084cebcbc0705";

    private static string MarkerFilePath => Path.Combine(Directory.GetCurrentDirectory(), "MainCloudDriftSetup.run");

    static MainCloudDriftSetup()
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
        Run(false);
    }

    [MenuItem("Tools/Template/Build Main Cloud Drift")]
    public static void BuildMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Run(true);
    }

    /// <summary>给已有主 Canvas 挂云层。MainSceneUISetup 重建主界面后也可再调一次。</summary>
    public static void BuildInto(Canvas mainCanvas)
    {
        if (mainCanvas == null)
        {
            throw new Exception("Main Canvas is missing.");
        }

        Sprite[] sprites = LoadCloudSprites();
        if (sprites.Length == 0)
        {
            throw new Exception("Cloud sprites yun1/yun2 not found (ZJM-Y1 / ZJM-Y2).");
        }

        RectTransform canvasRt = mainCanvas.transform as RectTransform;
        CollectExistingYun(canvasRt, out List<Image> existingYun, out List<Sprite> fromYun);
        if (fromYun.Count > 0)
        {
            sprites = MergeSprites(sprites, fromYun);
        }

        // 已有且引用齐全则跳过重建（菜单交互除外由 Run 决定）
        MainCloudDrift existing = mainCanvas.GetComponentInChildren<MainCloudDrift>(true);
        RectTransform cloudRoot = EnsureCloudLayer(canvasRt);
        Image[] pool = EnsurePool(cloudRoot, PoolSize, sprites, existingYun);

        // 去掉 Canvas 下残留的独立 yun（已并入池或禁用）
        DestroyStandaloneYun(canvasRt, cloudRoot);

        PlaceCloudLayer(canvasRt, cloudRoot);

        if (existing == null)
        {
            existing = cloudRoot.gameObject.AddComponent<MainCloudDrift>();
        }
        else if (existing.transform != cloudRoot)
        {
            UnityEngine.Object.DestroyImmediate(existing);
            existing = cloudRoot.gameObject.AddComponent<MainCloudDrift>();
        }

        SerializedObject so = new SerializedObject(existing);
        SetRef(so, "cloudRoot", cloudRoot);
        SerializedProperty poolProp = so.FindProperty("cloudPool");
        poolProp.arraySize = pool.Length;
        for (int i = 0; i < pool.Length; i++)
        {
            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = pool[i];
        }

        SerializedProperty spritesProp = so.FindProperty("cloudSprites");
        spritesProp.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        so.FindProperty("minActive").intValue = 4;
        so.FindProperty("maxActive").intValue = Mathf.Min(10, pool.Length);
        so.FindProperty("flyLeftToRight").boolValue = true;
        so.FindProperty("spawnOnScreenFirstEnter").boolValue = true;
        so.FindProperty("firstEnterCount").intValue = Mathf.Min(10, pool.Length);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Run(bool interactive)
    {
        try
        {
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError($"{LogTag} {MainScenePath} not found.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedHere = false;
            if (!(scene.IsValid() && scene.isLoaded))
            {
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            else if (scene.isDirty && !interactive)
            {
                Debug.LogWarning($"{LogTag} MainScene has unsaved changes, auto-run skipped.");
                return;
            }

            Canvas canvas = FindMainCanvas(scene);
            if (canvas == null)
            {
                Debug.LogError($"{LogTag} Main Canvas not found.");
                CloseIfOpened(scene, openedHere);
                return;
            }

            MainCloudDrift already = canvas.GetComponentInChildren<MainCloudDrift>(true);
            if (already != null && HasWiredPool(already))
            {
                if (!interactive)
                {
                    Debug.Log($"{LogTag} MainCloudDrift already wired, auto-run skipped.");
                    CloseIfOpened(scene, openedHere);
                    return;
                }

                if (!EditorUtility.DisplayDialog("Build Main Cloud Drift", "Cloud drift already exists. Rebuild it?", "Rebuild", "Cancel"))
                {
                    return;
                }
            }

            Scene previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                BuildInto(canvas);
                EditorSceneManager.MarkSceneDirty(scene);
                if (EditorSceneManager.SaveScene(scene))
                {
                    Debug.Log($"{LogTag} CloudLayer + MainCloudDrift built. Pool={PoolSize}, sprites=yun1+yun2.");
                }
                else
                {
                    Debug.LogError($"{LogTag} Failed to save MainScene.");
                }
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }

                CloseIfOpened(scene, openedHere);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{LogTag} {e}");
        }
    }

    private static Canvas FindMainCanvas(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != "Canvas")
            {
                continue;
            }

            Canvas c = roots[i].GetComponent<Canvas>();
            if (c != null)
            {
                return c;
            }
        }

        return null;
    }

    private static RectTransform EnsureCloudLayer(RectTransform canvasRt)
    {
        Transform t = canvasRt.Find(CloudLayerName);
        RectTransform rt;
        if (t != null)
        {
            rt = t as RectTransform;
        }
        else
        {
            GameObject go = new GameObject(CloudLayerName, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(canvasRt, false);
            rt = go.GetComponent<RectTransform>();
        }

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        // 不挡点击：根节点不要挂 Image；若有则关掉 raycast
        Image rootImg = rt.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.raycastTarget = false;
        }

        return rt;
    }

    private static Image[] EnsurePool(RectTransform cloudRoot, int count, Sprite[] sprites, List<Image> reuse)
    {
        List<Image> pool = new List<Image>(count);

        // 先清掉 CloudLayer 里旧的云图（重建）
        for (int i = cloudRoot.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(cloudRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < count; i++)
        {
            Image img = null;
            if (reuse != null && i < reuse.Count && reuse[i] != null)
            {
                img = reuse[i];
                img.transform.SetParent(cloudRoot, false);
                img.gameObject.name = "Cloud_" + i;
            }
            else
            {
                GameObject go = new GameObject("Cloud_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = LayerMask.NameToLayer("UI");
                go.transform.SetParent(cloudRoot, false);
                img = go.GetComponent<Image>();
            }

            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(421f, 123f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            if (sprites != null && sprites.Length > 0)
            {
                img.sprite = sprites[i % sprites.Length];
            }

            img.color = Color.white;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.gameObject.SetActive(false);
            pool.Add(img);
        }

        return pool.ToArray();
    }

    private static void CollectExistingYun(RectTransform canvasRt, out List<Image> images, out List<Sprite> sprites)
    {
        images = new List<Image>(4);
        sprites = new List<Sprite>(4);
        for (int i = 0; i < canvasRt.childCount; i++)
        {
            Transform child = canvasRt.GetChild(i);
            if (child.name != "yun1" && child.name != "yun2" && !child.name.StartsWith("yun", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Image img = child.GetComponent<Image>();
            if (img == null)
            {
                continue;
            }

            images.Add(img);
            if (img.sprite != null && !sprites.Contains(img.sprite))
            {
                sprites.Add(img.sprite);
            }
        }
    }

    private static void DestroyStandaloneYun(RectTransform canvasRt, RectTransform cloudRoot)
    {
        for (int i = canvasRt.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasRt.GetChild(i);
            if (child == cloudRoot)
            {
                continue;
            }

            if (child.name == "yun1" || child.name == "yun2" || child.name.StartsWith("yun", StringComparison.OrdinalIgnoreCase))
            {
                // 若已被挪进 CloudLayer，不会再走到这里；残留的独立 yun 删掉
                if (child.parent == canvasRt)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    /// <summary>Tiankong → CloudLayer → bg → 按钮。云在天空上、在海/角色下面。</summary>
    private static void PlaceCloudLayer(RectTransform canvasRt, RectTransform cloudRoot)
    {
        int insertAt = 0;
        Transform tiankong = canvasRt.Find("Tiankong");
        Transform bg = canvasRt.Find("bg");
        if (tiankong != null)
        {
            insertAt = tiankong.GetSiblingIndex() + 1;
        }
        else if (bg != null)
        {
            // 没有天空时插到 bg 之前，避免被整屏海图挡住
            insertAt = bg.GetSiblingIndex();
        }

        cloudRoot.SetSiblingIndex(insertAt);
    }

    private static Sprite[] LoadCloudSprites()
    {
        List<Sprite> list = new List<Sprite>(2);
        AddSpriteByGuid(list, Yun1Guid);
        AddSpriteByGuid(list, Yun2Guid);
        return list.ToArray();
    }

    private static void AddSpriteByGuid(List<Sprite> list, string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp != null)
        {
            list.Add(sp);
        }
    }

    private static Sprite[] MergeSprites(Sprite[] a, List<Sprite> b)
    {
        List<Sprite> list = new List<Sprite>(a);
        for (int i = 0; i < b.Count; i++)
        {
            if (b[i] != null && !list.Contains(b[i]))
            {
                list.Add(b[i]);
            }
        }

        return list.ToArray();
    }

    private static bool HasWiredPool(MainCloudDrift drift)
    {
        SerializedObject so = new SerializedObject(drift);
        SerializedProperty root = so.FindProperty("cloudRoot");
        SerializedProperty pool = so.FindProperty("cloudPool");
        SerializedProperty sprites = so.FindProperty("cloudSprites");
        return root.objectReferenceValue != null
               && pool != null && pool.arraySize >= 4
               && sprites != null && sprites.arraySize >= 1;
    }

    private static void CloseIfOpened(Scene scene, bool openedHere)
    {
        if (openedHere && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void SetRef(SerializedObject so, string field, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            throw new Exception("Field not found: " + field);
        }

        p.objectReferenceValue = value;
    }
}
