// Put the whole Padlock_5Dial_1to9 folder inside Assets, then run the menu below.
// This editor utility assigns materials; it does not change game input or puzzle logic.
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PadlockSetup
{
    const string ModelName = "Padlock_5Dial_1to9";

    [MenuItem("Tools/SILENT/Setup Padlock 5 Dial")]
    public static void Setup()
    {
        string script = null;
        foreach (string guid in AssetDatabase.FindAssets("PadlockSetup t:MonoScript"))
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(candidate) == "PadlockSetup.cs")
            {
                string folder = Path.GetDirectoryName(Path.GetDirectoryName(candidate)).Replace('\\', '/');
                if (File.Exists(folder + "/" + ModelName + ".fbx")) { script = candidate; break; }
            }
        }
        if (script == null) throw new InvalidOperationException("Keep PadlockSetup.cs in Editor beside the model folder.");
        string root = Path.GetDirectoryName(Path.GetDirectoryName(script)).Replace('\\', '/');
        bool urp = GraphicsSettings.currentRenderPipeline != null &&
                   GraphicsSettings.currentRenderPipeline.GetType().Name.Contains("Universal");
        if (GraphicsSettings.currentRenderPipeline != null && !urp)
            throw new InvalidOperationException("This utility supports URP and Built-in. For HDRP, assign the included maps to HDRP/Lit manually.");
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) throw new InvalidOperationException("The active pipeline's Lit shader was not found.");
        string matDir = root + "/Materials";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(root, "Materials");

        Texture2D color = ImportTexture(root + "/Textures/Color.jpg", true, false);
        Texture2D normal = ImportTexture(root + "/Textures/Normal.png", false, true);
        Texture2D metal = ImportTexture(root + "/Textures/Padlock_MetallicSmoothness.png", false, false);
        string[] names = { "tripo_mat_cf8e5b21", "Socket_Interior", "Shackle_Internal_Steel", "Dial_Ivory", "Dial_Edge_Brass", "Dial_Numbers_Black" };
        Color[] colors = {
            Color.white, new Color(.014f,.016f,.019f), new Color(.36f,.39f,.42f),
            new Color(.72f,.67f,.54f), new Color(.34f,.24f,.11f), new Color(.008f,.009f,.011f)
        };
        float[] metallic = {1f, .2f, .8f, 0f, .68f, 0f};
        float[] roughness = {.5f, .64f, .34f, .52f, .39f, .56f};
        string modelPath = root + "/" + ModelName + ".fbx";
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Model importer unavailable: " + modelPath);
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.preserveHierarchy = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        for (int i = 0; i < names.Length; i++)
        {
            string path = matDir + "/" + names[i] + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            // Blender's constant inputs are linear; Unity material color inputs use sRGB values.
            Color tint = i == 0 ? Color.white : colors[i].gamma;
            material.SetColor(urp ? "_BaseColor" : "_Color", tint);
            material.SetFloat("_Metallic", metallic[i]);
            material.SetFloat(urp ? "_Smoothness" : "_Glossiness", 1f - roughness[i]);
            material.SetTexture(urp ? "_BaseMap" : "_MainTex", i == 0 ? color : null);
            material.SetTexture("_BumpMap", i == 0 ? normal : null);
            material.SetTexture("_MetallicGlossMap", i == 0 ? metal : null);
            if (i == 0)
            {
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
                material.SetFloat("_BumpScale", 1f);
                material.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
                if (material.HasProperty("_SmoothnessTextureChannel")) material.SetFloat("_SmoothnessTextureChannel", 0f);
                material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
                material.DisableKeyword("_METALLICSPECGLOSSMAP");
                material.DisableKeyword("_METALLICGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), names[i]), material);
        }
        AssetDatabase.SaveAssets();
        importer.SaveAndReimport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        if (filters.Length != 7) throw new InvalidOperationException("Expected 7 meshes; found " + filters.Length);
        foreach (string n in new[] { "Body", "Dial_01", "Dial_02", "Dial_03", "Dial_04", "Dial_05", "Shackle" })
        {
            int count = 0;
            foreach (MeshFilter f in filters) if (f.name == n && f.sharedMesh != null) count++;
            if (count != 1) throw new InvalidOperationException("Expected one independent mesh named " + n);
        }
        Selection.activeObject = model;
        EditorGUIUtility.PingObject(model);
        Debug.Log("Padlock ready: 7 meshes, 5 independent 40-degree dials, independent shackle. Original textures assigned.");
    }

    static Texture2D ImportTexture(string path, bool srgb, bool normal)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException("Missing texture", path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.convertToNormalmap = false;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 4;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.isReadable = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
#endif
