using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 将项目全部 TMP 文本切换为 AlimamaFangYuanTiVF-Thin-2，加粗 + 黑色描边 0.2。
/// 菜单：Tools/Battle/Apply Alimama Font Project-Wide
/// </summary>
public static class ApplyAlimamaFont
{
    private const string TtfPath = "Assets/TextMesh Pro/Fonts/AlimamaFangYuanTiVF-Thin-2.ttf";
    private const string SdfPath = "Assets/TextMesh Pro/Fonts/AlimamaFangYuanTiVF-Thin-2 SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string OldFontGuid = "8f586378b4e144a9851e7b34d9b748ee";
    private const float OutlineWidth = 0.2f;

    [MenuItem("Tools/Battle/Apply Alimama Font Project-Wide")]
    public static void Apply()
    {
        TMP_FontAsset font = EnsureFontAsset();
        if (font == null)
        {
            Debug.LogError("[ApplyAlimamaFont] Failed to create/load TMP Font Asset.");
            return;
        }

        ApplyOutline(font.material);
        EditorUtility.SetDirty(font);
        if (font.material != null)
        {
            EditorUtility.SetDirty(font.material);
        }

        SetDefaultFont(font);
        ApplyToAllPrefabs(font);
        ApplyToAllScenes(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int left = CountOldGuidRefs();
        Debug.Log("[ApplyAlimamaFont] Done. SDF=" + SdfPath + " remaining old guid refs=" + left);
    }

    public static void ApplyFromBatch()
    {
        try
        {
            Apply();
            EditorApplication.Exit(leftOver() == 0 ? 0 : 1);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(2);
        }
    }

    private static int leftOver()
    {
        return CountOldGuidRefs();
    }

    private static TMP_FontAsset EnsureFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
        if (existing != null)
        {
            // 确保指向源 TTF（Dynamic）
            if (existing.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            }

            return existing;
        }

        Font source = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (source == null)
        {
            Debug.LogError("[ApplyAlimamaFont] Missing TTF at " + TtfPath);
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null)
        {
            return null;
        }

        fontAsset.name = Path.GetFileNameWithoutExtension(SdfPath);
        AssetDatabase.CreateAsset(fontAsset, SdfPath);

        if (fontAsset.atlasTexture != null)
        {
            fontAsset.atlasTexture.name = "AlimamaFangYuanTiVF-Thin-2 Atlas";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "AlimamaFangYuanTiVF-Thin-2 Atlas Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
    }

    private static void ApplyOutline(Material mat)
    {
        if (mat == null)
        {
            return;
        }

        mat.EnableKeyword("OUTLINE_ON");
        if (mat.HasProperty(ShaderUtilities.ID_OutlineWidth))
        {
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
        }

        if (mat.HasProperty(ShaderUtilities.ID_OutlineColor))
        {
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        }

        if (mat.HasProperty(ShaderUtilities.ID_ScaleRatio_A))
        {
            float a = mat.GetFloat(ShaderUtilities.ID_ScaleRatio_A);
            if (a <= 0.0001f)
            {
                mat.SetFloat(ShaderUtilities.ID_ScaleRatio_A, 1f);
            }
        }
    }

    private static void SetDefaultFont(TMP_FontAsset font)
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings == null)
        {
            return;
        }

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty def = so.FindProperty("m_defaultFontAsset");
        if (def != null)
        {
            def.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }

    private static void ApplyToAllPrefabs(TMP_FontAsset font)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.Contains("Examples & Extras"))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = ApplyToHierarchy(root, font);
            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ApplyToAllScenes(TMP_FontAsset font)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.Contains("Examples & Extras"))
            {
                continue;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool dirty = false;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                dirty |= ApplyToHierarchy(roots[r], font);
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
    }

    private static bool ApplyToHierarchy(GameObject root, TMP_FontAsset font)
    {
        bool dirty = false;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text tmp = texts[i];
            if (tmp == null)
            {
                continue;
            }

            // 使用共享材质（描边已写在 font.material 上），避免每个文本实例化材质
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
            tmp.fontStyle = FontStyles.Bold;
            tmp.fontWeight = FontWeight.Bold;
            EditorUtility.SetDirty(tmp);
            dirty = true;
        }

        return dirty;
    }

    private static int CountOldGuidRefs()
    {
        int count = 0;
        string[] roots = { "Assets/Prefab", "Assets/Scenes", "Assets/TextMesh Pro/Resources" };
        for (int r = 0; r < roots.Length; r++)
        {
            if (!Directory.Exists(roots[r]))
            {
                continue;
            }

            string[] files = Directory.GetFiles(roots[r], "*.*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (!(path.EndsWith(".prefab") || path.EndsWith(".unity") || path.EndsWith(".asset")))
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                int idx = 0;
                while ((idx = text.IndexOf(OldFontGuid, idx)) >= 0)
                {
                    count++;
                    idx += OldFontGuid.Length;
                }
            }
        }

        return count;
    }
}