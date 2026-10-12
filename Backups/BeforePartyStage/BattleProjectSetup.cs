using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Spine.Unity;

public static class BattleProjectSetup {
    const string ScenePath="Assets/Scenes/DiceTraining.unity";
    const string MaterialPath="Assets/Resources/Battle/SpriteMaterial.mat";
    [InitializeOnLoadMethod]
    static void QueuePreparation(){EditorApplication.delayCall+=Prepare;}
    static void Prepare() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Prepare;return;}
        if(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)==null){var shader=Shader.Find("Sprites/Default");if(shader!=null)AssetDatabase.CreateAsset(new Material(shader),MaterialPath);}
        // Bake individual props once, preserving any later artist edits to this scene.
        if(!File.Exists(ScenePath)||!File.ReadAllText(ScenePath).Contains("layoutVersion: "+BattleStage.CurrentLayoutVersion))BakeStage();
    }
    [MenuItem("Dice Hero/Open Dice Training")]
    public static void Open(){Prepare();if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
    [MenuItem("Dice Hero/Bake Training Stage If Missing")]
    public static void BakeStage() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(Resources.LoadAll<Sprite>("Battle/Art/Enchanted/Tileset").Length!=6||Resources.Load<Sprite>("Battle/Art/Enchanted/Background")==null){Debug.LogWarning("Forest sprites are importing. Use Dice Hero/Bake Training Stage If Missing after import completes.");return;}
        const string backup="Backups/DiceTraining-before-forest.unity";
        if(File.Exists(ScenePath)&&!File.Exists(backup)){Directory.CreateDirectory("Backups");File.Copy(ScenePath,backup);}
        var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ScenePath);bool wasLoaded=scene.IsValid()&&scene.isLoaded;
        if(wasLoaded&&scene.isDirty){Debug.LogWarning("Save DiceTraining before baking the stage.");return;}
        if(!wasLoaded)scene=File.Exists(ScenePath)?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try {
            var game=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<DiceBattle>(true)).FirstOrDefault();
            if(game==null)game=new GameObject("Dice Battle").AddComponent<DiceBattle>();
            if(game.dataAsset==null)game.dataAsset=AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/Generated/HeroSkeleton.asset");
            if(game.stage==null){var root=new GameObject("Layered training ground");root.transform.SetParent(game.transform,false);game.stage=root.AddComponent<BattleStage>();game.stage.Build();}
            else game.stage.Build();
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).FirstOrDefault();
            if(camera==null){camera=new GameObject("Battle Camera").AddComponent<Camera>();camera.tag="MainCamera";}
            camera.orthographic=true;camera.orthographicSize=3.2f;camera.transform.position=new Vector3(0,0,-10);camera.rect=new Rect(0,.335f,1,.53f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            var other=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();other.Insert(0,new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=other.ToArray();
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            AssetDatabase.SaveAssets();
        } finally {if(prior.IsValid()&&prior.isLoaded)SceneManager.SetActiveScene(prior);if(!wasLoaded)EditorSceneManager.CloseScene(scene,true);}
    }
}

