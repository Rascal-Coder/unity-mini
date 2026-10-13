using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spine;
using Spine.Unity;

public static class ForestStageValidation {
    [InitializeOnLoadMethod]
    static void CheckRequest(){EditorApplication.delayCall+=RunRequested;}
    static void RunRequested(){
        const string request="Temp/forest-preview.request";
        if(!File.Exists(request))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=RunRequested;return;}
        File.Delete(request);
        try{Run();}catch(Exception error){Directory.CreateDirectory("Docs/Previews");File.WriteAllText("Docs/Previews/forest-validation.txt",error.ToString());Debug.LogException(error);}
    }
    [MenuItem("Expedition/Validate Forest Preview")]
    public static void Run() {
        if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("Exit Play mode before forest preview validation.");return;}
        BattleProjectSetup.BakeStage();
        var preview=EditorSceneManager.NewPreviewScene();
        try {
            var root=new GameObject("Forest validation only");SceneManager.MoveGameObjectToScene(root,preview);
            var stage=root.AddComponent<BattleStage>();stage.Build();
            foreach(var sr in stage.GetComponentsInChildren<SpriteRenderer>())if(sr.sprite==null)throw new Exception("Missing sprite: "+sr.name);
            if(stage.allySlots==null||stage.allySlots.Length!=8||stage.enemySlots.Length!=8)throw new Exception("Formation slots missing");
            var cameraObject=new GameObject("Preview camera");SceneManager.MoveGameObjectToScene(cameraObject,preview);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=preview;camera.orthographic=true;camera.orthographicSize=3.2f;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.2f,.19f);
            var hero=Spawn(stage.heroPoint,0,2.4f,false);
            Directory.CreateDirectory("Docs/Previews");Render(camera,"Docs/Previews/forest-stage.png");
            stage.heroPoint.gameObject.SetActive(false);stage.dummyPoint.gameObject.SetActive(false);
            Vector2[] feet={new Vector2(-5.5f,-.3f),new Vector2(-3.1f,-1.15f),new Vector2(-5.2f,-2.05f),new Vector2(5.5f,-.3f),new Vector2(3.1f,-1.15f),new Vector2(5.2f,-2.05f)};
            for(int i=0;i<feet.Length;i++) {
                var anchor=new GameObject("Formation preview "+i).transform;anchor.SetParent(root.transform,false);anchor.localPosition=feet[i];
                Spawn(anchor,i%3,1.75f,i>=3);stage.AddContactShadow(anchor,1.1f);
            }
            Render(camera,"Docs/Previews/forest-formation.png");
            File.WriteAllText("Docs/Previews/forest-validation.txt","PASS: sprite sheet imports, scene bake, 16 formation anchors, actual engine renders at 1920x572; single hero and six-actor layout.\n");
            Debug.Log("FOREST_VALIDATION_PASS");
        } finally {EditorSceneManager.ClosePreviewScene(preview);}
    }
    static SkeletonAnimation Spawn(Transform anchor,int role,float targetHeight,bool flip) {
        anchor.gameObject.AddComponent<BattleDepth>();
        var asset=AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/Generated/HeroSkeleton.asset");
        var hero=SkeletonAnimation.NewSkeletonAnimationGameObject(asset);hero.transform.SetParent(anchor,false);hero.Initialize(false);
        var data=hero.Skeleton.Data;var profession=HeroProfession.All[new[]{0,4,6}[role]];
        var selected=new System.Collections.Generic.SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var skin in data.Skins.OrderBy(s=>s.Name,StringComparer.Ordinal)) {
            if(!skin.Name.StartsWith("001_appearance/",StringComparison.Ordinal))continue;
            string key=skin.Name.Substring(0,skin.Name.LastIndexOf('/'));if(!selected.ContainsKey(key))selected[key]=skin.Name;
        }
        selected["001_appearance/head"]="001_appearance/head/head_001";
        selected["001_appearance/dress/headwear"]="001_appearance/dress/headwear/headwear_000";
        selected["001_appearance/dress/Tail"]="001_appearance/dress/Tail/Tail_000";
        selected["001_appearance/head_hair"]="001_appearance/head_hair/"+profession.hair;
        selected["001_appearance/dress/backpack"]="001_appearance/dress/backpack/"+profession.backpack;
        selected["001_appearance/dress/eyewear"]="001_appearance/dress/eyewear/"+profession.eyewear;
        selected["002A_armor"]="002A_armor/"+profession.armor;selected["003_weapon"]="003_weapon/"+profession.weapon;
        var combined=new Skin("preview");if(data.DefaultSkin!=null)combined.AddSkin(data.DefaultSkin);
        foreach(var name in selected.Values){var skin=data.FindSkin(name);if(skin!=null)combined.AddSkin(skin);}
        hero.Skeleton.SetSkin(combined);hero.Skeleton.SetToSetupPose();hero.Skeleton.ScaleX=flip?1:-1;
        var idle=data.Animations.FirstOrDefault(a=>a.Name.StartsWith(profession.animationGroup+"/001A_idle",StringComparison.Ordinal));
        if(idle!=null)hero.AnimationState.SetAnimation(0,idle,false);hero.Update(0);hero.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        float[] vertices=null;float x,y,w,h;hero.Skeleton.GetBounds(out x,out y,out w,out h,ref vertices);
        float scale=targetHeight/h;hero.transform.localScale=Vector3.one*scale;hero.transform.localPosition=new Vector3(0,-y*scale,0);hero.LateUpdate();return hero;
    }
    static void Render(Camera camera,string path) {
        var target=new RenderTexture(1920,572,24,RenderTextureFormat.ARGB32);var prior=RenderTexture.active;
        camera.targetTexture=target;camera.aspect=1920f/572;camera.Render();RenderTexture.active=target;
        var pixels=new Texture2D(1920,572,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1920,572),0,0);pixels.Apply();
        File.WriteAllBytes(path,pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=prior;
        UnityEngine.Object.DestroyImmediate(pixels);target.Release();UnityEngine.Object.DestroyImmediate(target);
    }
}
