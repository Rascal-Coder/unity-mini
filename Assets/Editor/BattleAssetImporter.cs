using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

public sealed class BattleAssetImporter : AssetPostprocessor {
    public override uint GetVersion(){return 8;}
    [Serializable] sealed class SliceFile { public Slice[] sprites; }
    [Serializable] sealed class Slice { public string name; public int x,y,width,height; public float pivotX,pivotY; }
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith("Assets/Resources/Battle/Art/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0);settings.spriteMeshType=SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);importer.spritePixelsPerUnit=100;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.sRGBTexture=true;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
        importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
        foreach(string platform in new[]{"Standalone","Android","iPhone","WebGL"})importer.ClearPlatformTextureSettings(platform);
        if(assetPath.EndsWith("ContactShadow.png")){settings.spritePivot=new Vector2(.5f,.5f);importer.SetTextureSettings(settings);}
        if(assetPath.Contains("/Maps/")||assetPath.Contains("/Forest/")||assetPath.Contains("/Enchanted/")||assetPath.Contains("/Layered/")||assetPath.Contains("/Handpainted/")) {
            importer.maxTextureSize=2048;importer.isReadable=false;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.compressionQuality=100;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name="Standalone",overridden=true,maxTextureSize=2048,format=TextureImporterFormat.BC7,compressionQuality=100 });
            foreach(string platform in new[]{"Android","iPhone"})
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name=platform,overridden=true,maxTextureSize=2048,format=TextureImporterFormat.ASTC_6x6,compressionQuality=100 });
            string slicePath=Path.ChangeExtension(assetPath,"slices.json");
            if(File.Exists(slicePath)) {
                var data=JsonUtility.FromJson<SliceFile>(File.ReadAllText(slicePath));
                importer.spriteImportMode=SpriteImportMode.Multiple;
#pragma warning disable 0618
                importer.spritesheet=data.sprites.Select(s=>new SpriteMetaData { name=s.name,rect=new Rect(s.x,s.y,s.width,s.height),alignment=(int)SpriteAlignment.Custom,pivot=new Vector2(s.pivotX,s.pivotY) }).ToArray();
#pragma warning restore 0618
            }
        }
    }
    void OnPreprocessAudio() {
        if(!assetPath.StartsWith("Assets/Resources/Battle/Audio/"))return;
        var importer=(AudioImporter)assetImporter;importer.forceToMono=true;
        var settings=importer.defaultSampleSettings;settings.preloadAudioData=true;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;
    }
}
