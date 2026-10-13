using System.Collections;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;

// The class/GUID remains stable so existing Battle scene references migrate in place.
public sealed class PartyBattle : MonoBehaviour {
    public SkeletonDataAsset dataAsset;
    public BattleStage stage;
    public bool autoStart = true;
    public BattleHud Hud { get; private set; }
    public int Wave { get; private set; } = 1;
    public int TargetHealth { get; private set; }
    public int TargetMaxHealth { get; private set; } = 720;
    public int TotalDamage { get; private set; }
    public readonly int[] HitCounts = new int[3];
    public readonly int[] Energy = new int[3];
    public readonly int[] MaxHealth = { 320, 280, 240 };
    public bool IsPaused { get { return paused; } }
    public int PartyCount { get { return members == null ? 0 : members.Length; } }
    public bool IsReady { get { return initialized; } }
    SkeletonAnimation[] members;
    Transform[] feet;
    Vector3[] homes;
    bool[] attacking = new bool[3];
    Camera battleCamera;
    AudioSource sound;
    readonly System.Collections.Generic.Dictionary<string, AudioClip> clips = new System.Collections.Generic.Dictionary<string, AudioClip>();
    bool paused, muted, changingWave, initialized;
    float speed = 1, previousTimeScale = 1;
    Coroutine recoil;

    void Start() {
        if (dataAsset == null || dataAsset.GetSkeletonData(false) == null) { Debug.LogError("Training: missing HeroSkeleton.", this); enabled = false; return; }
        previousTimeScale = Time.timeScale;
        Time.timeScale = speed;
        battleCamera = Camera.main;
        if (battleCamera == null) { battleCamera = new GameObject("Battle Camera").AddComponent<Camera>(); battleCamera.tag = "MainCamera"; }
        battleCamera.orthographic = true; battleCamera.orthographicSize = BattleStage.CameraHeight;
        battleCamera.transform.position = new Vector3(0, 0, -10); battleCamera.rect = new Rect(0, 0, 1, 1);
        battleCamera.backgroundColor = new Color(.12f, .18f, .25f);
        if (FindObjectOfType<AudioListener>() == null) battleCamera.gameObject.AddComponent<AudioListener>();
        sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0; sound.volume = .45f;
        foreach (string name in new[] { "sword-swing", "wood-impact", "bow-release", "arcane-cast", "victory", "ui-click" }) clips[name] = Resources.Load<AudioClip>("Battle/Audio/" + name);
        if (stage == null) stage = BattleStage.Anchor(transform, "Layered training ground", Vector2.zero).gameObject.AddComponent<BattleStage>();
        stage.Build();
        if (stage.NeedsRebuild) { enabled = false; return; }
        battleCamera.backgroundColor = stage.SkyColor;
        feet = new Transform[3]; homes = new Vector3[3]; members = new SkeletonAnimation[3];
        for (int i = 0; i < 3; i++) {
            feet[i] = i == 0 ? stage.heroPoint : BattleStage.Anchor(stage.heroPoint.parent, BattleHeroVisual.Titles[i] + " foot anchor", stage.allySlots[i].localPosition);
            homes[i] = feet[i].position;
            stage.AddContactShadow(feet[i], .85f);
            members[i] = BattleHeroVisual.Spawn(dataAsset, feet[i], i);
        }
        var follow = battleCamera.GetComponent<BattleParallax>();
        if (follow == null) follow = battleCamera.gameObject.AddComponent<BattleParallax>();
        follow.Bind(battleCamera, stage, feet[0]);
        if (EventSystem.current == null) new GameObject("Battle input", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
        Hud = BattleStage.Anchor(transform, "Party HUD", Vector2.zero).gameObject.AddComponent<BattleHud>(); Hud.Build(this); ShowMap();
        TargetHealth = TargetMaxHealth; initialized = true; RefreshTarget(); RefreshControls();
        for (int i = 0; i < 3; i++) Hud.SetMember(i, MaxHealth[i], Energy[i], "准备就绪");
        if (autoStart) BeginLoops();
    }
    void BeginLoops() { for (int i = 0; i < 3; i++) StartCoroutine(MemberLoop(i)); }
    IEnumerator MemberLoop(int role) {
        yield return new WaitForSeconds(1.3f + role * .45f);
        while (true) {
            while (changingWave || TargetHealth <= 0) yield return null;
            yield return AttackRoutine(role);
            float cooldown = role == 0 ? 1.5f : role == 1 ? 1.8f : 2.3f;
            for (float elapsed = 0; elapsed < cooldown; elapsed += .1f) {
                Hud.SetMember(role, MaxHealth[role], Energy[role], "准备 " + Mathf.Max(0, cooldown - elapsed).ToString("0.0") + "秒");
                yield return new WaitForSeconds(.1f);
            }
        }
    }
    IEnumerator AttackRoutine(int role) {
        attacking[role] = true;
        bool skill = Energy[role] >= 100;
        if (skill) Energy[role] = 0;
        Hud.SetMember(role, MaxHealth[role], Energy[role], skill ? "技能释放" : "自动攻击");
        Vector3 target = stage.dummyPoint.position + Vector3.up;
        if (role == 0) {
            BattleHeroVisual.Play(members[role], role, "002A_move", true);
            yield return Move(feet[role], stage.dummyPoint.position + Vector3.left * 1.2f, .65f);
            float duration = BattleHeroVisual.Play(members[role], role, skill ? "004A" : "003A", false);
            Sound("sword-swing"); yield return new WaitForSeconds(Mathf.Clamp(duration * .42f, .15f, .55f));
            if (TargetHealth > 0) { Effect("slash", target, new Color(1, .8f, .35f), skill ? 1.05f : .7f, .3f); Hit(role, skill ? 98 : 38, target); }
            yield return new WaitForSeconds(.25f);
            BattleHeroVisual.Play(members[role], role, "002A_move", true);
            yield return Move(feet[role], homes[role], .6f);
        } else {
            float duration = BattleHeroVisual.Play(members[role], role, skill ? "004A" : "003A", false);
            Sound(role == 1 ? "bow-release" : "arcane-cast");
            yield return new WaitForSeconds(Mathf.Clamp(duration * .4f, .15f, .55f));
            int count = role == 1 && skill ? 3 : 1;
            for (int hit = 0; hit < count && TargetHealth > 0; hit++) {
                Vector3 origin = feet[role].position + new Vector3(.6f, 1, 0);
                var projectile = Effect(role == 1 ? "arrow" : "orb", origin, role == 1 ? new Color(.8f, 1, .5f) : new Color(.55f, .4f, 1), role == 1 ? .48f : .2f, .5f);
                projectile.velocity = (target - origin) / .35f;
                if (role == 1) projectile.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(target.y - origin.y, target.x - origin.x) * Mathf.Rad2Deg);
                yield return new WaitForSeconds(.35f); if (projectile != null) Destroy(projectile.gameObject);
                if (TargetHealth > 0) {
                    if (role == 2) { var ring = Effect("ring", target, new Color(.6f, .45f, 1), skill ? .9f : .45f, .35f); ring.expansion = 2; }
                    Hit(role, role == 1 ? 29 : skill ? 125 : 47, target);
                }
                yield return new WaitForSeconds(.12f);
            }
        }
        if (!skill) Energy[role] = Mathf.Min(100, Energy[role] + 34);
        BattleHeroVisual.Play(members[role], role, "001A_idle", true); attacking[role] = false;
        if (TargetHealth <= 0 && !changingWave) StartCoroutine(NextWave());
    }
    IEnumerator Move(Transform foot, Vector3 destination, float duration) {
        Vector3 from = foot.position;
        for (float t = 0; t < duration; t += Time.deltaTime) { foot.position = Vector3.Lerp(from, destination, Mathf.SmoothStep(0, 1, t / duration)); yield return null; }
        foot.position = destination;
    }
    void Hit(int role, int damage, Vector3 position) {
        int applied = Mathf.Min(TargetHealth, damage); TargetHealth -= applied; TotalDamage += applied; HitCounts[role]++;
        Sound("wood-impact");
        for (int i = 0; i < 6; i++) { var spark = Effect("spark", position, new Color(1, .78f, .4f), Random.Range(.025f, .045f), .25f); spark.velocity = Random.insideUnitCircle * 2.5f; }
        if (recoil != null) StopCoroutine(recoil); recoil = StartCoroutine(Recoil());
        Hud.ShowDamage(applied, position + Vector3.up * .35f, battleCamera); RefreshTarget();
    }
    IEnumerator Recoil() {
        for (float t = 0; t < .25f; t += Time.deltaTime) {
            stage.dummy.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 65) * 6 * (1 - t / .25f));
            stage.dummy.color = Color.Lerp(new Color(1, .6f, .4f), Color.white, t / .25f); yield return null;
        }
        stage.dummy.transform.localRotation = Quaternion.identity; stage.dummy.color = Color.white; recoil = null;
    }
    IEnumerator NextWave() {
        changingWave = true; Hud.SetBattleState("木桩击破"); Sound("victory");
        while (attacking[0] || attacking[1] || attacking[2]) yield return null;
        yield return new WaitForSeconds(1.4f);
        Wave++; TargetMaxHealth = 720 + (Wave - 1) * 120; TargetHealth = TargetMaxHealth;
        changingWave = false; Hud.SetBattleState("自动战斗"); RefreshTarget();
    }
    BattleEffect Effect(string shape, Vector3 position, Color color, float size, float life) {
        var effect = BattleEffect.Make(shape, position, color, size, life, 600); effect.transform.SetParent(transform, true); return effect;
    }
    public void RestartTraining() {
        if (!initialized) return;
        StopAllCoroutines(); recoil = null; changingWave = false; Wave = 1; TargetMaxHealth = 720; TargetHealth = 720; TotalDamage = 0;
        foreach (var effect in GetComponentsInChildren<BattleEffect>()) Destroy(effect.gameObject);
        for (int i = 0; i < 3; i++) { attacking[i] = false; HitCounts[i] = 0; Energy[i] = 0; feet[i].position = homes[i]; BattleHeroVisual.Play(members[i], i, "001A_idle", true); Hud.SetMember(i, MaxHealth[i], 0, "准备就绪"); }
        stage.dummy.transform.localRotation = Quaternion.identity; stage.dummy.color = Color.white;
        paused = false; Time.timeScale = speed; Hud.SetBattleState("自动战斗"); RefreshTarget(); RefreshControls(); BeginLoops();
    }
    public void NextMap() { ShiftMap(1); }
    public void PreviousMap() { ShiftMap(-1); }
    void ShiftMap(int direction) {
        if (!initialized) return;
        var maps = BattleMapCatalog.All;
        int index = (BattleMapCatalog.IndexOf(stage.MapId) + direction + maps.Length) % maps.Length;
        stage.SetMap(maps[index].id);
        battleCamera.backgroundColor = stage.SkyColor;
        ShowMap(); RefreshTarget();
    }
    public void TogglePause() { paused = !paused; Time.timeScale = paused ? 0 : speed; RefreshControls(); }
    public void ToggleSpeed() { speed = speed == 1 ? 2 : 1; if (!paused) Time.timeScale = speed; RefreshControls(); }
    public void ToggleAudio() { muted = !muted; sound.mute = muted; RefreshControls(); }
    void RefreshControls() { if (Hud != null) Hud.SetControls(paused, speed, muted); }
    void ShowMap() { if (Hud != null) Hud.SetLocation(stage.MapTitle, BattleMapCatalog.IndexOf(stage.MapId) + 1, BattleMapCatalog.All.Length); }
    void RefreshTarget() { if (Hud != null) Hud.SetTarget(Wave, TargetHealth, TargetMaxHealth, TotalDamage); }
    void Sound(string key) { AudioClip clip; if (sound != null && clips.TryGetValue(key, out clip) && clip != null) sound.PlayOneShot(clip); }
    void Update() { if (initialized && Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }
    void OnApplicationPause(bool value) { if (value && initialized && !paused) TogglePause(); }
    void OnDestroy() { if (initialized) Time.timeScale = previousTimeScale; }
}
