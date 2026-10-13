using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Spine.Unity;

// Runs against an isolated project copy. Produces real renderer captures, never mockups.
public static class PartyStageValidation {
    const string Report = "Docs/Previews/party-validation.txt";
    const string RunningKey = "PartyStageValidation.Running";
    static double started;
    static int phase;
    static int damageBeforePause;
    static Vector3 cameraBeforePause;
    static int initialWave;
    static readonly System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();

    [MenuItem("Expedition/Validate Four Layer Party Stage")]
    public static void Run() {
        try {
            Directory.CreateDirectory("Docs/Previews");
            AssetDatabase.Refresh();
            BattleProjectSetup.BakeStage();
            RenderStatic();
            EditorSceneManager.OpenScene("Assets/Scenes/Battle.unity");
            File.WriteAllText(Report, "Static PASS: 4 groups; 16 anchors; rear props behind terrain; 3 Spine heroes; parallax coefficients verified.\nGrounding PASS: boot attachments stay on the floor across idle, run, attack and skill cycles for all 3 roles; shadows remain behind actors.\n");
            SessionState.SetBool(RunningKey, true);
            EditorApplication.isPlaying = true;
        } catch (Exception error) { Fail(error); }
    }
    [InitializeOnLoadMethod]
    static void Resume() {
        if (!SessionState.GetBool(RunningKey, false)) return;
        started = EditorApplication.timeSinceStartup; phase = 0;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }
    static void OnLog(string message, string stack, LogType type) {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
    }
    static void Tick() {
        try {
            if (!EditorApplication.isPlaying) return;
            var game = UnityEngine.Object.FindObjectOfType<PartyBattle>();
            if (game == null || !game.IsReady) {
                if (EditorApplication.timeSinceStartup - started > 40) throw new Exception("Party runtime did not initialize.");
                return;
            }
            double time = EditorApplication.timeSinceStartup - started;
            if (phase == 0) {
                Require(game.PartyCount == 3, "Expected three party members.");
                Require(game.Hud.EnergyBars.Length == 3, "Expected three energy bars.");
                Require(!game.Hud.GetComponentsInChildren<Text>().Any(t => t.text.Contains("骰")), "Legacy dice UI is still active.");
                RenderRuntime(game, 1920, 1080, "party-runtime-1920x1080.png");
                initialWave = game.Wave; phase = 1;
            }
            if (phase == 1 && time > 10) {
                Require(game.HitCounts.All(n => n > 0), "Every member must attack automatically.");
                Require(game.TotalDamage > 0, "Damage was not applied.");
                game.Hud.PauseButton.onClick.Invoke();
                Require(game.IsPaused && Time.timeScale == 0, "Pause button did not freeze game time.");
                damageBeforePause = game.TotalDamage; cameraBeforePause = Camera.main.transform.position; phase = 2; started = EditorApplication.timeSinceStartup;
            } else if (phase == 2 && time > 1) {
                Require(game.TotalDamage == damageBeforePause, "Damage advanced while paused.");
                Require(Camera.main.transform.position == cameraBeforePause, "Camera moved while paused.");
                game.TogglePause(); game.Hud.SpeedButton.onClick.Invoke();
                Require(Time.timeScale == 2, "Speed button did not apply x2.");
                phase = 3; started = EditorApplication.timeSinceStartup;
            } else if (phase == 3 && game.Wave > initialWave) {
                RenderRuntime(game, 2400, 1080, "party-runtime-2400x1080.png");
                RenderRuntime(game, 1600, 1200, "party-runtime-1600x1200.png");
                game.Hud.RestartButton.onClick.Invoke();
                Require(game.Wave == 1 && game.TargetHealth == 720 && game.TotalDamage == 0, "Restart did not reset training.");
                Require(game.HitCounts.All(n => n == 0) && game.Energy.All(n => n == 0), "Restart did not reset member state.");
                Require(!game.IsPaused, "Restart left game paused.");
                if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
                File.AppendAllText(Report, "Runtime PASS: three independent attackers; target damage; automatic next wave; pause freezes combat/camera; x2; restart; no dice UI; no runtime errors.\nCaptured 1920x1080, 2400x1080, 1600x1200 using actual camera and HUD.\n");
                Finish(true);
            } else if (phase == 3 && time > 70) throw new Exception("Automatic wave transition timed out.");
        } catch (Exception error) { Fail(error); }
    }
    static void RenderStatic() {
        var preview = EditorSceneManager.NewPreviewScene();
        try {
            var root = new GameObject("Party validation only"); SceneManager.MoveGameObjectToScene(root, preview);
            var stage = root.AddComponent<BattleStage>(); stage.Build();
            Require(root.transform.childCount == 4, "Stage must have exactly four depth groups.");
            Require(stage.allySlots.Length == 8 && stage.enemySlots.Length == 8, "Formation anchors missing.");
            Require(stage.GetComponentsInChildren<SpriteRenderer>().All(s => s.sprite != null), "Missing scene sprite.");
            var terrain = stage.near.Find("Solid terrace - broad top and deep rock face").GetComponent<SpriteRenderer>();
            foreach (var prop in stage.near.Find("Rear decorations - bases hidden by terrain").GetComponentsInChildren<SpriteRenderer>()) Require(prop.sortingOrder < terrain.sortingOrder, "Rear props not occluded by terrain.");
            var asset = AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>("Assets/Generated/HeroSkeleton.asset");
            for (int i = 0; i < 3; i++) {
                var foot = i == 0 ? stage.heroPoint : BattleStage.Anchor(stage.heroPoint.parent, "Preview ally " + i, stage.allySlots[i].localPosition);
                stage.AddContactShadow(foot, .85f);
                var hero = BattleHeroVisual.Spawn(asset, foot, i);
                VerifyGrounding(hero, foot, i);
            }
            var cameraObject = new GameObject("Validation camera"); SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = preview; camera.orthographic = true; camera.orthographicSize = BattleStage.CameraHeight;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .15f, .2f);
            Render(camera, 1920, 1080, "Docs/Previews/party-stage.png");
            var actors = stage.GetComponentsInChildren<SkeletonAnimation>();
            foreach (string token in new[] { "002A_move", "003A", "004A" }) {
                for (int i = 0; i < actors.Length; i++) {
                    float duration = BattleHeroVisual.Play(actors[i], i, token, false);
                    actors[i].Update(duration * .4f); actors[i].LateUpdate();
                }
                Render(camera, 1920, 1080, "Docs/Previews/grounding-" + token + ".png");
            }
            for (int i = 0; i < actors.Length; i++) {
                BattleHeroVisual.Play(actors[i], i, "001A_idle", true); actors[i].Update(0); actors[i].LateUpdate();
            }
            var parallax = cameraObject.AddComponent<BattleParallax>(); parallax.Bind(camera, stage, stage.heroPoint);
            Vector3 origin = stage.near.position;
            camera.transform.position += Vector3.right * .8f; parallax.ApplyOffset(.8f);
            Require(stage.near.position == origin, "Near terrain should stay in world space.");
            Require(Mathf.Abs(stage.far.localPosition.x - .752f) < .001f, "Far parallax coefficient wrong.");
            Require(Mathf.Abs(stage.middle.localPosition.x - .56f) < .001f, "Middle parallax coefficient wrong.");
            Require(Mathf.Abs(stage.foreground.localPosition.x + .2f) < .001f, "Foreground parallax coefficient wrong.");
            Render(camera, 1920, 1080, "Docs/Previews/party-parallax.png"); parallax.enabled = false;
            root.SetActive(false);
            Directory.CreateDirectory("Assets/Resources/Battle/Art/Portraits");
            for (int i = 0; i < 3; i++) {
                var foot = new GameObject("Portrait").transform; SceneManager.MoveGameObjectToScene(foot.gameObject, preview);
                BattleHeroVisual.Spawn(asset, foot, i, 1.65f);
                camera.backgroundColor = new Color(.06f, .13f, .17f, 1); camera.orthographicSize = .63f;
                camera.transform.position = new Vector3(0, 1.18f, -10);
                Render(camera, 256, 256, "Assets/Resources/Battle/Art/Portraits/" + BattleHeroVisual.PortraitNames[i] + ".png");
                UnityEngine.Object.DestroyImmediate(foot.gameObject);
            }
        } finally { EditorSceneManager.ClosePreviewScene(preview); }
        AssetDatabase.Refresh();
    }
    static void RenderRuntime(PartyBattle game, int width, int height, string name) {
        var canvas = game.Hud.GetComponent<Canvas>(); var camera = Camera.main;
        var priorMode = canvas.renderMode; var priorCamera = canvas.worldCamera; float priorSize = camera.orthographicSize;
        var priorMin = game.Hud.Root.anchorMin; var priorMax = game.Hud.Root.anchorMax;
        try {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5; canvas.sortingOrder = 2000;
            game.Hud.Root.anchorMin = Vector2.zero; game.Hud.Root.anchorMax = Vector2.one;
            camera.orthographicSize = Mathf.Max(BattleStage.CameraHeight, 10.3f / (width / (float)height));
            game.stage.FrameForeground(camera.orthographicSize * (width / (float)height), camera.orthographicSize);
            Render(camera, width, height, "Docs/Previews/" + name);
        } finally { canvas.renderMode = priorMode; canvas.worldCamera = priorCamera; camera.orthographicSize = priorSize; game.Hud.Root.anchorMin = priorMin; game.Hud.Root.anchorMax = priorMax; }
    }
    static void VerifyGrounding(SkeletonAnimation hero, Transform foot, int role) {
        Vector3 anchor = foot.position;
        var grounding = hero.GetComponent<BattleGrounding>();
        foreach (string token in new[] { "001A_idle", "002A_move", "003A", "004A" }) {
            float duration = BattleHeroVisual.Play(hero, role, token, true);
            for (int frame = 0; frame < 48; frame++) {
                hero.Update(duration / 24); hero.LateUpdate();
                Require(grounding.TryGetBootBounds(hero.Skeleton.FindSlot("armor_footF"), out var front), "Front boot missing.");
                Require(grounding.TryGetBootBounds(hero.Skeleton.FindSlot("armor_footB"), out var back), "Back boot missing.");
                float sole = hero.transform.TransformPoint(new Vector3(0, Mathf.Min(front.min.y, back.min.y), 0)).y;
                Require(Mathf.Abs(sole - foot.position.y) < .02f, "Boots left the floor: " + role + " " + token);
                Require(foot.position == anchor, "Visual grounding moved the formation anchor.");
                foreach (var shadow in foot.GetComponentsInChildren<SpriteRenderer>()) {
                    Require(shadow.sortingOrder < 0, "Shadow obscures actor.");
                    Require(Mathf.Abs(shadow.transform.position.y - foot.position.y) < .03f, "Shadow left the ground plane.");
                }
            }
        }
        BattleHeroVisual.Play(hero, role, "001A_idle", true); hero.Update(0); hero.LateUpdate();
    }
    static void Render(Camera camera, int width, int height, string path) {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var priorTarget = camera.targetTexture; var priorActive = RenderTexture.active; float priorAspect = camera.aspect;
        Texture2D pixels = null;
        try {
            camera.targetTexture = target; camera.aspect = width / (float)height; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        } finally {
            camera.targetTexture = priorTarget; camera.aspect = priorAspect; RenderTexture.active = priorActive;
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Fail(Exception error) {
        Directory.CreateDirectory("Docs/Previews"); File.AppendAllText(Report, "FAIL: " + error + "\n"); Debug.LogException(error); Finish(false);
    }
    static void Finish(bool success) {
        SessionState.SetBool(RunningKey, false); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
    }
}
