using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Spine;
using Spine.Unity;

// Each profession owns its weapon family, animation set and default outfit.
public sealed class HeroProfession {
    public string id, title, description, weaponFamily, animationGroup, weapon, hair, armor, backpack, eyewear;
    public Color accent;
    public HeroProfession(string id, string title, string description, string weaponFamily, string animationGroup,
        string weapon, string hair, string armor, string backpack, string eyewear, string color) {
        this.id = id; this.title = title; this.description = description;
        this.weaponFamily = weaponFamily; this.animationGroup = animationGroup;
        this.weapon = weapon; this.hair = hair; this.armor = armor; this.backpack = backpack; this.eyewear = eyewear;
        ColorUtility.TryParseHtmlString(color, out accent);
    }
    public static readonly HeroProfession[] All = {
        new HeroProfession("guardian", "盾卫", "剑盾守御 · 前排保护", "SHW", "hero_SHW", "SHW/D1_001", "hair_005_HairA", "HA/HA_003", "backpack_001", "eyewear_000", "#D8BC72"),
        new HeroProfession("vanguard", "重刃战士", "双手重刃 · 正面压制", "THW", "hero_THW", "THW/D2_001", "hair_032_HairC", "HA/HA_009", "backpack_004", "eyewear_000", "#D9876E"),
        new HeroProfession("shadow", "影刃", "双刃突袭 · 灵活近战", "DB", "hero_DUALBLADES", "DB/D8_001", "hair_019_HairA", "CA/CA_008", "backpack_003", "eyewear_000", "#B59CD9"),
        new HeroProfession("samurai", "武士", "太刀连击 · 拔刀斩击", "Katana", "hero_Katana", "Katana/D3_001", "hair_015_HairC", "NA/NA_016", "backpack_004", "eyewear_000", "#DE8C91"),
        new HeroProfession("ranger", "游侠", "弓箭远射 · 机动输出", "Bow", "hero_BOW", "Bow/D6_001", "hair_002_HairC", "NA/NA_031", "backpack_001", "eyewear_000", "#8BC69C"),
        new HeroProfession("gunner", "枪手", "枪械射击 · 远程火力", "Gun", "hero_GUN", "Gun/A/D5_001", "hair_011_HairA", "CA/CA_022", "backpack_009", "eyewear_006", "#E1AF6B"),
        new HeroProfession("arcanist", "秘法师", "法杖施法 · 奥术输出", "Staff", "hero_STAFF", "Staff/D4_001", "hair_038_HairA", "CA/CA_004", "backpack_020", "eyewear_003", "#8FB6E5"),
        new HeroProfession("breaker", "破阵者", "长柄重兵 · 突进破阵", "FistDrill", "hero_FistDrill", "FistDrill/D7_001", "hair_023_HairA", "HA/HA_018", "backpack_014", "eyewear_008", "#75C8C5")
    };
}

public sealed class HeroWardrobe : MonoBehaviour {
    public SkeletonDataAsset dataAsset;
    SkeletonAnimation hero;
    Camera previewCamera;
    int professionIndex, tab;
    HeroProfession Profession { get { return HeroProfession.All[professionIndex]; } }
    readonly Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);
    readonly Dictionary<string, string[]> sessionOutfits = new Dictionary<string, string[]>();
    List<Spine.Animation> animations = new List<Spine.Animation>();
    readonly List<string> itemChoices = new List<string>();
    string category = "003_weapon", filter = "", animationFilter = "", currentAnimation = "", status = "";
    Vector2 roleScroll, categoryScroll, itemScroll, animationScroll;
    float zoom = 1.25f, speed = 1;
    bool paused, flipped, repeatAction;
    Font uiFont;
    GUISkin uiSkin;
    GUIStyle heading, subheading, muted, card, selectedCard, item, selectedItem, section, body;
    readonly List<Texture2D> uiTextures = new List<Texture2D>();
    static readonly string[] Tabs = { "职业动作", "外观搭配" };
    static readonly string[] CategoryKeys = { "003_weapon", "002A_armor", "002B_helmet", "001_appearance/head_hair", "001_appearance/dress/backpack", "001_appearance/dress/headwear", "001_appearance/dress/eyewear", "001_appearance/face_eye", "001_appearance/face_eyebrow", "001_appearance/face_mouth", "001_appearance/face_nose", "001_appearance/face_beard", "001_appearance/face_makeup", "001_appearance/head", "001_appearance/head_ear", "001_appearance/dress/Tail" };
    static readonly string[] CategoryLabels = { "武器", "服装", "头盔", "发型", "背饰", "头饰", "眼饰", "眼睛", "眉毛", "嘴型", "鼻子", "胡须", "妆容", "脸型", "耳朵", "尾饰" };
    static string Group(string name) {
        if (name.StartsWith("002A_armor/", StringComparison.Ordinal)) return "002A_armor";
        if (name.StartsWith("002B_helmet/", StringComparison.Ordinal)) return "002B_helmet";
        if (name.StartsWith("003_weapon/", StringComparison.Ordinal)) return "003_weapon";
        int slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(0, slash);
    }
    static bool IsExcluded(string name) {
        return name.StartsWith("003_weapon/PL/", StringComparison.Ordinal) || name.StartsWith("002A_armor/PL/", StringComparison.Ordinal);
    }
    bool AllowedSkin(string name) {
        return !IsExcluded(name) && (!name.StartsWith("003_weapon/", StringComparison.Ordinal) || name.StartsWith("003_weapon/" + Profession.weaponFamily + "/", StringComparison.Ordinal));
    }
    void Start() {
        if (dataAsset == null || dataAsset.GetSkeletonData(false) == null) {
            status = "角色资源未加载，请检查 HeroSkeleton。"; enabled = false; return;
        }
        previewCamera = Camera.main;
        if (previewCamera == null) {
            previewCamera = new GameObject("Preview Camera").AddComponent<Camera>();
            previewCamera.tag = "MainCamera";
        }
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(.065f, .095f, .12f);
        hero = SkeletonAnimation.NewSkeletonAnimationGameObject(dataAsset);
        hero.name = "Profession Preview";
        hero.transform.SetParent(transform, false);
        hero.Initialize(false);
        foreach (var skin in hero.Skeleton.Data.Skins) {
            if (skin.Name == "default" || IsExcluded(skin.Name)) continue;
            string key = Group(skin.Name);
            if (!groups.ContainsKey(key)) groups[key] = new List<string>();
            groups[key].Add(skin.Name);
        }
        foreach (var options in groups.Values) options.Sort(StringComparer.Ordinal);
        SwitchProfession(0, false);
    }
    void SwitchProfession(int index, bool remember = true) {
        if (remember) sessionOutfits[Profession.id] = equipped.Values.ToArray();
        professionIndex = Mathf.Clamp(index, 0, HeroProfession.All.Length - 1);
        animations = hero.Skeleton.Data.Animations.Where(a => a.Name.StartsWith(Profession.animationGroup + "/", StringComparison.Ordinal)
            && a.Name.IndexOf("backup", StringComparison.OrdinalIgnoreCase) < 0).ToList();
        animationFilter = ""; filter = ""; animationScroll = itemScroll = Vector2.zero;
        paused = false; currentAnimation = "";
        hero.AnimationState.ClearTracks();
        hero.Skeleton.SetToSetupPose();
        SetDefaults();
        if (sessionOutfits.TryGetValue(Profession.id, out var saved)) RestoreSkins(saved);
        Apply();
        PlayIdle();
        RebuildChoices();
        status = Profession.title + " · 已配好外观与动作";
    }
    void SetDefaults() {
        equipped.Clear();
        foreach (var pair in groups) {
            if (pair.Key.StartsWith("001_appearance/", StringComparison.Ordinal)) equipped[pair.Key] = pair.Value[0];
        }
        SetSkinIfPresent("001_appearance/head/head_001");
        SetSkinIfPresent("001_appearance/head_hair/" + Profession.hair);
        SetSkinIfPresent("002A_armor/" + Profession.armor);
        SetSkinIfPresent("003_weapon/" + Profession.weapon);
        SetSkinIfPresent("001_appearance/dress/backpack/" + Profession.backpack);
        SetSkinIfPresent("001_appearance/dress/eyewear/" + Profession.eyewear);
        SetSkinIfPresent("001_appearance/dress/headwear/headwear_000");
        SetSkinIfPresent("001_appearance/dress/Tail/Tail_000");
    }
    void SetSkinIfPresent(string name) {
        if (AllowedSkin(name) && hero.Skeleton.Data.FindSkin(name) != null) equipped[Group(name)] = name;
        else Debug.LogWarning("职业预设缺少或不允许使用皮肤：" + name);
    }
    public void Equip(string skinName) {
        if (!AllowedSkin(skinName) || hero.Skeleton.Data.FindSkin(skinName) == null) return;
        equipped[Group(skinName)] = skinName;
        Apply(); status = "已替换：" + ShortName(skinName);
    }
    void Apply() {
        var skin = new Skin("profession-" + Profession.id);
        if (hero.Skeleton.Data.DefaultSkin != null) skin.AddSkin(hero.Skeleton.Data.DefaultSkin);
        foreach (var pair in equipped.OrderBy(p => p.Key, StringComparer.Ordinal)) skin.AddSkin(hero.Skeleton.Data.FindSkin(pair.Value));
        hero.Skeleton.SetSkin(skin);
        hero.Skeleton.SetSlotsToSetupPose();
        hero.AnimationState.Apply(hero.Skeleton);
    }
    void ResetOutfit() {
        hero.AnimationState.ClearTracks(); hero.Skeleton.SetToSetupPose();
        SetDefaults(); Apply(); paused = false; PlayIdle();
        status = "已恢复「" + Profession.title + "」推荐搭配";
    }
    string IdleName() {
        return animations.FirstOrDefault(a => a.Name.EndsWith("/001A_idle", StringComparison.Ordinal))?.Name;
    }
    void PlayIdle() {
        var idle = IdleName();
        if (idle != null) Play(idle);
    }
    void Play(string name) {
        if (!animations.Any(a => a.Name == name)) return;
        currentAnimation = name;
        hero.AnimationState.ClearTracks();
        hero.Skeleton.SetToSetupPose();
        hero.Skeleton.SetSlotsToSetupPose();
        bool loop = repeatAction || name.Contains("001A_idle") || name.EndsWith("/002A_move", StringComparison.Ordinal);
        hero.AnimationState.SetAnimation(0, name, loop);
        string idle = IdleName();
        if (!loop && idle != null && name != idle && name.IndexOf("die", StringComparison.OrdinalIgnoreCase) < 0)
            hero.AnimationState.AddAnimation(0, idle, true, 0);
        hero.AnimationState.Apply(hero.Skeleton);
    }
    void RebuildChoices() {
        itemChoices.Clear();
        if (!groups.TryGetValue(category, out var options)) return;
        itemChoices.AddRange(options.Where(AllowedSkin));
    }
    void RestoreSkins(string[] skins) {
        foreach (string name in skins ?? new string[0]) {
            if (name != null && AllowedSkin(name) && hero.Skeleton.Data.FindSkin(name) != null) equipped[Group(name)] = name;
        }
    }
    [Serializable] public class Outfit { public int version = 2; public string profession; public string[] skins; }
    string SaveKey { get { return "hero.profession.v2." + Profession.id; } }
    void Save() {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new Outfit { profession = Profession.id, skins = equipped.Values.ToArray() }));
        PlayerPrefs.Save(); status = "已保存「" + Profession.title + "」搭配";
    }
    void Load() {
        if (!PlayerPrefs.HasKey(SaveKey)) { status = "该职业还没有已保存的搭配"; return; }
        try {
            var outfit = JsonUtility.FromJson<Outfit>(PlayerPrefs.GetString(SaveKey));
            if (outfit == null || outfit.version != 2 || outfit.profession != Profession.id || outfit.skins == null) { status = "存档与当前职业不匹配"; return; }
            SetDefaults(); RestoreSkins(outfit.skins); Apply(); status = "已读取「" + Profession.title + "」搭配";
        } catch (Exception) { status = "读取失败，请重新保存该职业搭配"; }
    }
    void Update() {
        if (hero == null) return;
        hero.timeScale = paused ? 0 : speed;
        hero.Skeleton.ScaleX = flipped ? -1 : 1;
        previewCamera.rect = new Rect(.195f, .09f, .495f, .79f);
        var data = hero.Skeleton.Data;
        float unit = dataAsset.scale;
        float size = Mathf.Max(1, data.Height * unit * .8f, data.Width * unit / Mathf.Max(.2f, previewCamera.aspect) * .65f);
        previewCamera.orthographicSize = size / zoom;
        Vector3 center = hero.transform.TransformPoint(new Vector3((data.X + data.Width / 2) * unit, (data.Y + data.Height / 2) * unit, 0));
        previewCamera.transform.position = new Vector3(center.x, center.y, center.z - 10);
    }
    Texture2D Solid(Color color) {
        var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); uiTextures.Add(texture); return texture;
    }
    void InitUI() {
        if (uiSkin != null) return;
        uiSkin = Instantiate(GUI.skin);
        uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, 16);
        uiSkin.font = uiFont;
        var normal = Solid(new Color(.105f, .145f, .18f));
        var hover = Solid(new Color(.16f, .215f, .25f));
        var selected = Solid(new Color(.21f, .30f, .32f));
        uiSkin.label.fontSize = 14; uiSkin.label.normal.textColor = new Color(.86f, .90f, .91f);
        uiSkin.label.wordWrap = true;
        uiSkin.button.fontSize = 14; uiSkin.button.fixedHeight = 34;
        uiSkin.button.normal.background = normal; uiSkin.button.hover.background = hover;
        uiSkin.button.active.background = selected; uiSkin.button.focused.background = hover;
        uiSkin.button.normal.textColor = new Color(.86f, .90f, .91f);
        uiSkin.button.hover.textColor = Color.white; uiSkin.button.active.textColor = Color.white;
        uiSkin.button.border = new RectOffset(0, 0, 0, 0);
        uiSkin.button.margin = new RectOffset(2, 2, 3, 3);
        uiSkin.textField.fontSize = 14; uiSkin.textField.fixedHeight = 32;
        uiSkin.textField.normal.background = normal; uiSkin.textField.focused.background = hover;
        uiSkin.textField.normal.textColor = Color.white; uiSkin.textField.focused.textColor = Color.white;
        uiSkin.textField.padding = new RectOffset(10, 10, 6, 5);
        uiSkin.toggle.fontSize = 14;
        heading = new GUIStyle(uiSkin.label) { fontSize = 27, fontStyle = FontStyle.Bold };
        subheading = new GUIStyle(uiSkin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        muted = new GUIStyle(uiSkin.label) { fontSize = 12 }; muted.normal.textColor = new Color(.52f, .63f, .67f);
        section = new GUIStyle(uiSkin.label) { fontSize = 13, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 10, 6) };
        body = new GUIStyle(uiSkin.label) { fontSize = 14, wordWrap = true };
        card = new GUIStyle(uiSkin.button) { alignment = TextAnchor.MiddleLeft, fixedHeight = 61, padding = new RectOffset(14, 8, 7, 7), fontSize = 16 };
        selectedCard = new GUIStyle(card); selectedCard.normal.background = selected;
        item = new GUIStyle(uiSkin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 6, 3, 3), wordWrap = true, fixedHeight = 42, fontSize = 12 };
        selectedItem = new GUIStyle(item); selectedItem.normal.background = selected;
    }
    static string ShortName(string name) { return name.Substring(name.LastIndexOf('/') + 1); }
    void Panel(Rect rect) { GUI.DrawTexture(rect, uiTextures[0], ScaleMode.StretchToFill); }
    void OnGUI() {
        if (hero == null) return;
        InitUI(); var oldSkin = GUI.skin; var oldMatrix = GUI.matrix; GUI.skin = uiSkin;
        float scale = Mathf.Max(.1f, Mathf.Min(Screen.width / 1360f, Screen.height / 820f));
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = Screen.height / scale;
        float leftW = w * .18f, rightX = w * .705f, rightW = w - rightX - 18;
        Panel(new Rect(0, 0, w, 82));
        GUI.Label(new Rect(22, 14, 450, 37), "英雄图鉴  /  职业搭配", heading);
        GUI.Label(new Rect(24, 52, 600, 22), "8 个职业  ·  专属武器与动作  ·  独立保存外观", muted);
        Panel(new Rect(12, 98, leftW - 12, h - 116));
        GUILayout.BeginArea(new Rect(24, 109, leftW - 36, h - 140));
        GUILayout.Label("选择职业", section);
        roleScroll = GUILayout.BeginScrollView(roleScroll);
        for (int i = 0; i < HeroProfession.All.Length; i++) {
            var p = HeroProfession.All[i];
            GUI.contentColor = i == professionIndex ? p.accent : Color.white;
            if (GUILayout.Button(p.title + "\n" + p.description.Split('·')[0].Trim(), i == professionIndex ? selectedCard : card)) SwitchProfession(i);
        }
        GUI.contentColor = Color.white;
        GUILayout.EndScrollView(); GUILayout.EndArea();
        float previewX = w * .205f;
        GUI.contentColor = Profession.accent;
        GUI.Label(new Rect(previewX, 103, w * .47f, 35), Profession.title, heading);
        GUI.contentColor = Color.white;
        GUI.Label(new Rect(previewX, 144, w * .47f, 30), Profession.description, body);
        GUI.Label(new Rect(previewX, h - 75, w * .47f, 27), "当前动作：" + AnimationLabel(hero.AnimationState.GetCurrent(0)?.Animation.Name ?? currentAnimation), muted);
        GUI.Label(new Rect(previewX, h - 47, w * .47f, 24), status, muted);
        Panel(new Rect(rightX - 10, 98, rightW + 10, h - 116));
        GUILayout.BeginArea(new Rect(rightX, 111, rightW - 12, h - 141));
        GUILayout.Label("角色预览", subheading);
        GUILayout.BeginHorizontal();
        GUILayout.Label("缩放 " + zoom.ToString("F1"), GUILayout.Width(85)); zoom = GUILayout.HorizontalSlider(zoom, .65f, 2.5f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(paused ? "继续" : "暂停")) paused = !paused;
        if (GUILayout.Button(flipped ? "面向左侧" : "翻转朝向")) flipped = !flipped;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("速度 " + speed.ToString("F1"), GUILayout.Width(85)); speed = GUILayout.HorizontalSlider(speed, .25f, 2);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        tab = GUILayout.Toolbar(tab, Tabs);
        if (tab == 0) DrawAnimations(); else DrawOutfit();
        GUILayout.Space(8);
        GUILayout.BeginHorizontal(); if (GUILayout.Button("保存搭配")) Save(); if (GUILayout.Button("读取搭配")) Load(); GUILayout.EndHorizontal();
        if (GUILayout.Button("恢复本职业推荐搭配")) ResetOutfit();
        GUILayout.EndArea();
        GUI.contentColor = Color.white; GUI.skin = oldSkin; GUI.matrix = oldMatrix;
    }
    void DrawAnimations() {
        GUILayout.Label("常用动作", section);
        string[] names = { "待机", "移动", "普攻", "技能", "受击", "死亡" };
        string[] codes = { "001A_idle", "002A_move", "003A_", "004A_", "005A_", "007_die" };
        for (int row = 0; row < 3; row++) {
            GUILayout.BeginHorizontal();
            for (int col = 0; col < 2; col++) {
                int i = row * 2 + col;
                var action = animations.FirstOrDefault(a => ShortName(a.Name).StartsWith(codes[i], StringComparison.Ordinal));
                GUI.enabled = action != null;
                if (GUILayout.Button(names[i])) { paused = false; Play(action.Name); }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
        repeatAction = GUILayout.Toggle(repeatAction, "循环预览攻击 / 技能（下次播放生效）");
        GUILayout.Label("本职业动作 · " + animations.Count, section);
        animationFilter = GUILayout.TextField(animationFilter);
        if (animationFilter.Length == 0) GUILayout.Label("输入动作原名或编号筛选", muted);
        animationScroll = GUILayout.BeginScrollView(animationScroll);
        foreach (var animation in animations) {
            if (animation.Name.IndexOf(animationFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool active = hero.AnimationState.GetCurrent(0)?.Animation.Name == animation.Name;
            if (GUILayout.Button(AnimationLabel(animation.Name) + "  /  " + ShortName(animation.Name), active ? selectedItem : item)) { paused = false; Play(animation.Name); }
        }
        GUILayout.EndScrollView();
    }
    void DrawOutfit() {
        GUILayout.Label("部件分类", section);
        categoryScroll = GUILayout.BeginScrollView(categoryScroll, GUILayout.Height(112));
        for (int row = 0; row < CategoryKeys.Length; row += 3) {
            GUILayout.BeginHorizontal();
            for (int col = 0; col < 3 && row + col < CategoryKeys.Length; col++) {
                int i = row + col;
                GUI.contentColor = category == CategoryKeys[i] ? Profession.accent : Color.white;
                if (GUILayout.Button(CategoryLabels[i])) { category = CategoryKeys[i]; filter = ""; itemScroll = Vector2.zero; RebuildChoices(); }
            }
            GUILayout.EndHorizontal();
        }
        GUI.contentColor = Color.white; GUILayout.EndScrollView();
        filter = GUILayout.TextField(filter);
        GUILayout.Label((category == "003_weapon" ? "仅显示本职业武器" : "可自由调整外观") + " · " + itemChoices.Count + " 款", muted);
        if (category == "002B_helmet" && GUILayout.Button("不戴头盔")) { equipped.Remove(category); Apply(); }
        itemScroll = GUILayout.BeginScrollView(itemScroll);
        foreach (string name in itemChoices) {
            if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool selected = equipped.TryGetValue(category, out var current) && current == name;
            if (GUILayout.Button((selected ? "●  " : "    ") + ShortName(name), selected ? selectedItem : item)) Equip(name);
        }
        GUILayout.EndScrollView();
    }
    static string AnimationLabel(string name) {
        string n = ShortName(name ?? "");
        if (n.StartsWith("001")) return "待机 / 转向";
        if (n.StartsWith("002")) return "移动";
        if (n.StartsWith("003")) return "普通攻击";
        if (n.StartsWith("004")) return "职业技能";
        if (n.StartsWith("005") || n.StartsWith("006")) return "受击";
        if (n.StartsWith("007")) return "死亡";
        if (n.StartsWith("008") || n.StartsWith("009")) return "跳跃";
        if (n.StartsWith("010") || n.StartsWith("011")) return "闪避";
        if (n.StartsWith("015")) return "道具";
        return "特殊动作";
    }
    void OnDestroy() {
        if (uiSkin != null) Destroy(uiSkin);
        if (uiFont != null) Destroy(uiFont);
        foreach (var t in uiTextures) if (t != null) Destroy(t);
    }
}
