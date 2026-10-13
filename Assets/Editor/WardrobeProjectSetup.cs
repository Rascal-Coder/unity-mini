using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Spine.Unity;

public static class WardrobeProjectSetup {
    [MenuItem("Expedition/Generate Hero Assets")]
    public static void Create() {
        AssetDatabase.Refresh();
        foreach (string path in new[] { "Assets/Hero/LC2_Hero.png", "Assets/Hero/LC2_Hero_2.png", "Assets/Hero/LC2_Hero_3.png" }) {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.maxTextureSize = 8192; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (var platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
                importer.ClearPlatformTextureSettings(platform);
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
        Directory.CreateDirectory("Assets/Generated");
        var textures = new[] { "LC2_Hero", "LC2_Hero_2", "LC2_Hero_3" }.Select(n => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Hero/" + n + ".png")).ToArray();
        var shader = Shader.Find("Spine/Skeleton");
        if (shader == null) throw new Exception("Spine/Skeleton shader missing");
        var materials = textures.Select(t => {
            var mat = new Material(shader) { name = t.name, mainTexture = t };
            // These source PNGs contain straight alpha, including RGB in transparent pixels.
            mat.SetFloat("_StraightAlphaInput", 1);
            mat.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
            var path = "Assets/Generated/" + t.name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) { EditorUtility.CopySerialized(mat, existing); UnityEngine.Object.DestroyImmediate(mat); return existing; }
            AssetDatabase.CreateAsset(mat, path); return mat;
        }).ToArray();
        var atlas = SpineAtlasAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Hero/LC2_Hero.atlas.txt"), materials, true);
        var existingAtlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>("Assets/Generated/HeroAtlas.asset");
        if (existingAtlas != null) { EditorUtility.CopySerialized(atlas, existingAtlas); UnityEngine.Object.DestroyImmediate(atlas); atlas = existingAtlas; atlas.Clear(); }
        else AssetDatabase.CreateAsset(atlas, "Assets/Generated/HeroAtlas.asset");
        var data = SkeletonDataAsset.CreateRuntimeInstance(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Hero/LC2_Hero.json"), atlas, true);
        var existingData = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/Generated/HeroSkeleton.asset");
        if (existingData != null) { EditorUtility.CopySerialized(data, existingData); UnityEngine.Object.DestroyImmediate(data); data = existingData; data.Clear(); }
        else AssetDatabase.CreateAsset(data, "Assets/Generated/HeroSkeleton.asset");
        var skeleton = data.GetSkeletonData(false);
        if (skeleton == null) throw new Exception("Skeleton failed to load");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.companyName = "mahu"; PlayerSettings.productName = "Expedition";
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/setup-result.txt", skeleton.Skins.Count + " skins; " + skeleton.Animations.Count + " animations; hero assets generated.");
    }
}
