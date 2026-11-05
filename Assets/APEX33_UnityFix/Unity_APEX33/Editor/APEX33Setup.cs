using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.IO;

// Run explicitly from the menu after copying Unity_APEX33 into Assets.
public static class APEX33Setup
{
    [MenuItem("Tools/APEX33/Create Material and Prefab")]
    public static void Build()
    {
        string source = null;
        foreach (string guid in AssetDatabase.FindAssets("APEX33 t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path) == "APEX33.fbx" && File.Exists(Path.Combine(Path.GetDirectoryName(path), "Textures/Slick33_BaseColor.png"))) { source = path; break; }
        }
        if (source == null) { Debug.LogError("APEX33.fbx was not found in Assets."); return; }
        var pipeline = GraphicsSettings.currentRenderPipeline;
        bool urp = pipeline != null && pipeline.GetType().Name.Contains("Universal");
        if (pipeline != null && !urp)
        {
            EditorUtility.DisplayDialog("APEX33", "This setup supports URP and Built-in. For HDRP, use the supplied roughness and metallic maps to build an HDRP mask map.", "OK");
            return;
        }
        string folder = Path.GetDirectoryName(source).Replace('\\', '/');
        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(source);
        modelImporter.importCameras = false;
        modelImporter.importLights = false;
        modelImporter.importAnimation = false;
        modelImporter.importNormals = ModelImporterNormals.Import;
        modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
        modelImporter.bakeAxisConversion = true;
        modelImporter.isReadable = false;

        var mat = CreateMaterial(folder, "APEX33", "APEX33_Game", urp);
        var slick = CreateMaterial(folder, "Slick33", "APEX33_Slick", urp);
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "APEX33_Game"), mat);
        modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "APEX33_Slick"), slick);
        modelImporter.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            instance.name = "APEX33";
            PrefabUtility.SaveAsPrefabAsset(instance, folder + "/APEX33.prefab");
        }
        finally { Object.DestroyImmediate(instance); }
        AssetDatabase.SaveAssets();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/APEX33.prefab");
        Debug.Log("APEX33 material and prefab created. Physics and suspension behaviour are configured separately.");
    }
    static Material CreateMaterial(string folder, string prefix, string materialName, bool urp)
    {
        var color = LoadTexture(folder, prefix, "BaseColor", true, false);
        var normal = LoadTexture(folder, prefix, "Normal", false, true);
        var metal = LoadTexture(folder, prefix, "MetallicSmoothness", false, false);
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) { throw new System.InvalidOperationException("Required shader was not found."); }
        string materialPath = folder + "/" + materialName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, materialPath); }
        else mat.shader = shader;
        mat.SetTexture(urp ? "_BaseMap" : "_MainTex", color);
        mat.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", metal);
        mat.SetFloat("_Metallic", 1f);
        if (urp)
        {
            mat.SetFloat("_WorkflowMode", 1f);
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        else
        {
            mat.SetFloat("_GlossMapScale", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
    static Texture2D LoadTexture(string folder, string prefix, string suffix, bool srgb, bool normal)
    {
        string path = folder + "/Textures/" + prefix + "_" + suffix + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new FileNotFoundException(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.maxTextureSize = 4096;
        importer.mipmapEnabled = true;
        importer.wrapMode = prefix == "Slick33" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
