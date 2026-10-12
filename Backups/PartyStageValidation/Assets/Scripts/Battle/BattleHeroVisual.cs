using System;
using System.Collections.Generic;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEngine;

public static class BattleHeroVisual {
    public static readonly int[] Professions = { 0, 4, 6 };
    public static readonly string[] Titles = { "战士", "游侠", "法师" };
    public static readonly string[] PortraitNames = { "warrior", "ranger", "mage" };
    public static SkeletonAnimation Spawn(SkeletonDataAsset asset, Transform foot, int role, float height = 1.65f, bool faceRight = true) {
        if (foot.GetComponent<BattleDepth>() == null) foot.gameObject.AddComponent<BattleDepth>();
        var hero = SkeletonAnimation.NewSkeletonAnimationGameObject(asset);
        hero.name = Titles[role]; hero.transform.SetParent(foot, false); hero.Initialize(false);
        var data = hero.Skeleton.Data; var profession = HeroProfession.All[Professions[role]];
        var selected = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var skin in data.Skins.OrderBy(s => s.Name, StringComparer.Ordinal)) {
            if (!skin.Name.StartsWith("001_appearance/", StringComparison.Ordinal)) continue;
            string key = skin.Name.Substring(0, skin.Name.LastIndexOf('/'));
            if (!selected.ContainsKey(key)) selected[key] = skin.Name;
        }
        selected["001_appearance/head"] = "001_appearance/head/head_001";
        selected["001_appearance/dress/headwear"] = "001_appearance/dress/headwear/headwear_000";
        selected["001_appearance/dress/Tail"] = "001_appearance/dress/Tail/Tail_000";
        selected["001_appearance/head_hair"] = "001_appearance/head_hair/" + profession.hair;
        selected["001_appearance/dress/backpack"] = "001_appearance/dress/backpack/" + profession.backpack;
        selected["001_appearance/dress/eyewear"] = "001_appearance/dress/eyewear/" + profession.eyewear;
        selected["002A_armor"] = "002A_armor/" + profession.armor;
        selected["003_weapon"] = "003_weapon/" + profession.weapon;
        var combined = new Skin("party-" + profession.id);
        if (data.DefaultSkin != null) combined.AddSkin(data.DefaultSkin);
        foreach (string skinName in selected.Values) { var skin = data.FindSkin(skinName); if (skin != null) combined.AddSkin(skin); }
        hero.Skeleton.SetSkin(combined); hero.Skeleton.SetToSetupPose(); hero.Skeleton.ScaleX = faceRight ? -1 : 1;
        Play(hero, role, "001A_idle", true); hero.Update(0); hero.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
        float[] vertices = null; float x, y, w, h; hero.Skeleton.GetBounds(out x, out y, out w, out h, ref vertices);
        float scale = height / Mathf.Max(.01f, h);
        hero.transform.localScale = Vector3.one * scale; hero.transform.localPosition = new Vector3(0, -y * scale, 0);
        hero.gameObject.AddComponent<BattleGrounding>().Bind(hero, foot);
        hero.LateUpdate(); return hero;
    }
    public static float Play(SkeletonAnimation hero, int role, string token, bool loop) {
        string group = HeroProfession.All[Professions[role]].animationGroup + "/";
        var animation = hero.Skeleton.Data.Animations.FirstOrDefault(a => a.Name.StartsWith(group + token, StringComparison.Ordinal) && a.Name.IndexOf("backup", StringComparison.OrdinalIgnoreCase) < 0);
        if (animation == null) { if (token != "001A_idle") return Play(hero, role, "001A_idle", true); return .7f; }
        hero.AnimationState.SetAnimation(0, animation, loop); return animation.Duration;
    }
}
