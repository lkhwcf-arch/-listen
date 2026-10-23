using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SILENTConsoleDoorSetup
{
    static bool building;
    static string root;
    static readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    static string Root
    {
        get
        {
            if (!String.IsNullOrEmpty(root) && File.Exists(root + "/Console_Manifest.json"))
                return root;
            var candidates = AssetDatabase.FindAssets("SILENTConsoleDoorSetup t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileName(p) == "SILENTConsoleDoorSetup.cs")
                .Select(p => Path.GetDirectoryName(Path.GetDirectoryName(p)).Replace('\\', '/'))
                .Where(p => File.Exists(p + "/Console_Manifest.json")).Distinct().ToArray();
            if (candidates.Length != 1)
                throw new Exception("Console: put this setup script in the model's Editor folder, beside Console_Manifest.json.");
            return root = candidates[0];
        }
    }
    [InitializeOnLoadMethod]
    static void Schedule()
    {
        EditorApplication.delayCall += AutoSetup;
    }
    static void AutoSetup()
    {
        if (Application.isBatchMode || building)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoSetup;
            return;
        }
        try
        {
            if (File.Exists(Root + "/Prefabs/Console_Door_Manual.prefab"))
                return;
            if (!File.Exists(Root + "/Textures/Console_MetallicSmoothness.png") || !File.Exists(Root + "/Models/Console_Door_Animated.fbx"))
                return;
            Build();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Console setup: " + e.Message);
        }
    }
    static Texture2D Texture(string name, bool color, bool normal = false)
    {
        string path = Root + "/Textures/" + name;
        if (textureCache.TryGetValue(path, out var cached))
            return cached;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new Exception("Missing console texture: " + path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = color;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
        return textureCache[path] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Material Material(string name, Shader shader)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.name = name;
        material.shader = shader;
        material.shaderKeywords = Array.Empty<string>();
        return material;
    }
    [MenuItem("Tools/SILENT/Setup Console Door")]
    public static void Build()
    {
        if (building)
            return;
        building = true;
        try
        {
            textureCache.Clear();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            if (urp && GraphicsSettings.currentRenderPipeline.GetType().Name != "UniversalRenderPipelineAsset")
                throw new Exception("Console setup supports URP and Built-in. HDRP materials need manual setup.");
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (shader == null)
                throw new Exception("Console material shader unavailable.");
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            var surface = Material("Console_Surface", shader);
            string mainTexture = urp ? "_BaseMap" : "_MainTex", mainColor = urp ? "_BaseColor" : "_Color";
            surface.SetColor(mainColor, Color.white);
            surface.SetTexture(mainTexture, Texture("Console_BaseColor.jpg", true));
            surface.SetTextureScale(mainTexture, Vector2.one);
            surface.SetTextureOffset(mainTexture, Vector2.zero);
            surface.SetTexture("_BumpMap", Texture("Console_Normal.png", false, true));
            surface.SetFloat("_BumpScale", 1);
            surface.EnableKeyword("_NORMALMAP");
            surface.SetTexture("_MetallicGlossMap", Texture("Console_MetallicSmoothness.png", false));
            surface.EnableKeyword("_METALLICSPECGLOSSMAP");
            surface.SetFloat("_Metallic", 1);
            surface.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1);
            if (urp)
            {
                surface.SetFloat("_WorkflowMode", 1);
                surface.SetFloat("_SmoothnessTextureChannel", 0);
            }
            var cut = Material("Console_CutMetal", shader);
            cut.SetColor(mainColor, new Color(.12f, .085f, .045f, 1).gamma);
            cut.SetFloat("_Metallic", .75f);
            cut.SetFloat(urp ? "_Smoothness" : "_Glossiness", .45f);
            EditorUtility.SetDirty(surface);
            EditorUtility.SetDirty(cut);
            AssetDatabase.SaveAssets();
            Quaternion closedRotation = Quaternion.identity, openRotation = Quaternion.identity;
            foreach (string filename in new[] { "Console_Door_Animated", "Console_Door_Manual" })
            {
                string path = Root + "/Models/" + filename + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    throw new Exception("Missing model: " + path);
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = filename.EndsWith("Animated");
                importer.animationType = filename.EndsWith("Animated") ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.weldVertices = false;
                importer.preserveHierarchy = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Console_Surface"), surface);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Console_CutMetal"), cut);
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filters = model.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length != 2 || !filters.Any(f => f.name == "Console_Door") || !filters.Any(f => f.name == "Console_Body"))
                    throw new Exception("Console must contain one body mesh and one independent door mesh.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    var doorTransform = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "Console_Door");
                    if (filename.EndsWith("Animated"))
                    {
                        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == "Open" || c.name.EndsWith("|Open"));
                        if (clip == null)
                            throw new Exception("Console Open animation clip was not imported.");
                        clip.SampleAnimation(instance, 0);
                        closedRotation = doorTransform.localRotation;
                        clip.SampleAnimation(instance, clip.length);
                        openRotation = doorTransform.localRotation;
                        if (Mathf.Abs(Quaternion.Angle(closedRotation, openRotation) - 126) > 0.1f)
                            throw new Exception("Console door animation must rotate 126 degrees.");
                        clip.SampleAnimation(instance, 0);
                    }
                    else
                        instance.AddComponent<SILENTConsoleDoor>().Configure(doorTransform, closedRotation, openRotation);
                    PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Prefabs/" + filename + ".prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CONSOLE_DOOR_SETUP_OK: body + separate hinged door; Manual and Animated prefabs ready.");
        }
        finally
        {
            building = false;
        }
    }
}
