using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 在 MainScene 的主 Canvas 下建 CloudLayerBack / CloudLayerFront，把 yun 收进对象池，挂上 MainCloudDrift。
/// 层级：Tiankong → CloudLayerBack(远/中) → bg → Lang → CloudLayerFront(近景大云) → 按钮。云 raycast 关闭。
/// 菜单：Tools/Template/Build Main Cloud Drift
/// 自动执行：项目根目录存在 MainCloudDriftSetup.run 时执行一次。
/// </summary>
[InitializeOnLoad]
public static class MainCloudDriftSetup
{
    private const string LogTag = "[MainCloudDriftSetup]";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string CloudLayerBackName = "CloudLayerBack";
    private const string CloudLayerFrontName = "CloudLayerFront";
    private const string CloudLayerLegacyName = "CloudLayer";
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

        MainCloudDrift existing = mainCanvas.GetComponentInChildren<MainCloudDrift>(true);
        RectTransform back = EnsureCloudLayer(canvasRt, CloudLayerBackName);
        RectTransform front = EnsureCloudLayer(canvasRt, CloudLayerFrontName);
        // 旧名 CloudLayer 迁到 Back
        Transform legacy = canvasRt.Find(CloudLayerLegacyName);
        if (legacy != null && legacy != back)
        {
            for (int i = legacy.childCount - 1; i >= 0; i--)
            {
                legacy.GetChild(i).SetParent(back, false);
            }

            if (existing != null && existing.transform == legacy)
            {
                UnityEngine.Object.DestroyImmediate(existing);
                existing = null;
            }

            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }

        Image[] pool = EnsurePool(back, front, PoolSize, sprites, existingYun);
        DestroyStandaloneYun(canvasRt, back, front);
        PlaceCloudLayers(canvasRt, back, front);

        if (existing == null)
        {
            existing = back.gameObject.AddComponent<MainCloudDrift>();
        }
        else if (existing.transform != back && existing.transform != front)
        {
            UnityEngine.Object.DestroyImmediate(existing);
            existing = back.gameObject.AddComponent<MainCloudDrift>();
        }

        SerializedObject so = new SerializedObject(existing);
        SetRef(so, "cloudRootBack", back);
        SetRef(so, "cloudRootFront", front);
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
        // 写入收小后的近景 Z 倾角（避免场景里残留旧默认）
        SerializedProperty near = so.FindProperty("nearLayer");
        if (near != null)
        {
            near.FindPropertyRelative("tiltAngleRange").vector2Value = new Vector2(0.8f, 2.0f);
            near.FindPropertyRelative("scaleRange").vector2Value = new Vector2(0.95f, 1.12f);
            near.FindPropertyRelative("bobAmplitudeRange").vector2Value = new Vector2(6f, 12f);
        }

        SerializedProperty mid = so.FindProperty("midLayer");
        if (mid != null)
        {
            mid.FindPropertyRelative("tiltAngleRange").vector2Value = new Vector2(0.5f, 1.2f);
        }

        SerializedProperty far = so.FindProperty("farLayer");
        if (far != null)
        {
            far.FindPropertyRelative("tiltAngleRange").vector2Value = new Vector2(0.2f, 0.6f);
        }

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
                Debug.LogWarning($"{LogTag} MainScene has unsaved changes; auto-run will apply cloud layers and save.");
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
                    // 缺 Front 根时强制升级接线
                    SerializedObject chk = new SerializedObject(already);
                    if (chk.FindProperty("cloudRootFront").objectReferenceValue != null)
                    {
                        Debug.Log($"{LogTag} MainCloudDrift already wired, auto-run skipped.");
                        CloseIfOpened(scene, openedHere);
                        return;
                    }
                }
                else if (!EditorUtility.DisplayDialog("Build Main Cloud Drift", "Cloud drift already exists. Rebuild it?", "Rebuild", "Cancel"))
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
                    Debug.Log($"{LogTag} CloudLayerBack/Front + MainCloudDrift built. Pool={PoolSize}.");
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

    private static RectTransform EnsureCloudLayer(RectTransform canvasRt, string name)
    {
        Transform t = canvasRt.Find(name);
        RectTransform rt;
        if (t != null)
        {
            rt = t as RectTransform;
        }
        else
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
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

        Image rootImg = rt.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.raycastTarget = false;
        }

        return rt;
    }

    private static Image[] EnsurePool(RectTransform back, RectTransform front, int count, Sprite[] sprites, List<Image> reuse)
    {
        List<Image> pool = new List<Image>(count);

        ClearChildren(back);
        ClearChildren(front);

        for (int i = 0; i < count; i++)
        {
            Image img = null;
            if (reuse != null && i < reuse.Count && reuse[i] != null)
            {
                img = reuse[i];
                img.transform.SetParent(back, false);
                img.gameObject.name = "Cloud_" + i;
            }
            else
            {
                GameObject go = new GameObject("Cloud_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = LayerMask.NameToLayer("UI");
                go.transform.SetParent(back, false);
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

    private static void ClearChildren(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
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

    private static void DestroyStandaloneYun(RectTransform canvasRt, RectTransform back, RectTransform front)
    {
        for (int i = canvasRt.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasRt.GetChild(i);
            if (child == back || child == front)
            {
                continue;
            }

            if (child.name == "yun1" || child.name == "yun2" || child.name.StartsWith("yun", StringComparison.OrdinalIgnoreCase))
            {
                if (child.parent == canvasRt)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Tiankong → CloudLayerBack → bg → Lang → CloudLayerFront → Collection/Bag/Stage/Shop…  
    /// 近景大云盖住海/浪，但不挡主按钮。
    /// </summary>
    private static void PlaceCloudLayers(RectTransform canvasRt, RectTransform back, RectTransform front)
    {
        Transform tiankong = canvasRt.Find("Tiankong");
        Transform bg = canvasRt.Find("bg");
        Transform lang = canvasRt.Find("Lang");

        int backAt = 0;
        if (tiankong != null)
        {
            backAt = tiankong.GetSiblingIndex() + 1;
        }
        else if (bg != null)
        {
            backAt = bg.GetSiblingIndex();
        }

        back.SetSiblingIndex(backAt);

        int frontAt;
        if (lang != null)
        {
            frontAt = lang.GetSiblingIndex() + 1;
        }
        else if (bg != null)
        {
            frontAt = bg.GetSiblingIndex() + 1;
        }
        else
        {
            frontAt = back.GetSiblingIndex() + 1;
        }

        // 若 back 插在 lang 之前导致 index 偏移，重新取一次
        if (lang != null)
        {
            frontAt = lang.GetSiblingIndex() + 1;
        }

        front.SetSiblingIndex(frontAt);
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
        SerializedProperty back = so.FindProperty("cloudRootBack");
        SerializedProperty front = so.FindProperty("cloudRootFront");
        SerializedProperty pool = so.FindProperty("cloudPool");
        SerializedProperty sprites = so.FindProperty("cloudSprites");
        bool hasBack = back != null && back.objectReferenceValue != null;
        bool hasFront = front != null && front.objectReferenceValue != null;
        return hasBack && hasFront
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
