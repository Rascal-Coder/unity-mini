using System.Collections.Generic;
using UnityEngine;

// Four physical depth groups. The near terrain occludes separate rear decorations.
public sealed class BattleStage : MonoBehaviour {
    public const int CurrentLayoutVersion = 7;
    public const float CameraHeight = 5.8f;
    [SerializeField] int layoutVersion;
    public Transform heroPoint, dummyPoint;
    public Transform[] allySlots, enemySlots;
    public SpriteRenderer dummy;
    public Transform foreground, near, middle, far;
    public bool NeedsRebuild { get { return layoutVersion != CurrentLayoutVersion || heroPoint == null; } }
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static readonly string[] LegacyGroups = { "01 Far landscape", "02 Walkable ground", "03 Rear props", "04 Actors", "05 Foreground props", "06 Formation slots" };
    static readonly string[] LayerNames = { "01 Foreground", "02 Near battlefield", "03 Middle forest", "04 Far landscape" };

    public void Build() {
        if (!NeedsRebuild) return;
        foreach (var sprite in Resources.LoadAll<Sprite>("Battle/Art/Handpainted/Props")) sprites[sprite.name] = sprite;
        var terrain = Resources.Load<Sprite>("Battle/Art/Handpainted/TerrainWide");
        var mid = Resources.Load<Sprite>("Battle/Art/Handpainted/Middle");
        var sky = Resources.Load<Sprite>("Battle/Art/Handpainted/Far");
        if (terrain == null || mid == null || sky == null || !sprites.ContainsKey("Runestone")) {
            Debug.LogError("Party stage assets have not finished importing.", this); return;
        }
        for (int i = transform.childCount - 1; i >= 0; i--) {
            var child = transform.GetChild(i);
            if (System.Array.IndexOf(LegacyGroups, child.name) < 0 && System.Array.IndexOf(LayerNames, child.name) < 0) continue;
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        foreground = Anchor(transform, LayerNames[0], Vector2.zero);
        near = Anchor(transform, LayerNames[1], Vector2.zero);
        middle = Anchor(transform, LayerNames[2], Vector2.zero);
        far = Anchor(transform, LayerNames[3], Vector2.zero);
        Sized(far, "Distant mountains and sky", sky, new Vector2(0, -7.5f), new Vector2(36, 16), -1000);
        Sized(middle, "Forest ruins and gateway", mid, new Vector2(0, -1.4f), new Vector2(28, 10.6f), -800);
        var rear = Anchor(near, "Rear decorations - bases hidden by terrain", Vector2.zero);
        Place(rear, "Left rune stone", sprites["Runestone"], new Vector2(-8, -.35f), 2.9f, -600);
        Place(rear, "Right crystals", sprites["Crystal"], new Vector2(8, -.35f), 2.8f, -600);
        Place(rear, "Left moon flowers", sprites["MoonBush"], new Vector2(-6.5f, -.18f), 2.2f, -590);
        Place(rear, "Right moon flowers", sprites["MoonBush"], new Vector2(6.6f, -.22f), 2.3f, -590).flipX = true;
        Sized(near, "Solid terrace - broad top and deep rock face", terrain, new Vector2(0, -8.3f), new Vector2(28, 11), -400);
        var actors = Anchor(near, "Actors", Vector2.zero);
        var formation = Anchor(near, "Formation anchors", Vector2.zero);
        Vector2[] positions = { new Vector2(-2.5f, -1.65f), new Vector2(-4.5f, -.85f), new Vector2(-5.3f, -2.2f),
            new Vector2(-6.4f, -.85f), new Vector2(-6.7f, -1.65f), new Vector2(-3.4f, -2.6f), new Vector2(-1.2f, -.85f), new Vector2(-1.1f, -2.6f) };
        allySlots = new Transform[8]; enemySlots = new Transform[8];
        for (int i = 0; i < positions.Length; i++) {
            allySlots[i] = Anchor(formation, "Ally slot " + (i + 1), positions[i]);
            enemySlots[i] = Anchor(formation, "Enemy slot " + (i + 1), new Vector2(-positions[i].x, positions[i].y));
        }
        heroPoint = Anchor(actors, "Warrior foot anchor", positions[0]);
        dummyPoint = Anchor(actors, "Dummy foot anchor", new Vector2(4.8f, -1.65f));
        dummyPoint.gameObject.AddComponent<BattleDepth>();
        dummy = Place(dummyPoint, "Wooden training dummy", sprites["TrainingDummy"], Vector2.zero, 1.35f, 0);
        AddContactShadow(heroPoint, .9f); AddContactShadow(dummyPoint, 1.1f);
        var left = Place(foreground, "Foreground left roots", sprites["RootLeft"], new Vector2(-10.7f, -6.4f), 5.1f, 1000);
        var right = Place(foreground, "Foreground right roots", sprites["RootRight"], new Vector2(10.7f, -6.4f), 5.1f, 1000);
        left.color = right.color = new Color(.52f, .64f, .72f);
        Place(foreground, "Foreground mushrooms", sprites["Mushrooms"], new Vector2(9.1f, -5.4f), 1.8f, 1001).color = new Color(.7f, .77f, .85f);
        layoutVersion = CurrentLayoutVersion;
    }
    public void AddContactShadow(Transform foot, float width) {
        Place(foot, "Contact shadow", Resources.Load<Sprite>("Battle/Art/Enchanted/ContactShadow"), Vector2.zero, width, -20);
    }
    public void FrameForeground(float halfWidth, float halfHeight) {
        if (foreground == null || foreground.childCount < 3) return;
        foreground.GetChild(0).localPosition = new Vector3(-halfWidth - .4f, -halfHeight - .6f, 0);
        foreground.GetChild(1).localPosition = new Vector3(halfWidth + .4f, -halfHeight - .6f, 0);
        foreground.GetChild(2).localPosition = new Vector3(halfWidth - 1.2f, -halfHeight + .2f, 0);
    }
    public static Transform Anchor(Transform parent, string title, Vector2 position) {
        var obj = new GameObject(title); obj.transform.SetParent(parent, false); obj.transform.localPosition = position; return obj.transform;
    }
    SpriteRenderer Place(Transform parent, string title, Sprite sprite, Vector2 position, float width, int order) {
        var obj = Anchor(parent, title, position).gameObject;
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
        if (sprite == null) { Debug.LogError("Missing stage sprite: " + title, this); return renderer; }
        obj.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        renderer.sharedMaterial = Resources.Load<Material>("Battle/SpriteMaterial"); return renderer;
    }
    void Sized(Transform parent, string title, Sprite sprite, Vector2 position, Vector2 size, int order) {
        var renderer = Place(parent, title, sprite, position, size.x, order);
        renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
    }
    void OnDrawGizmosSelected() { DrawSlots(allySlots, Color.cyan); DrawSlots(enemySlots, new Color(1, .5f, .3f)); }
    static void DrawSlots(Transform[] slots, Color color) {
        if (slots == null) return; Gizmos.color = color;
        foreach (var slot in slots) if (slot != null) Gizmos.DrawWireCube(slot.position, new Vector3(1.2f, .4f, .01f));
    }
}
