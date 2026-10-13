using System.Collections.Generic;
using UnityEngine;

// Four physical depth groups. The floor stays in the near group so boots and contact shadows remain planted while the backdrop parallax moves.
public sealed class BattleStage : MonoBehaviour {
    public const int CurrentLayoutVersion = 11;
    public const float CameraHeight = 5.8f;
    public const float Horizon = -0.50f;
    public const float StageWidth = 24f;
    [SerializeField] int layoutVersion;
    [SerializeField] string mapId = BattleMapCatalog.DefaultId;
    public Transform heroPoint, dummyPoint;
    public Transform[] allySlots, enemySlots;
    public SpriteRenderer dummy;
    public Transform foreground, near, middle, far;
    public string MapId { get { return string.IsNullOrEmpty(mapId) ? BattleMapCatalog.DefaultId : mapId; } }
    public string MapTitle { get; private set; }
    public Color SkyColor { get; private set; }
    public Color ShadowTint { get; private set; } = new Color(.7f, .77f, .68f, 1f);
    public bool NeedsRebuild { get { return layoutVersion != CurrentLayoutVersion || heroPoint == null; } }
    [SerializeField] SpriteRenderer farArt, groundArt;
    [SerializeField] List<SpriteRenderer> middleArt = new List<SpriteRenderer>();
    static readonly string[] LegacyGroups = { "01 Far landscape", "02 Walkable ground", "03 Rear props", "04 Actors", "05 Foreground props", "06 Formation slots" };
    static readonly string[] LayerNames = { "01 Foreground", "02 Near battlefield", "03 Middle forest", "04 Far landscape" };

    public void Build() {
        if (!NeedsRebuild) return;
        var props = new Dictionary<string, Sprite>();
        foreach (var sprite in Resources.LoadAll<Sprite>("Battle/Art/Handpainted/Props")) props[sprite.name] = sprite;
        if (!props.ContainsKey("TrainingDummy")) { Debug.LogError("Training dummy has not finished importing.", this); return; }
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
        farArt = Paint(far, "Distant backdrop", -1000);
        if (middleArt == null) middleArt = new List<SpriteRenderer>();
        middleArt.Clear();
        middleArt.Add(Paint(middle, "Scenery", -800));
        middleArt.Add(Paint(middle, "Scenery front", -790));
        groundArt = Paint(near, "Battle floor", -400);
        if (!ApplyMap(MapId)) return;
        var actors = Anchor(near, "Actors", Vector2.zero);
        var formation = Anchor(near, "Formation anchors", Vector2.zero);
        Vector2[] positions = { new Vector2(-2.5f, -2.65f), new Vector2(-4.5f, -1.85f), new Vector2(-5.3f, -3.2f),
            new Vector2(-6.4f, -1.85f), new Vector2(-6.7f, -2.65f), new Vector2(-3.4f, -3.6f), new Vector2(-1.2f, -1.85f), new Vector2(-1.1f, -3.6f) };
        allySlots = new Transform[8]; enemySlots = new Transform[8];
        for (int i = 0; i < positions.Length; i++) {
            allySlots[i] = Anchor(formation, "Ally slot " + (i + 1), positions[i]);
            enemySlots[i] = Anchor(formation, "Enemy slot " + (i + 1), new Vector2(-positions[i].x, positions[i].y));
        }
        heroPoint = Anchor(actors, "Warrior foot anchor", positions[0]);
        dummyPoint = Anchor(actors, "Dummy foot anchor", new Vector2(4.8f, -2.65f));
        dummyPoint.gameObject.AddComponent<BattleDepth>();
        dummy = Place(dummyPoint, "Wooden training dummy", props["TrainingDummy"], Vector2.zero, 1.35f, 0);
        AddContactShadow(heroPoint, .9f); AddContactShadow(dummyPoint, 1.1f);
        layoutVersion = CurrentLayoutVersion;
    }

    void Awake() {
        if (Application.isPlaying && !string.IsNullOrEmpty(BattleSelection.MapId)) mapId = BattleSelection.MapId;
        if (!EnsureArt()) {
            var map = BattleMapCatalog.Get(MapId);
            MapTitle = map.title; SkyColor = map.sky; ShadowTint = map.shadow;
            return;
        }
        ApplyMap(MapId);
    }

    public void SetMap(string id) {
        mapId = string.IsNullOrEmpty(id) ? BattleMapCatalog.DefaultId : id;
        if (EnsureArt()) ApplyMap(mapId);
        else Build();
    }

    bool EnsureArt() {
        if (far == null) far = transform.Find(LayerNames[3]);
        if (middle == null) middle = transform.Find(LayerNames[2]);
        if (near == null) near = transform.Find(LayerNames[1]);
        if (foreground == null) foreground = transform.Find(LayerNames[0]);
        if (farArt == null && far != null) farArt = far.Find("Distant backdrop")?.GetComponent<SpriteRenderer>();
        if (groundArt == null && near != null) groundArt = near.Find("Battle floor")?.GetComponent<SpriteRenderer>();
        if (middleArt == null) middleArt = new List<SpriteRenderer>();
        if (middleArt.Count == 0 && middle != null) {
            var scenery = middle.Find("Scenery")?.GetComponent<SpriteRenderer>();
            var front = middle.Find("Scenery front")?.GetComponent<SpriteRenderer>();
            if (scenery != null) middleArt.Add(scenery);
            if (front != null) middleArt.Add(front);
        }
        return farArt != null && groundArt != null && middleArt.Count > 0;
    }

    bool ApplyMap(string id) {
        var map = BattleMapCatalog.Get(id);
        var farSprite = Resources.Load<Sprite>(map.Art("Far"));
        var groundSprite = Resources.Load<Sprite>(map.Art("Ground"));
        if (farSprite == null || groundSprite == null || map.middle.Length == 0) {
            Debug.LogError("Map assets have not finished importing: " + map.id, this); return false;
        }
        var scenery = new Sprite[map.middle.Length];
        for (int i = 0; i < scenery.Length; i++) {
            scenery[i] = Resources.Load<Sprite>(map.Art(map.middle[i]));
            if (scenery[i] == null) { Debug.LogError("Missing map scenery: " + map.id + "/" + map.middle[i], this); return false; }
        }
        mapId = map.id; MapTitle = map.title; SkyColor = map.sky; ShadowTint = map.shadow;
        float band = Mathf.Max(.08f, 1f - map.groundLine);
        float groundHeight = groundSprite.rect.height * (StageWidth / groundSprite.rect.width);
        float scale = Mathf.Max(1f, 2.85f / (band * groundHeight), 5.55f / (band * groundHeight));
        float groundWidth = StageWidth * scale;
        groundHeight *= scale;
        float groundBottom = Horizon - groundHeight * band;
        Fit(groundArt, groundSprite, groundWidth, groundBottom, -400);
        float unit = StageWidth / Mathf.Max(farSprite.rect.width, groundSprite.rect.width);
        float farWidth = farSprite.rect.width * unit, farHeight = farSprite.rect.height * unit;
        float cover = Mathf.Max(26.8f / farWidth, (CameraHeight * 2f + 1.5f) / farHeight, 1f);
        farWidth *= cover; farHeight *= cover;
        float farBottom = CameraHeight + .75f - farHeight;
        if (farBottom > Horizon - .3f) farBottom = Horizon - .3f;
        Fit(farArt, farSprite, farWidth, farBottom, -1000);
        float pixel = groundWidth / groundSprite.rect.width;
        for (int i = 0; i < middleArt.Count; i++) {
            bool used = i < scenery.Length;
            middleArt[i].gameObject.SetActive(used);
            if (!used) continue;
            float width = scenery[i].rect.width * pixel, height = scenery[i].rect.height * pixel;
            float bottom = map.pinToGround[i] ? groundBottom : Horizon - .04f - height * (1f - map.middleBottom[i]);
            // Full-width scenery is cropped at the top of its texture. Push that edge above the camera.
            if (map.pinToGround[i] && width >= groundWidth * .7f) {
                float needTop = CameraHeight + .9f;
                if (bottom + height < needTop) {
                    float grow = (needTop - bottom) / Mathf.Max(.01f, height);
                    width *= grow; height *= grow;
                }
            }
            Fit(middleArt[i], scenery[i], width, bottom, -800 + i * 10);
        }
        RefreshShadows();
        return true;
    }

    public void AddContactShadow(Transform foot, float width) {
        var shadow = foot.Find("Contact shadow")?.GetComponent<SpriteRenderer>();
        if (shadow == null) shadow = Place(foot, "Contact shadow", Resources.Load<Sprite>("Battle/Art/Enchanted/ContactShadow"), Vector2.zero, width, -20);
        shadow.sharedMaterial = Resources.Load<Material>("Battle/ContactShadowMaterial");
        shadow.transform.localScale = new Vector3(width / shadow.sprite.bounds.size.x, .23f / shadow.sprite.bounds.size.y, 1);
        shadow.color = ShadowTint;
    }

    void RefreshShadows() {
        foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name == "Contact shadow") renderer.color = ShadowTint;
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

    SpriteRenderer Paint(Transform parent, string title, int order) {
        var renderer = Anchor(parent, title, Vector2.zero).gameObject.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order; renderer.sharedMaterial = Resources.Load<Material>("Battle/SpriteMaterial"); return renderer;
    }

    static void Fit(SpriteRenderer renderer, Sprite sprite, float width, float bottom, int order) {
        renderer.sprite = sprite; renderer.sortingOrder = order; renderer.color = Color.white;
        float nativeWidth = sprite.rect.width / sprite.pixelsPerUnit, nativeHeight = sprite.rect.height / sprite.pixelsPerUnit;
        float height = width * sprite.rect.height / sprite.rect.width;
        renderer.transform.localScale = new Vector3(width / nativeWidth, height / nativeHeight, 1);
        renderer.transform.localPosition = new Vector3(0, bottom, 0);
    }

    SpriteRenderer Place(Transform parent, string title, Sprite sprite, Vector2 position, float width, int order) {
        var obj = Anchor(parent, title, position).gameObject;
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
        if (sprite == null) { Debug.LogError("Missing stage sprite: " + title, this); return renderer; }
        obj.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        renderer.sharedMaterial = Resources.Load<Material>("Battle/SpriteMaterial"); return renderer;
    }

    void OnDrawGizmosSelected() { DrawSlots(allySlots, Color.cyan); DrawSlots(enemySlots, new Color(1, .5f, .3f)); }
    static void DrawSlots(Transform[] slots, Color color) {
        if (slots == null) return; Gizmos.color = color;
        foreach (var slot in slots) if (slot != null) Gizmos.DrawWireCube(slot.position, new Vector3(1.2f, .4f, .01f));
    }
}
