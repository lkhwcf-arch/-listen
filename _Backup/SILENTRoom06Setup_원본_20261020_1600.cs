using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class SILENTRoom06Setup
{
    const string Root = "Assets/SILENT_Room_06";
    static bool isBuilding;
    [InitializeOnLoadMethod]
    public static void ScheduleSetup()
    {
        EditorApplication.delayCall -= SetupIfNeeded;
        EditorApplication.delayCall += SetupIfNeeded;
    }
    static void SetupIfNeeded()
    {
        if(isBuilding) return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += SetupIfNeeded; return; }
        if(!File.Exists(Root+"/Room06_MaterialManifest.json") || File.Exists(Root+"/Prefabs/SILENT_Room_06.prefab")) return;
        var ready=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/Room06_MaterialManifest.json"));
        if(ready==null || ready.files==null || ready.files.Any(f=>!File.Exists(Root+"/"+f.path))) return;
        if(Shader.Find("Universal Render Pipeline/Lit")==null) { Debug.LogWarning("Room 06 requires URP. After installing URP, run Tools > SILENT > Setup Room 06 (URP)."); return; }
        try { Build(); } catch(Exception e) { Debug.LogException(e); }
    }
    [Serializable] public class MaterialData
    {
        public string name, file_name, base_map, metallic_smoothness_map, normal_map, height_map;
        public float[] base_color, emission_color;
        public bool alpha_clip, double_sided, unlit;
        public float metallic, smoothness, normal_scale, height_scale, specular;
    }
    [Serializable] public class FileData { public string path; }
    [Serializable] public class MeshData { public string name, parent; public int polygons; }
    [Serializable] public class Manifest { public MaterialData[] materials; public string[] model_files; public FileData[] files; public MeshData[] meshes, tv_markers; }
    static readonly Dictionary<string,Texture2D> textureCache=new Dictionary<string,Texture2D>();
    [Serializable] public class Audit
    {
        public int meshRenderers, meshFilters, materials, lights, cameras, triangles, missingMaterials, missingUVs, hierarchyErrors, tvMarkers;
        public bool allShadersSupported;
        public string unityVersion, renderPipeline;
        public string[] rendererNames;
        public Vector3 boundsCenter, boundsSize;
    }
    static Color ColorValue(float[] v, bool gamma = true)
    {
        Color c = new Color(v[0], v[1], v[2], v.Length > 3 ? v[3] : 1);
        return gamma ? c.gamma : c;
    }
    static Texture2D Texture(string filename, bool srgb, bool normal=false)
    {
        if (String.IsNullOrEmpty(filename)) return null;
        string path=Root+"/Textures/"+filename;
        if(textureCache.TryGetValue(path,out var cached)) return cached;
        var imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp==null) throw new Exception("Missing texture: "+path);
        imp.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        imp.sRGBTexture=srgb;
        imp.alphaSource=TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency=false;
        imp.maxTextureSize=4096;imp.npotScale=TextureImporterNPOTScale.None;
        imp.textureCompression=TextureImporterCompression.Uncompressed;
        imp.mipmapEnabled=true;imp.anisoLevel=4;imp.wrapMode=TextureWrapMode.Repeat;
        imp.isReadable=false;imp.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);textureCache[path]=texture;return texture;
    }
    [MenuItem("Tools/SILENT/Setup Room 06 (URP)")]
    public static void Build()
    {
        if(isBuilding) return;
        isBuilding=true;
        try { BuildInternal(); }
        finally { isBuilding=false; }
    }
    static void BuildInternal()
    {
        textureCache.Clear();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");
        var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/Room06_MaterialManifest.json"));
        foreach(var file in data.files) if(!File.Exists(Root+"/"+file.path)) throw new Exception("Copy all Room 06 package files first: "+file.path);
        var shader=Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new Exception("URP Lit unavailable");
        var unlitShader=Shader.Find("Universal Render Pipeline/Unlit");
        if(unlitShader==null)throw new Exception("URP Unlit unavailable");
        var mats=new Dictionary<string,Material>();
        foreach(var d in data.materials)
        {
            string path=Root+"/Materials/"+d.file_name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.name=d.name;mat.shader=shader;mat.shaderKeywords=Array.Empty<string>();
            if(d.unlit)
            {
                mat.shader=unlitShader;
                mat.SetColor("_BaseColor",ColorValue(d.emission_color));mat.SetTexture("_BaseMap",null);
                mat.SetFloat("_Surface",0);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_Cull",d.double_sided?0:2);mat.SetFloat("_ZWrite",1);
                mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.renderQueue=(int)RenderQueue.Geometry;mat.SetOverrideTag("RenderType","Opaque");
                mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;
                EditorUtility.SetDirty(mat);mats.Add(d.name,mat);continue;
            }
            mat.SetFloat("_WorkflowMode",1);mat.SetColor("_BaseColor",ColorValue(d.base_color));
            mat.SetTexture("_BaseMap",Texture(d.base_map,true));
            mat.SetFloat("_Metallic",d.metallic);mat.SetFloat("_Smoothness",d.smoothness);
            var metallic=Texture(d.metallic_smoothness_map,false);
            mat.SetTexture("_MetallicGlossMap",metallic);
            if(metallic!=null){mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Metallic",1);mat.SetFloat("_Smoothness",1);}
            mat.SetFloat("_SmoothnessTextureChannel",0);
            var normal=Texture(d.normal_map,false,true);mat.SetTexture("_BumpMap",normal);mat.SetFloat("_BumpScale",d.normal_scale);
            if(normal!=null)mat.EnableKeyword("_NORMALMAP");
            var height=Texture(d.height_map,false);mat.SetTexture("_ParallaxMap",height);mat.SetFloat("_Parallax",d.height_scale);
            if(height!=null)mat.EnableKeyword("_PARALLAXMAP");
            Color emission=ColorValue(d.emission_color);
            mat.SetColor("_EmissionColor",emission);
            if(emission.maxColorComponent>0){mat.EnableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;}
            else mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.SetFloat("_Surface",0);mat.SetFloat("_Cull",d.double_sided?0:2);mat.doubleSidedGI=d.double_sided;mat.SetFloat("_ZWrite",1);
            mat.SetFloat("_AlphaClip",d.alpha_clip?1:0);mat.SetFloat("_Cutoff",0.5f);
            if(d.alpha_clip){mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=(int)RenderQueue.AlphaTest;mat.SetOverrideTag("RenderType","TransparentCutout");}
            else{mat.renderQueue=(int)RenderQueue.Geometry;mat.SetOverrideTag("RenderType","Opaque");}
            mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.SetFloat("_ReceiveShadows",1);
            mat.SetFloat("_SpecularHighlights",d.specular==0?0:1);
            if(d.specular==0)mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(mat);mats.Add(d.name,mat);
        }
        AssetDatabase.SaveAssets();
        var instance=new GameObject("SILENT_Room_06");
        try
        {
        foreach(string modelPath in data.model_files)
        {
            var importer=AssetImporter.GetAtPath(Root+"/"+modelPath) as ModelImporter;
            if(importer==null)throw new Exception("Missing model: "+modelPath);
            importer.importCameras=false;importer.importLights=false;bool puzzle=modelPath.EndsWith("_A_Puzzle.fbx",StringComparison.Ordinal);
            importer.importAnimation=puzzle;
            if(puzzle){importer.animationType=ModelImporterAnimationType.Generic;importer.animationCompression=ModelImporterAnimationCompression.Off;}
            importer.globalScale=1;importer.useFileScale=true;
            importer.importNormals=ModelImporterNormals.Import;
            importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.weldVertices=false;importer.preserveHierarchy=true;importer.optimizeGameObjects=false;
            importer.indexFormat=ModelImporterIndexFormat.UInt32;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var kv in mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),kv.Key),kv.Value);
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+modelPath);
            var child=(GameObject)PrefabUtility.InstantiatePrefab(model);
            child.transform.SetParent(instance.transform,false);
        }
        var renderers=instance.GetComponentsInChildren<MeshRenderer>(true);
        var meshes=instance.GetComponentsInChildren<MeshFilter>(true);
        var audit=new Audit{meshRenderers=renderers.Length,meshFilters=meshes.Length,materials=mats.Count,lights=instance.GetComponentsInChildren<Light>(true).Length,cameras=instance.GetComponentsInChildren<Camera>(true).Length,allShadersSupported=mats.Values.All(m=>m.shader.isSupported),unityVersion=Application.unityVersion,renderPipeline=GraphicsSettings.currentRenderPipeline?.GetType().Name,rendererNames=renderers.Select(r=>r.name).ToArray()};
        Bounds bounds=renderers[0].bounds;
        foreach(var r in renderers){bounds.Encapsulate(r.bounds);audit.missingMaterials+=r.sharedMaterials.Count(m=>m==null||!mats.Values.Contains(m));}
        foreach(var f in meshes){audit.triangles+=f.sharedMesh.triangles.Length/3;if(f.sharedMesh.uv.Length==0&&f.GetComponent<MeshRenderer>().sharedMaterials.Any(m=>m.mainTexture!=null||(m.HasProperty("_BumpMap")&&m.GetTexture("_BumpMap")!=null)||(m.HasProperty("_MetallicGlossMap")&&m.GetTexture("_MetallicGlossMap")!=null)||(m.HasProperty("_ParallaxMap")&&m.GetTexture("_ParallaxMap")!=null)))audit.missingUVs++;}
        foreach(var expected in data.meshes)
        {
            var f=meshes.FirstOrDefault(x=>x.name==expected.name);
            if(f==null) { audit.hierarchyErrors++; continue; }
            if(!String.IsNullOrEmpty(expected.parent))
            {
                if(f.transform.parent==null || f.transform.parent.name!=expected.parent) audit.hierarchyErrors++;

            }
        }
        var transforms=instance.GetComponentsInChildren<Transform>(true);
        foreach(var expected in data.tv_markers)
        {
            var tr=transforms.FirstOrDefault(x=>x.name==expected.name);
            if(tr==null || tr.GetComponent<MeshFilter>()!=null || tr.parent==null || tr.parent.name!=expected.parent) audit.hierarchyErrors++;
            else audit.tvMarkers++;
        }
        audit.boundsCenter=bounds.center;audit.boundsSize=bounds.size;
        if(audit.meshFilters!=data.meshes.Length||audit.meshRenderers!=data.meshes.Length||audit.materials!=data.materials.Length||audit.triangles!=data.meshes.Sum(x=>x.polygons)||audit.hierarchyErrors!=0||audit.tvMarkers!=data.tv_markers.Length||audit.lights!=0||audit.cameras!=0||audit.missingMaterials!=0||audit.missingUVs!=0)throw new Exception("Room audit failed: "+JsonUtility.ToJson(audit));
        PrefabUtility.SaveAsPrefabAsset(instance,Root+"/Prefabs/SILENT_Room_06.prefab");

        AssetDatabase.SaveAssets();
        File.WriteAllText(Root+"/Room06_UnityValidation.json",JsonUtility.ToJson(audit,true));
        Debug.Log("ROOM06_BUILD_OK "+JsonUtility.ToJson(audit));
        }
        finally {UnityEngine.Object.DestroyImmediate(instance);}
    }
}

public class SILENTRoom06ImportWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if(imported.Concat(moved).Any(p=>p=="Assets/SILENT_Room_06/Room06_MaterialManifest.json" || p.StartsWith("Assets/SILENT_Room_06/Models/",StringComparison.Ordinal) || p.StartsWith("Assets/SILENT_Room_06/Textures/",StringComparison.Ordinal)))
            SILENTRoom06Setup.ScheduleSetup();
    }
}
