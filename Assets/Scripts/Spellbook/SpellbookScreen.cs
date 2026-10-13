using System;
using System.Collections.Generic;
using System.Linq;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Magic-book hub, built entirely from code on a fixed 1600x900 stage that is scaled to fit the screen.
//   left page : status panel (rank emblem, stage medallion + progress gauge, team bar, level / bonus / upgrade)
//   right page: two expedition cards + one recruitable hero
//   bottom    : tabs with hanging name plates, status line, help, refresh
public sealed class SpellbookScreen : MonoBehaviour {
    public SkeletonDataAsset dataAsset;
    public Material uiMaterial;

    // ---------- tuning ----------
    const float StageW = 1600f, StageH = 900f;
    const int UpgradeBase = 200, UpgradeStep = 50, RerollCost = 20, TeamMax = 9, LevelsPerRank = 3;

    // panel.png landmarks (normalised, y up) - produced by Tools/process_spellbook_art.py (spellbook_art_report.json)
    const float PanelH = 660f, PanelAspect = 704f / 1073f;
    static readonly Vector2 PanelPos = new Vector2(440f, 500f);
    static readonly Vector4 Hole = new Vector4(.4354f, .685f, .2578f, .1724f);       // cx, cy, rx, ry
    const float TrackIn = .2823f, TrackOut = .3214f;                                  // gauge band radii (fraction of panel width)
    static readonly Vector3 Socket = new Vector3(.0934f, .938f, .0888f);              // cx, cy, rx
    static readonly Vector4 PlaqueText = new Vector4(.7273f, .6375f, .9723f, .7377f); // x0 y0 x1 y1
    static readonly Vector4 Bar = new Vector4(.1271f, .3602f, .7557f, .4045f);
    static readonly Vector4 Plate = new Vector4(.0952f, .034f, .8054f, .3392f);

    // card.png landmarks
    static readonly Vector4 CardHeader = new Vector4(.1f, .893f, .9f, .985f);
    static readonly Vector2 CardNiche = new Vector2(.493f, .615f);
    static readonly Vector4 CardPlate = new Vector4(.13f, .158f, .87f, .368f);
    static readonly Vector4 CardButton = new Vector4(.13f, .034f, .87f, .142f);

    static readonly string[] DangerNames = { "相安无事", "暗流涌动", "危机四伏", "险象环生", "九死一生" };
    static readonly string[] MonsterIcons = { "monster", "slime", "ghost", "skull" };
    static readonly string[] TabNames = { "队伍", "铁匠铺", "仓库", "羁绊" };
    static readonly string[] TabArt = { "tab-team", "tab-smith", "tab-chest", "tab-bond" };
    static readonly string[] TabNotes = { "", "铁匠正在打磨新装备。", "仓库里还是空的。", "队伍之间还没有结下羁绊。" };
    static readonly Color Ink = new Color(.29f, .16f, .08f);
    static readonly Color SoftInk = new Color(.46f, .3f, .17f);
    static readonly Color Gold = new Color(.98f, .86f, .42f);
    static readonly Color Cream = new Color(1f, .95f, .8f);
    static readonly Color Warn = new Color(1f, .35f, .3f);
    static readonly Color Good = new Color(.5f, .9f, .4f);

    sealed class Expedition { public int danger, count, reward, icon; }

    sealed class OfferCard {
        public RectTransform slot, body, content;
        public CanvasGroup group;
        public bool character, sold;
        public Expedition expedition;
        public int profession, price;
        public Text recruitLabel;
        public Button recruitButton;
        public float phase;
    }

    // ---------- state ----------
    Font font;
    AudioSource audioSource;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    RectTransform root, stage, fx, panelRect, bookRoot, notePanel, medFrame, medContent, medView, pointerPivot, rankRect;
    Image goldIcon, upgradeGlow, refreshIcon, fadeOverlay, teamBadge, rankImage, gaugeArc, noteIcon, tabNoteHeader;
    RectTransform upgradeRect, refreshRect, refreshPillRect, teamFill, goldRect, levelRect, bonusRect, gaugeRoot;
    Text levelText, goldText, bonusText, statusText, plaqueTitle, plaqueSub, teamText, rerollCostText, upgradeCostText, noteText, noteTitle, badgeText, rankChip;
    readonly List<Image> gaugeMarks = new List<Image>();
    readonly RectTransform[] tabRects = new RectTransform[4];
    readonly Image[] tabBases = new Image[4];
    readonly Image[] tabIcons = new Image[4];
    readonly PressFeedback[] tabPress = new PressFeedback[4];
    readonly List<OfferCard> cards = new List<OfferCard>();
    int level = 1, gold = 800, shownGold = 800, bonus = 50, team = 1, recruited, tab, shownTier = -1;
    float teamFillShown = 1f / TeamMax, gauge, refreshSpin;
    bool busy, tabReady, heroFitted, leaving;
    Coroutine goldRoll, statusRoutine, gaugeRoutine;
    int UpgradeCost { get { return UpgradeBase + (level - 1) * UpgradeStep; } }
    static int TierOf(int lv) { return Mathf.Clamp((lv - 1) / LevelsPerRank, 0, 4); }
    static Sprite arcSprite;

    // hero card
    RectTransform heroRect, heroStage, shadowRect;
    float[] bootVertices = new float[32];

    // =====================================================================
    // Build
    // =====================================================================
    void Start() {
        Time.timeScale = 1;
        font = Resources.Load<Font>("Battle/Fonts/NotoSansCJKsc-Regular");
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
        var camera = Camera.main;
        if (camera == null) {
            camera = new GameObject("Spellbook Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
        }
        camera.orthographic = true;
        camera.backgroundColor = new Color(.16f, .08f, .03f);
        if (FindObjectOfType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        if (EventSystem.current == null) new GameObject("Spellbook input", typeof(EventSystem), typeof(StandaloneInputModule));
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false; audioSource.spatialBlend = 0f;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;

        var backdrop = Picture("Backdrop", root, null, Vector2.zero, Vector2.one);
        backdrop.color = new Color(.16f, .08f, .03f);
        stage = Pin("Stage", root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(StageW, StageH));
        FitStage();

        BuildPage();
        BuildPanel();
        BuildBook();
        BuildTabs();
        BuildOverlays();
        Refresh(false);
        SelectTab(0);
        tabReady = true;
        PlayIntro();
    }

    void FitStage() {
        if (stage == null) return;
        float s = Mathf.Min(root.rect.width / StageW, root.rect.height / StageH);
        if (s > 0f && !Mathf.Approximately(stage.localScale.x, s)) stage.localScale = new Vector3(s, s, 1f);
    }

    void BuildPage() {
        Picture("Page", stage, Art("page"), Vector2.zero, Vector2.one);

        // title ribbon above the cards
        var ribbon = Pin("Title ribbon", stage, Vector2.zero, new Vector2(1160f, 822f), new Vector2(340f, 340f * 251f / 1164f)).gameObject.AddComponent<Image>();
        ribbon.sprite = Art("ribbon"); ribbon.raycastTarget = false;
        Label(ribbon.transform, "魔法书", .14f, .2f, .86f, .8f, 32, Color.white, TextAnchor.MiddleCenter);

        // gold purse, top right of the right page
        var purse = Pin("Gold purse", stage, Vector2.zero, new Vector2(1432f, 826f), new Vector2(184f, 44f));
        var back = purse.gameObject.AddComponent<Image>();
        back.sprite = UiFx.RoundedFill(); back.type = Image.Type.Sliced; back.pixelsPerUnitMultiplier = 2f;
        back.color = new Color(.16f, .08f, .035f, .94f); back.raycastTarget = false;
        var rim = Picture("Rim", purse, UiFx.RoundedOutline(), Vector2.zero, Vector2.one);
        rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = 2f; rim.color = new Color(.85f, .62f, .25f);
        goldIcon = Picture("Coin", purse, Art("coin"), new Vector2(.03f, .1f), new Vector2(.26f, .9f));
        goldIcon.preserveAspect = true;
        goldText = Label(purse, "", .3f, 0f, .94f, 1f, 24, Gold, TextAnchor.MiddleCenter);
        goldRect = goldText.rectTransform;

        // help
        var help = ButtonOf("Help", stage, Art("help"), "", Vector2.zero, Vector2.zero, ShowHelp, 0f);
        var helpRect = (RectTransform)help.transform;
        helpRect.anchorMin = helpRect.anchorMax = Vector2.zero; helpRect.pivot = new Vector2(.5f, .5f);
        helpRect.anchoredPosition = new Vector2(1262f, 62f); helpRect.sizeDelta = new Vector2(60f, 60f);
        help.GetComponent<Image>().preserveAspect = true;
    }

    void BuildPanel() {
        float pw = PanelH * PanelAspect;
        panelRect = Pin("Status panel", stage, Vector2.zero, PanelPos, new Vector2(pw, PanelH));

        // medallion sits behind panel.png; the opening in the art frames it
        float rx = Hole.z * 1.03f, ry = Hole.w * 1.03f;
        medFrame = Region(panelRect, Hole.x - rx, Hole.y - ry, Hole.x + rx, Hole.y + ry);
        medFrame.name = "Stage medallion";
        var mask = medFrame.gameObject.AddComponent<Image>();
        mask.sprite = UiFx.Disc(); mask.color = Color.white; mask.raycastTarget = false;
        medFrame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        medContent = Region(medFrame, 0, 0, 1, 1);
        medContent.name = "Content";

        Picture("Panel art", panelRect, Art("panel"), Vector2.zero, Vector2.one);

        BuildGauge(pw);
        BuildRank();

        // stage plaque on the right (the "tutorial 1/6" tab in the reference)
        float mid = (PlaqueText.y + PlaqueText.w) * .5f;
        plaqueTitle = Label(panelRect, "", PlaqueText.x, mid - .004f, PlaqueText.z, PlaqueText.w + .004f, 22, Gold, TextAnchor.MiddleCenter);
        plaqueSub = Label(panelRect, "", PlaqueText.x, PlaqueText.y - .004f, PlaqueText.z, mid + .004f, 20, Cream, TextAnchor.MiddleCenter);

        // team capacity in the HP slot
        var slot = Region(panelRect, Bar.x, Bar.y, Bar.z, Bar.w);
        slot.name = "Team bar";
        var fillArea = Region(slot, 0, 0, 1, 1);
        fillArea.offsetMin = new Vector2(3, 3); fillArea.offsetMax = new Vector2(-3, -3);
        var fill = Picture("Fill", fillArea, UiFx.RoundedFill(), Vector2.zero, new Vector2(teamFillShown, 1));
        fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 2f; fill.color = new Color(.9f, .22f, .18f);
        teamFill = fill.rectTransform;
        var shine = Picture("Shine", fillArea, UiFx.RoundedFill(), new Vector2(0f, .55f), new Vector2(1f, .95f));
        shine.type = Image.Type.Sliced; shine.pixelsPerUnitMultiplier = 3f; shine.color = new Color(1f, 1f, 1f, .16f);
        teamText = Label(slot, "", 0f, 0f, 1f, 1f, 20, Color.white, TextAnchor.MiddleCenter);

        // stats + upgrade on the lower plate
        var plate = Region(panelRect, Plate.x, Plate.y, Plate.z, Plate.w);
        plate.name = "Plate";
        levelText = StatRow(plate, .70f, .97f, "魔法书等级", Color.white);
        levelRect = levelText.rectTransform;
        bonusText = StatRow(plate, .42f, .69f, "队伍加成", Good);
        bonusRect = bonusText.rectTransform;
        var divider = Region(plate, .04f, .695f, .96f, .695f); divider.offsetMax = new Vector2(0, 1);
        divider.gameObject.AddComponent<Image>().color = new Color(1f, .9f, .7f, .16f);
        divider = Region(plate, .04f, .415f, .96f, .415f); divider.offsetMax = new Vector2(0, 1);
        divider.gameObject.AddComponent<Image>().color = new Color(1f, .9f, .7f, .16f);

        upgradeGlow = Picture("Upgrade glow", plate, UiFx.Glow(), new Vector2(-.08f, -.12f), new Vector2(1.08f, .46f));
        upgradeGlow.color = new Color(1f, .62f, .15f, 0f);
        float plateH = PanelH * (Plate.w - Plate.y);
        var upgrade = ButtonOf("Upgrade", plate, Art("button-orange"), "升级", new Vector2(.07f, .05f), new Vector2(.93f, .33f), Upgrade, plateH * .28f);
        upgradeRect = (RectTransform)upgrade.transform;
        upgradeCostText = AttachCost(upgrade, "", .08f);
    }

    void BuildGauge(float pw) {
        // progress arc on the dark band of the frame, with a pointer riding its end
        float outer = TrackOut * .985f, outerY = outer * pw / PanelH;
        gaugeArc = Picture("Gauge arc", panelRect, ArcRing(), new Vector2(Hole.x - outer, Hole.y - outerY), new Vector2(Hole.x + outer, Hole.y + outerY));
        gaugeArc.type = Image.Type.Filled;
        gaugeArc.fillMethod = Image.FillMethod.Radial360;
        gaugeArc.fillOrigin = (int)Image.Origin360.Top;
        gaugeArc.fillClockwise = true;
        gaugeArc.fillAmount = 0f;
        gaugeArc.color = new Color(1f, .8f, .32f);

        gaugeRoot = Region(panelRect, Hole.x, Hole.y, Hole.x, Hole.y);
        gaugeRoot.name = "Gauge";
        float rMid = (TrackIn + TrackOut) * .5f * pw;
        for (int i = 0; i < StageProgress.Count; i++) {
            float a = (i + 1) * 360f / StageProgress.Count * Mathf.Deg2Rad;
            var dot = Pin("Stage mark " + (i + 1), gaugeRoot, new Vector2(.5f, .5f), new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * rMid, new Vector2(10f, 10f)).gameObject.AddComponent<Image>();
            dot.sprite = UiFx.Disc(); dot.raycastTarget = false;
            gaugeMarks.Add(dot);
        }
        pointerPivot = Pin("Pointer pivot", gaugeRoot, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        const float pointerH = 54f;
        var pointer = Pin("Pointer", pointerPivot, new Vector2(.5f, .5f), new Vector2(0f, rMid + pointerH * .5f), new Vector2(pointerH * 157f / 302f, pointerH));
        pointer.localRotation = Quaternion.Euler(0, 0, 180f);   // tip toward the medallion
        var image = pointer.gameObject.AddComponent<Image>();
        image.sprite = Art("pointer"); image.raycastTarget = false; image.preserveAspect = true;
    }

    void BuildRank() {
        // the round socket in the top-left corner carries the book's rank emblem
        float sx = Socket.x, sy = Socket.y, r = Socket.z * 1.08f;
        float ry = r * (PanelH * PanelAspect) / PanelH;
        var rect = Region(panelRect, sx - r, sy - ry, sx + r, sy + ry);
        rect.name = "Rank emblem";
        rankRect = rect;
        rankImage = rect.gameObject.AddComponent<Image>();
        rankImage.raycastTarget = false; rankImage.preserveAspect = true;
        var chip = Pin("Level chip", panelRect, new Vector2(sx, sy - ry - .036f), Vector2.zero, new Vector2(60f, 22f));
        var back = chip.gameObject.AddComponent<Image>();
        back.sprite = UiFx.RoundedFill(); back.type = Image.Type.Sliced; back.pixelsPerUnitMultiplier = 2f;
        back.color = new Color(.1f, .05f, .02f, .92f); back.raycastTarget = false;
        var rim = Picture("Rim", chip, UiFx.RoundedOutline(), Vector2.zero, Vector2.one);
        rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = 2f; rim.color = new Color(.85f, .62f, .25f);
        rankChip = Label(chip, "", .08f, 0f, .92f, 1f, 16, Gold, TextAnchor.MiddleCenter);
    }

    void BuildBook() {
        bookRoot = Region(stage, 0, 0, 1, 1);
        bookRoot.name = "Book";
        for (int i = 0; i < 3; i++) cards.Add(MakeCard(i, i == 2));
        cards[0].expedition = new Expedition { danger = 0, count = 1, reward = 200, icon = 0 };
        cards[1].expedition = new Expedition { danger = 0, count = 1, reward = 200, icon = 1 };
        cards[2].profession = 6; cards[2].price = 100;
        foreach (var card in cards) FillCard(card);

        // refresh: round gem + cost pill in the bottom bar, where the thumb rests
        refreshPillRect = Pin("Refresh pill", bookRoot, Vector2.zero, new Vector2(1390f, 60f), new Vector2(112f, 38f));
        var pill = refreshPillRect.gameObject.AddComponent<Image>();
        pill.sprite = UiFx.RoundedFill(); pill.type = Image.Type.Sliced; pill.pixelsPerUnitMultiplier = 2f;
        pill.color = new Color(.14f, .07f, .03f, .94f);
        var pillButton = refreshPillRect.gameObject.AddComponent<Button>();
        pillButton.transition = Selectable.Transition.None; pillButton.targetGraphic = pill;
        pillButton.onClick.AddListener(Reroll);
        refreshPillRect.gameObject.AddComponent<PressFeedback>();
        var rim = Picture("Rim", refreshPillRect, UiFx.RoundedOutline(), Vector2.zero, Vector2.one);
        rim.type = Image.Type.Sliced; rim.pixelsPerUnitMultiplier = 2f; rim.color = new Color(.85f, .62f, .25f);
        var coin = Picture("Coin", refreshPillRect, Art("coin"), new Vector2(.07f, .14f), new Vector2(.3f, .86f));
        coin.preserveAspect = true;
        rerollCostText = Label(refreshPillRect, RerollCost.ToString(), .33f, 0f, .76f, 1f, 22, Gold, TextAnchor.MiddleCenter);

        var icon = Pin("Refresh", bookRoot, Vector2.zero, new Vector2(1478f, 62f), new Vector2(92f, 92f)).gameObject.AddComponent<Image>();
        icon.sprite = Art("refresh"); icon.preserveAspect = true; icon.raycastTarget = true;
        refreshIcon = icon;
        refreshRect = icon.rectTransform;
        var iconButton = icon.gameObject.AddComponent<Button>();
        iconButton.transition = Selectable.Transition.None; iconButton.targetGraphic = icon;
        iconButton.onClick.AddListener(Reroll);
        icon.gameObject.AddComponent<PressFeedback>().pressedScale = .86f;

        // status line between the tabs and the buttons
        statusText = Label(stage, "", 0f, 0f, 1f, 1f, 22, Cream, TextAnchor.MiddleLeft);
        var statusRect = statusText.rectTransform;
        statusRect.anchorMin = statusRect.anchorMax = Vector2.zero;
        statusRect.pivot = new Vector2(0, .5f);
        statusRect.anchoredPosition = new Vector2(600f, 60f); statusRect.sizeDelta = new Vector2(630f, 44f);

        // placeholder card for the non-book tabs
        notePanel = Pin("Tab note", stage, Vector2.zero, new Vector2(1188f, 470f), new Vector2(300f, 300f / (658f / 985f)));
        var note = notePanel.gameObject.AddComponent<Image>();
        note.sprite = Art("card"); note.raycastTarget = false;
        noteTitle = Label(notePanel, "", CardHeader.x, CardHeader.y, CardHeader.z, CardHeader.w, 28, Cream, TextAnchor.MiddleCenter);
        noteIcon = Picture("Icon", notePanel, null, new Vector2(CardNiche.x - .2f, CardNiche.y - .13f), new Vector2(CardNiche.x + .2f, CardNiche.y + .13f));
        noteIcon.preserveAspect = true; noteIcon.color = Color.white;
        noteText = Label(notePanel, "", CardPlate.x + .03f, CardPlate.y, CardPlate.z - .03f, CardPlate.w, 24, Ink, TextAnchor.MiddleCenter);
        notePanel.gameObject.SetActive(false);
    }

    void BuildTabs() {
        const float w = 96f, h = 136f;
        for (int i = 0; i < TabNames.Length; i++) {
            int index = i;
            var rect = Pin("Tab " + TabNames[i], stage, Vector2.zero, new Vector2(150f + i * 110f, 2f), new Vector2(w, h));
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(150f + i * 110f, 2f);
            tabRects[i] = rect;
            var baseImage = rect.gameObject.AddComponent<Image>();
            baseImage.sprite = Art("tab-base"); baseImage.raycastTarget = true;
            tabBases[i] = baseImage;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = baseImage;
            button.onClick.AddListener(() => SelectTab(index));
            tabPress[i] = rect.gameObject.AddComponent<PressFeedback>();
            tabPress[i].pressedScale = .92f; tabPress[i].hoverScale = 1.03f;
            var icon = Picture("Icon", rect, Art(TabArt[i]), new Vector2(.15f, .33f), new Vector2(.85f, .87f));
            icon.preserveAspect = true;
            tabIcons[i] = icon;
            Label(rect, TabNames[i], .1f, .05f, .74f, .21f, 15, Ink, TextAnchor.MiddleCenter);
        }
        // small red badge on the team tab that counts recruits
        var badge = Picture("Team badge", tabRects[0], UiFx.Disc(), new Vector2(.7f, .76f), new Vector2(1.14f, 1.08f));
        badge.color = new Color(.9f, .2f, .18f);
        badge.gameObject.SetActive(false);
        teamBadge = badge;
        badgeText = Label(badge.transform, "", 0f, 0f, 1f, 1f, 16, Color.white, TextAnchor.MiddleCenter);
    }

    void BuildOverlays() {
        fx = Region(stage, 0, 0, 1, 1);
        fx.name = "FX";
        fx.pivot = new Vector2(.5f, .5f);
        fadeOverlay = Picture("Fade", root, null, Vector2.zero, Vector2.one);
        fadeOverlay.color = new Color(0, 0, 0, 0);
        fadeOverlay.gameObject.SetActive(false);
    }

    OfferCard MakeCard(int index, bool character) {
        const float w = 224f, h = 380f, gap = 12f, x0 = 840f, cy = 470f;
        var card = new OfferCard { character = character };
        card.slot = Pin(character ? "Character card" : "Adventure card " + (index + 1), bookRoot, Vector2.zero, new Vector2(x0 + w * .5f + index * (w + gap), cy), new Vector2(w, h));
        card.group = card.slot.gameObject.AddComponent<CanvasGroup>();
        var back = Picture("Body", card.slot, Art("card"), Vector2.zero, Vector2.one);
        card.body = back.rectTransform;
        card.content = Region(card.body, 0, 0, 1, 1);
        card.content.name = "Content";
        card.phase = index * 1.3f;
        return card;
    }

    // =====================================================================
    // Card content
    // =====================================================================
    void FillCard(OfferCard card) {
        for (int i = card.content.childCount - 1; i >= 0; i--) {
            var child = card.content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        card.recruitLabel = null; card.recruitButton = null; card.sold = false;
        if (card.character) FillCharacter(card); else FillAdventure(card);
    }

    void FillAdventure(OfferCard card) {
        var e = card.expedition;
        var c = card.content;
        Label(c, "冒险", CardHeader.x, CardHeader.y, CardHeader.z, CardHeader.w, 24, Cream, TextAnchor.MiddleCenter);
        var glow = Picture("Glow", c, UiFx.Glow(), new Vector2(CardNiche.x - .25f, CardNiche.y - .15f), new Vector2(CardNiche.x + .25f, CardNiche.y + .15f));
        glow.color = new Color(1f, .8f, .45f, .5f);
        var monster = Picture("Monster", c, Art(MonsterIcons[e.icon % MonsterIcons.Length]), new Vector2(CardNiche.x - .24f, CardNiche.y - .125f), new Vector2(CardNiche.x + .24f, CardNiche.y + .125f));
        monster.preserveAspect = true;
        Label(c, "x" + e.count, CardNiche.x + .12f, CardNiche.y - .165f, CardNiche.x + .34f, CardNiche.y - .09f, 18, Cream, TextAnchor.MiddleCenter);

        var plate = Region(c, CardPlate.x, CardPlate.y, CardPlate.z, CardPlate.w);
        plate.name = "Plate";
        Label(plate, DangerNames[e.danger], .04f, .54f, .96f, .98f, 20, Ink, TextAnchor.MiddleCenter);
        Chip(plate, Art("coin"), e.reward.ToString(), .02f, .04f, .5f, .5f);
        Chip(plate, Art("skull"), e.danger.ToString(), .52f, .04f, .98f, .5f);
        var button = ButtonOf("Start battle", c, Art("button-stone"), "开始战斗", new Vector2(CardButton.x, CardButton.y), new Vector2(CardButton.z, CardButton.w), StartBattle, 380f * (CardButton.w - CardButton.y));
        button.GetComponentInChildren<Text>().color = Color.white;
    }

    void FillCharacter(OfferCard card) {
        var c = card.content;
        var profession = HeroProfession.All[card.profession];
        Label(c, "角色", CardHeader.x, CardHeader.y, CardHeader.z, CardHeader.w, 24, Cream, TextAnchor.MiddleCenter);
        var glow = Picture("Glow", c, UiFx.Glow(), new Vector2(CardNiche.x - .25f, CardNiche.y - .15f), new Vector2(CardNiche.x + .25f, CardNiche.y + .15f));
        glow.color = new Color(1f, .8f, .45f, .5f);
        var stageRect = Region(c, .02f, .455f, .98f, .9f);
        stageRect.name = "Hero stage";
        var shadow = Picture("Contact shadow", stageRect, UiFx.Disc(), new Vector2(.46f, 0f), new Vector2(.54f, .02f));
        shadow.color = new Color(0, 0, 0, .35f);
        shadowRect = shadow.rectTransform;
        PlaceHero(stageRect, profession);

        var plate = Region(c, CardPlate.x, CardPlate.y, CardPlate.z, CardPlate.w);
        plate.name = "Plate";
        Label(plate, profession.title, .04f, .5f, .96f, .98f, 20, Ink, TextAnchor.MiddleCenter);
        Label(plate, profession.description, .05f, .03f, .95f, .52f, 14, SoftInk, TextAnchor.MiddleCenter);
        var button = ButtonOf("Recruit", c, Art("button-stone"), "购买", new Vector2(CardButton.x, CardButton.y), new Vector2(CardButton.z, CardButton.w), () => Recruit(card), 380f * (CardButton.w - CardButton.y));
        card.recruitButton = button;
        card.recruitLabel = button.GetComponentInChildren<Text>();
        card.recruitLabel.color = Color.white;
        AttachCost(button, card.price.ToString(), .06f);
    }

    // icon + value pair inside a plate
    void Chip(Transform parent, Sprite icon, string value, float x0, float y0, float x1, float y1) {
        float iw = (x1 - x0) * .36f;
        var image = Picture(value, parent, icon, new Vector2(x0, y0 + .04f), new Vector2(x0 + iw, y1 - .04f));
        image.preserveAspect = true;
        Label(parent, value, x0 + iw + .02f, y0, x1, y1, 19, Ink, TextAnchor.MiddleLeft);
    }

    // label on the left, value on the right
    Text StatRow(Transform parent, float y0, float y1, string label, Color valueColor) {
        Label(parent, label, .05f, y0, .58f, y1, 19, Cream, TextAnchor.MiddleLeft);
        return Label(parent, "", .5f, y0, .95f, y1, 22, valueColor, TextAnchor.MiddleRight);
    }

    // turns a stone/orange button into "caption ........ [coin] cost"
    Text AttachCost(Button button, string value, float pad) {
        var caption = button.GetComponentInChildren<Text>();
        caption.rectTransform.anchorMin = new Vector2(pad + .02f, .08f); caption.rectTransform.anchorMax = new Vector2(.5f, .92f);
        caption.alignment = TextAnchor.MiddleLeft;
        var coin = Picture("Cost coin", button.transform, Art("coin"), new Vector2(.53f, .2f), new Vector2(.67f, .8f));
        coin.preserveAspect = true;
        return Label(button.transform, value, .69f, .08f, .94f, .92f, 22, Color.white, TextAnchor.MiddleLeft);
    }

    // =====================================================================
    // Hero (Spine) on the character card
    // =====================================================================
    void PlaceHero(RectTransform area, HeroProfession profession) {
        heroRect = null; heroStage = null; heroFitted = false;
        if (dataAsset == null || dataAsset.GetSkeletonData(false) == null || uiMaterial == null) {
            Label(area, "角色资源未加载", 0f, 0f, 1f, 1f, 16, Ink, TextAnchor.MiddleCenter);
            return;
        }
        var heroObject = new GameObject("Hero idle " + profession.id, typeof(RectTransform));
        heroObject.SetActive(false);
        heroObject.transform.SetParent(area, false);
        var rect = heroObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(.5f, .02f);
        rect.anchorMax = new Vector2(.5f, .02f);
        rect.pivot = new Vector2(.5f, 0);
        rect.localScale = Vector3.zero;
        heroRect = rect;
        heroStage = area;
        var graphic = heroObject.AddComponent<SkeletonGraphic>();
        graphic.allowMultipleCanvasRenderers = true;
        graphic.material = uiMaterial;
        graphic.skeletonDataAsset = dataAsset;
        graphic.raycastTarget = false;
        graphic.initialFlipX = true;
        heroObject.SetActive(true);
        if (!graphic.IsValid) graphic.Initialize(true);
        var skeleton = graphic.Skeleton;
        if (skeleton == null) {
            Label(area, "角色资源未加载", 0f, 0f, 1f, 1f, 16, Ink, TextAnchor.MiddleCenter);
            return;
        }
        var data = skeleton.Data;
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
        var combined = new Skin("spellbook-" + profession.id);
        if (data.DefaultSkin != null) combined.AddSkin(data.DefaultSkin);
        foreach (string skinName in selected.Values) {
            var skin = data.FindSkin(skinName);
            if (skin != null) combined.AddSkin(skin);
        }
        skeleton.SetSkin(combined);
        skeleton.SetToSetupPose();
        skeleton.ScaleX = -1;
        string group = profession.animationGroup + "/";
        var animation = data.Animations.FirstOrDefault(a => a.Name.StartsWith(group + "001A_idle", StringComparison.Ordinal) && a.Name.IndexOf("backup", StringComparison.OrdinalIgnoreCase) < 0);
        if (animation != null) graphic.AnimationState.SetAnimation(0, animation, true);
    }

    void LateUpdate() {
        FitStage();
        if (heroFitted || heroRect == null || heroStage == null || heroStage.rect.height < 2f) return;
        var graphic = heroRect.GetComponent<SkeletonGraphic>();
        if (graphic != null && graphic.IsValid) graphic.MatchRectTransformWithBounds();
        float height = Mathf.Max(1f, heroRect.sizeDelta.y);
        heroRect.localScale = Vector3.one * (heroStage.rect.height * .82f / height);
        StickShadow(graphic);
        heroFitted = true;
    }

    void StickShadow(SkeletonGraphic graphic) {
        if (shadowRect == null || graphic == null || !graphic.IsValid) return;
        var skeleton = graphic.Skeleton;
        if (!BootSole(skeleton.FindSlot("armor_footF"), out var front) || !BootSole(skeleton.FindSlot("armor_footB"), out var back)) return;
        var sole = new Vector2((front.center.x + back.center.x) * .5f, Mathf.Min(front.min.y, back.min.y));
        shadowRect.anchorMin = shadowRect.anchorMax = new Vector2(.5f, .5f);
        shadowRect.pivot = new Vector2(.5f, .5f);
        shadowRect.position = heroRect.TransformPoint(sole);
        float width = (Mathf.Abs(front.center.x - back.center.x) + front.size.x * .5f + back.size.x * .5f) * heroRect.localScale.x;
        shadowRect.sizeDelta = new Vector2(Mathf.Max(28f, width), Mathf.Max(8f, width * .22f));
    }

    bool BootSole(Slot slot, out Bounds bounds) {
        bounds = default;
        if (slot == null || !slot.Bone.Active) return false;
        int count;
        if (slot.Attachment is RegionAttachment region) {
            count = 8;
            region.ComputeWorldVertices(slot, bootVertices, 0);
        } else if (slot.Attachment is MeshAttachment mesh) {
            count = mesh.WorldVerticesLength;
            if (bootVertices.Length < count) bootVertices = new float[count];
            mesh.ComputeWorldVertices(slot, 0, count, bootVertices, 0);
        } else return false;
        if (count < 2) return false;
        bounds = new Bounds(new Vector3(bootVertices[0], bootVertices[1], 0), Vector3.zero);
        for (int i = 2; i < count; i += 2) bounds.Encapsulate(new Vector3(bootVertices[i], bootVertices[i + 1], 0));
        return true;
    }

    // =====================================================================
    // Stage medallion + gauge (fixed by campaign progress)
    // =====================================================================
    void ShowStage(int index, bool animate) {
        var map = StageProgress.MapAt(index);
        plaqueTitle.text = "第 " + (index + 1) + "/" + StageProgress.Count + " 关";
        plaqueSub.text = map.title;
        for (int i = 0; i < gaugeMarks.Count; i++)
            gaugeMarks[i].color = i <= index ? new Color(1f, .96f, .75f) : new Color(.85f, .75f, .55f, .55f);
        SetGauge((index + 1f) / StageProgress.Count, animate);

        var view = MapPreview.Build(medContent, map, 1f);
        var old = medView;
        medView = view;
        if (!animate || old == null) { if (old != null) Destroy(old.gameObject); return; }
        var group = view.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        UiFx.Tween(this, .8f, u => {
            if (view == null) return;
            group.alpha = Mathf.Clamp01(u * 1.6f);
            view.localScale = Vector3.one * Mathf.Lerp(1.22f, 1f, UiFx.OutCubic(u));
        }, () => { if (old != null) Destroy(old.gameObject); });
    }

    void SetGauge(float value, bool animate) {
        if (gaugeRoutine != null) StopCoroutine(gaugeRoutine);
        if (!animate) { ApplyGauge(value); return; }
        float from = gauge;
        gaugeRoutine = UiFx.Tween(this, 1f, u => ApplyGauge(Mathf.LerpUnclamped(from, value, UiFx.OutBack(u, 1.1f))), () => ApplyGauge(value));
    }

    void ApplyGauge(float value) {
        gauge = value;
        gaugeArc.fillAmount = Mathf.Clamp01(value);
        pointerPivot.localRotation = Quaternion.Euler(0, 0, -360f * value);
    }

    void PlayIntro() {
        int target = StageProgress.Index, shown = StageProgress.ShownIndex;
        if (shown >= 0 && shown < target) {
            // a stage was cleared since the book was last open: show the old map, then unlock the next one
            ShowStage(shown, false);
            UiFx.After(this, 1.1f, () => {
                ShowStage(target, true);
                var c = UiFx.LocalCenter(fx, medFrame);
                UiFx.Pulse(this, fx, UiFx.Ring(), c, new Color(1f, .9f, .5f, .9f), 160f, 360f, .7f);
                UiFx.Pulse(this, fx, UiFx.Glow(), c, new Color(1f, .95f, .7f, .8f), 160f, 400f, .6f);
                UiFx.Burst(this, fx, c, 20, UiFx.Sparkle(), new Color(1f, .95f, .6f, 1f), new Color(1f, .6f, .2f, 0f), 340f, 1f, 28f, 120f);
                UiFx.FloatText(this, fx, font, "第 " + (target + 1) + " 关 已解锁", c + new Vector2(0, -20), Gold, 34, 60f, 1.4f);
                Sfx("victory", .7f);
            });
        } else ShowStage(target, false);
        StageProgress.ShownIndex = target;

        // cards deal in one after another
        for (int i = 0; i < cards.Count; i++) {
            var card = cards[i];
            SetFlip(card, 0f, 1f);
            card.group.alpha = 1f;
            FlipIn(card, .25f + i * .11f, null);
        }
    }

    // =====================================================================
    // Card flip transition
    // =====================================================================
    void SetFlip(OfferCard card, float scaleX, float lift) {
        card.slot.localScale = new Vector3(Mathf.Max(.001f, scaleX), 1f + .05f * lift, 1f);
        card.slot.localRotation = Quaternion.Euler(0, 0, 3.5f * lift * (card.character ? -1f : 1f));
    }

    void FlipOut(OfferCard card, float delay, Action done) {
        UiFx.Tween(this, .17f, u => SetFlip(card, 1f - UiFx.InCubic(u), u), done, delay);
    }

    void FlipIn(OfferCard card, float delay, Action done) {
        UiFx.Tween(this, .36f, u => {
            float e = UiFx.OutBack(u, 1.5f);
            SetFlip(card, e, 1f - UiFx.OutCubic(u));
        }, () => {
            card.slot.localScale = Vector3.one; card.slot.localRotation = Quaternion.identity;
            CardLanded(card);
            if (done != null) done();
        }, delay);
    }

    void CardLanded(OfferCard card) {
        if (!card.slot.gameObject.activeInHierarchy) return;
        var c = UiFx.LocalCenter(fx, card.slot);
        var top = c + new Vector2(0, card.slot.rect.height * .5f - 30f);
        UiFx.Burst(this, fx, top, 8, UiFx.Sparkle(), new Color(1f, .95f, .7f, 1f), new Color(1f, .7f, .3f, 0f), 260f, .6f, 24f, 200f, 90f, 150f);
        var flash = Picture("Flash", card.body, UiFx.RoundedFill(), Vector2.zero, Vector2.one);
        flash.type = Image.Type.Sliced; flash.pixelsPerUnitMultiplier = 3f; flash.color = new Color(1, 1, 1, .55f);
        UiFx.Tween(this, .35f, u => { if (flash != null) flash.color = new Color(1, 1, .9f, .5f * (1f - u)); }, () => { if (flash != null) Destroy(flash.gameObject); });
    }

    // =====================================================================
    // Actions
    // =====================================================================
    void Reroll() {
        if (busy || leaving) return;
        Sfx("ui-click");
        var from = UiFx.LocalCenter(fx, refreshRect);
        if (gold < RerollCost) {
            UiFx.Shake(this, refreshPillRect, 9f);
            Flash(rerollCostText, Warn);
            UiFx.FloatText(this, fx, font, "金币不足", from + new Vector2(-30, 70), Warn, 28, 50f);
            SetStatus("金币不足，刷新需要 " + RerollCost + "。");
            return;
        }
        busy = true;
        Sfx("dice-roll", .9f);
        // spin the gem and spend the coins
        float start = refreshSpin;
        refreshSpin -= 360f;
        float end = refreshSpin;
        UiFx.Tween(this, .7f, u => { if (refreshRect != null) refreshRect.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(start, end, UiFx.OutCubic(u))); });
        UiFx.Pulse(this, fx, UiFx.Glow(), from, new Color(.5f, 1f, 1f, .8f), 90f, 250f, .45f);
        UiFx.Burst(this, fx, from, 10, UiFx.Sparkle(), new Color(.7f, 1f, 1f, 1f), new Color(.2f, .8f, 1f, 0f), 260f, .6f, 24f, 0f);
        SpendGold(RerollCost, from, "-" + RerollCost);
        // roll new offers, remember what was there so they always change
        var prev = cards.Select(c => c.character ? c.profession : c.expedition.danger).ToArray();
        for (int i = 0; i < cards.Count; i++) {
            var card = cards[i];
            if (card.sold) continue;
            int index = i;
            FlipOut(card, i * .09f, () => {
                Roll(card, prev[index]);
                FillCard(card);
                FlipIn(card, 0f, null);
            });
        }
        UiFx.After(this, .17f + .09f * 2 + .45f, () => { busy = false; Refresh(true); });
        SetStatus("魔法书翻开了新的一页。");
    }

    void Roll(OfferCard card, int previous) {
        if (card.character) {
            int next;
            do next = UnityEngine.Random.Range(0, HeroProfession.All.Length); while (next == previous && HeroProfession.All.Length > 1);
            card.profession = next;
            card.price = 90 + (next % 4) * 20;
        } else {
            int danger;
            do danger = UnityEngine.Random.Range(0, DangerNames.Length); while (danger == previous);
            card.expedition = new Expedition {
                danger = danger, count = Mathf.Clamp(1 + danger / 2 + UnityEngine.Random.Range(0, 2), 1, 4),
                reward = 200 + danger * 100, icon = UnityEngine.Random.Range(0, MonsterIcons.Length)
            };
        }
    }

    void Upgrade() {
        if (busy || leaving) return;
        Sfx("ui-click");
        var button = UiFx.LocalCenter(fx, upgradeRect);
        int cost = UpgradeCost;
        if (gold < cost) {
            UiFx.Shake(this, upgradeRect, 10f);
            Flash(goldText, Warn);
            Pop(goldRect, 1.3f);
            UiFx.FloatText(this, fx, font, "需要 " + cost + " 金币", button + new Vector2(0, 60), Warn, 28, 50f);
            SetStatus("金币不足，升级需要 " + cost + "。");
            return;
        }
        SpendGold(cost, UiFx.LocalCenter(fx, goldRect), "");
        bool rankUp = TierOf(level + 1) > TierOf(level);
        level++; bonus += 5;
        Refresh(true);
        PlayUpgradeFx(button, rankUp);
        SetStatus(rankUp ? "魔法书进阶了！" : "等级提升到 " + level + "，加成 +" + bonus + "%。");
    }

    void PlayUpgradeFx(Vector2 button, bool rankUp) {
        Sfx("arcane-cast", .8f, 1.1f);
        var press = upgradeRect.GetComponent<PressFeedback>();
        if (press != null) press.Punch(5f);
        // rings, flash and rising sparkles from the button
        UiFx.Pulse(this, fx, UiFx.Ring(), button, new Color(1f, .85f, .4f, .95f), 70f, 380f, .62f);
        UiFx.Pulse(this, fx, UiFx.Ring(), button, new Color(1f, .6f, .2f, .85f), 50f, 280f, .5f, .1f);
        UiFx.Pulse(this, fx, UiFx.Glow(), button, new Color(1f, .75f, .25f, .95f), 140f, 520f, .5f);
        UiFx.Burst(this, fx, button, 26, UiFx.Sparkle(), new Color(1f, .95f, .55f, 1f), new Color(1f, .5f, .1f, 0f), 520f, .95f, 34f, 480f, 90f, 130f);
        UiFx.Burst(this, fx, button, 12, UiFx.Glow(), new Color(1f, .85f, .4f, .9f), new Color(1f, .5f, .1f, 0f), 280f, .7f, 28f, 120f, 90f, 160f);
        // light pillar through the medallion
        var med = UiFx.LocalCenter(fx, medFrame);
        var beam = UiFx.Add(fx, "pillar", UiFx.Glow(), new Color(1f, .9f, .55f, 0f), new Vector2(button.x, (button.y + med.y) * .5f), new Vector2(240f, 560f));
        UiFx.Tween(this, .7f, u => {
            if (beam == null) return;
            float a = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            beam.color = new Color(1f, .9f, .55f, .75f * a);
            beam.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(280f, 120f, u), 560f);
        }, () => { if (beam != null) Destroy(beam.gameObject); });
        UiFx.Pulse(this, fx, UiFx.Glow(), med, new Color(1f, .95f, .75f, .8f), 220f, 380f, .5f, .08f);
        UiFx.Burst(this, fx, med, 14, UiFx.Sparkle(), new Color(1f, .95f, .7f, 1f), new Color(1f, .7f, .2f, 0f), 300f, 1f, 26f, 150f, 90f, 360f);
        // the rank emblem and numbers react
        var socket = UiFx.LocalCenter(fx, rankRect);
        UiFx.Pulse(this, fx, UiFx.Ring(), socket, new Color(1f, .9f, .5f, .95f), 70f, rankUp ? 300f : 170f, .6f, .1f);
        Pop(rankRect, rankUp ? 1.9f : 1.35f);
        if (rankUp) {
            Sfx("victory", .7f);
            UiFx.Pulse(this, fx, UiFx.Glow(), socket, new Color(1f, .95f, .7f, .95f), 90f, 360f, .6f);
            UiFx.Burst(this, fx, socket, 22, UiFx.Sparkle(), new Color(1f, .95f, .6f, 1f), new Color(1f, .6f, .2f, 0f), 380f, 1f, 28f, 120f);
            UiFx.FloatText(this, fx, font, "魔法书进阶！", socket + new Vector2(120, -30), Gold, 34, 60f, 1.5f);
        } else {
            UiFx.FloatText(this, fx, font, "等级 " + level, med + new Vector2(0, 40), Gold, 40, 90f, 1.3f);
        }
        UiFx.FloatText(this, fx, font, "+5%", UiFx.LocalCenter(fx, bonusRect) + new Vector2(0, 10), Good, 30, 60f, 1.1f);
        Pop(levelRect, 1.6f); Flash(levelText, Gold);
        Pop(bonusRect, 1.35f); Flash(bonusText, Color.white);
        Pop(upgradeRect, 1.1f);
        UiFx.Tween(this, .4f, u => { if (panelRect != null) panelRect.localScale = Vector3.one * (1f + .02f * Mathf.Sin(u * Mathf.PI)); }, () => { if (panelRect != null) panelRect.localScale = Vector3.one; });
    }

    void Recruit(OfferCard card) {
        if (busy || leaving || card.sold) return;
        Sfx("ui-click");
        var button = UiFx.LocalCenter(fx, (RectTransform)card.recruitButton.transform);
        if (team >= TeamMax) {
            UiFx.Shake(this, (RectTransform)card.recruitButton.transform, 8f);
            UiFx.FloatText(this, fx, font, "队伍已满", button + new Vector2(0, 60), Warn, 28, 50f);
            SetStatus("队伍已满，最多 " + TeamMax + " 人。");
            return;
        }
        if (gold < card.price) {
            UiFx.Shake(this, (RectTransform)card.recruitButton.transform, 8f);
            Flash(goldText, Warn);
            Pop(goldRect, 1.3f);
            UiFx.FloatText(this, fx, font, "需要 " + card.price + " 金币", button + new Vector2(0, 60), Warn, 28, 50f);
            SetStatus("金币不足，招募需要 " + card.price + "。");
            return;
        }
        busy = true; card.sold = true;
        var profession = HeroProfession.All[card.profession];
        int price = card.price;
        SpendGold(price, UiFx.LocalCenter(fx, goldRect), "", button);
        card.recruitLabel.text = "成交！";
        card.recruitButton.interactable = false;

        UiFx.After(this, .4f, () => {
            if (card.slot == null) return;
            Sfx("victory", .8f);
            var c = UiFx.LocalCenter(fx, card.slot);
            UiFx.Pulse(this, fx, UiFx.Glow(), c, new Color(1f, .9f, .5f, .95f), 160f, 520f, .55f);
            UiFx.Pulse(this, fx, UiFx.Ring(), c, new Color(1f, .85f, .4f, .95f), 100f, 480f, .6f);
            UiFx.Pulse(this, fx, UiFx.Ring(), c, new Color(1f, .6f, .3f, .8f), 60f, 360f, .5f, .1f);
            UiFx.Burst(this, fx, c, 34, UiFx.Sparkle(), new Color(1f, .95f, .6f, 1f), new Color(1f, .45f, .15f, 0f), 600f, 1.2f, 34f, 400f, 90f, 360f);
            UiFx.Burst(this, fx, c, 18, UiFx.Disc(), new Color(.4f, .85f, 1f, 1f), new Color(.7f, .4f, 1f, 0f), 480f, 1.1f, 14f, 480f, 90f, 360f);
            Cheer();
            Stamp(card);
            if (card.recruitLabel != null) card.recruitLabel.text = "已入队";
            SetStatus(profession.title + "加入了队伍！");
        });
        UiFx.After(this, 1.35f, () => FlyToTeam(card, profession));
    }

    void Cheer() {
        if (heroRect == null) return;
        var rect = heroRect;
        var baseScale = rect.localScale;
        var basePos = rect.anchoredPosition;
        UiFx.Tween(this, .7f, u => {
            if (rect == null) return;
            float hop = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u);
            rect.anchoredPosition = basePos + new Vector2(0, hop * 40f);
            float squash = Mathf.Sin(u * Mathf.PI * 4f) * (1f - u) * .08f;
            rect.localScale = new Vector3(baseScale.x * (1f - squash), baseScale.y * (1f + squash), baseScale.z);
        }, () => { if (rect != null) { rect.localScale = baseScale; rect.anchoredPosition = basePos; } });
    }

    void Stamp(OfferCard card) {
        var stamp = Pin("Stamp", card.content, new Vector2(.5f, .27f), Vector2.zero, new Vector2(190f, 76f));
        stamp.localRotation = Quaternion.Euler(0, 0, -12f);
        var group = stamp.gameObject.AddComponent<CanvasGroup>();
        var plate = Picture("Plate", stamp, UiFx.RoundedFill(), Vector2.zero, Vector2.one);
        plate.type = Image.Type.Sliced; plate.pixelsPerUnitMultiplier = 2f; plate.color = new Color(.35f, .06f, .04f, .82f);
        var frame = Picture("Frame", stamp, UiFx.RoundedOutline(), Vector2.zero, Vector2.one);
        frame.type = Image.Type.Sliced; frame.pixelsPerUnitMultiplier = 1.4f; frame.color = new Color(1f, .45f, .25f);
        Label(stamp, "已入队", .06f, 0f, .94f, 1f, 42, new Color(1f, .6f, .35f), TextAnchor.MiddleCenter);
        UiFx.Tween(this, .28f, u => {
            if (stamp == null) return;
            group.alpha = Mathf.Clamp01(u * 5f);
            stamp.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, UiFx.OutBack(u, 1.2f));
        }, () => {
            if (stamp == null) return;
            stamp.localScale = Vector3.one;
            Sfx("wood-impact", .9f, .9f);
            UiFx.Shake(this, card.body, 7f, .3f);
            UiFx.Burst(this, fx, UiFx.LocalCenter(fx, stamp), 10, UiFx.Disc(), new Color(1f, .8f, .5f, .8f), new Color(.6f, .4f, .2f, 0f), 260f, .5f, 14f, 0f, 90f, 360f, 3f);
        });
    }

    void FlyToTeam(OfferCard card, HeroProfession profession) {
        var slot = card.slot;
        var teamTab = tabRects[0];
        Vector3 from = slot.position, to = teamTab.TransformPoint(new Vector3(0, teamTab.rect.height * .5f, 0));
        Vector3 mid = (from + to) * .5f + new Vector3(0, 160f * fx.lossyScale.y, 0);
        var min = slot.offsetMin; var max = slot.offsetMax;
        float trail = 0f;
        Sfx("sword-swing", .5f, 1.4f);
        UiFx.Tween(this, .62f, u => {
            if (slot == null) return;
            float e = UiFx.InCubic(u);
            Vector3 p = Mathf.Pow(1 - e, 2) * from + 2 * (1 - e) * e * mid + e * e * to;
            slot.position = p;
            float s = Mathf.Lerp(1f, .14f, UiFx.InOutCubic(u));
            slot.localScale = new Vector3(s, s, 1f);
            slot.localRotation = Quaternion.Euler(0, 0, 28f * e);
            card.group.alpha = 1f - Mathf.Clamp01((u - .75f) * 4f);
            trail += Time.unscaledDeltaTime;
            if (trail > .03f) {
                trail = 0f;
                UiFx.Burst(this, fx, fx.InverseTransformPoint(p), 1, UiFx.Sparkle(), new Color(1f, .95f, .6f, 1f), new Color(.5f, .8f, 1f, 0f), 60f, .5f, 30f * s + 10f);
            }
        }, () => {
            var c = UiFx.LocalCenter(fx, teamTab) + new Vector2(0, teamTab.rect.height * .5f);
            Sfx("dice-lock", .9f, 1.3f);
            tabPress[0].Punch(7f);
            UiFx.Pulse(this, fx, UiFx.Glow(), c, new Color(.5f, .85f, 1f, .95f), 80f, 260f, .5f);
            UiFx.Pulse(this, fx, UiFx.Ring(), c, new Color(.7f, .9f, 1f, .9f), 60f, 220f, .45f);
            UiFx.Burst(this, fx, c, 14, UiFx.Sparkle(), new Color(.8f, .95f, 1f, 1f), new Color(.4f, .6f, 1f, 0f), 340f, .8f, 26f, 200f);
            team++; recruited++;
            UiFx.FloatText(this, fx, font, "+1 队员", c + new Vector2(0, 60), new Color(.7f, .95f, 1f), 30, 70f, 1.2f);
            Refresh(true);
            Pop(teamBadge.rectTransform, 1.6f);
            // slot returns with a new candidate flipping in
            slot.offsetMin = min; slot.offsetMax = max;
            slot.localScale = new Vector3(.001f, 1f, 1f); slot.localRotation = Quaternion.identity;
            card.group.alpha = 1f;
            Roll(card, card.profession);
            FillCard(card);
            FlipIn(card, .2f, () => busy = false);
        });
    }

    void StartBattle() {
        if (busy || leaving) return;
        Sfx("ui-click");
        BattleSelection.MapId = StageProgress.Current.id;
        if (!Application.CanStreamedLevelBeLoaded("Battle")) { SetStatus("战斗场景还没放进构建列表。"); return; }
        leaving = true;
        SetStatus("前往第 " + (StageProgress.Index + 1) + " 关：" + StageProgress.Current.title);
        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.raycastTarget = true;
        UiFx.Tween(this, .35f, u => fadeOverlay.color = new Color(0, 0, 0, u), () => SceneManager.LoadScene("Battle"));
    }

    void ShowHelp() {
        Sfx("ui-click");
        SetStatus("关卡按进度推进，地图固定；刷新可更换遭遇与候选角色。");
    }

    void SelectTab(int index) {
        if (tabReady) Sfx("ui-click");
        tab = index;
        bool book = index == 0;
        bookRoot.gameObject.SetActive(book);
        notePanel.gameObject.SetActive(!book);
        if (!book) {
            noteTitle.text = TabNames[index];
            noteText.text = TabNotes[index];
            noteIcon.sprite = Art(TabArt[index]);
            notePanel.localScale = Vector3.one * .9f;
            UiFx.Tween(this, .3f, u => { if (notePanel != null) notePanel.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, UiFx.OutBack(u)); });
        }
        for (int i = 0; i < tabRects.Length; i++) {
            bool on = i == index;
            tabBases[i].sprite = Art(on ? "tab-base-on" : "tab-base");
            tabIcons[i].color = on ? Color.white : new Color(.86f, .84f, .8f);
            tabPress[i].restScale = on ? 1.08f : 1f;
            int slot = i;
            float from = tabRects[i].anchoredPosition.y, to = on ? 12f : 2f;
            UiFx.Tween(this, .22f, u => { if (tabRects[slot] != null) tabRects[slot].anchoredPosition = new Vector2(tabRects[slot].anchoredPosition.x, Mathf.Lerp(from, to, UiFx.OutCubic(u))); });
            if (on && tabReady) tabPress[i].Punch(4f);
        }
        SetStatus(book ? "魔法书已打开。" : TabNames[index]);
    }

    // =====================================================================
    // Gold, numbers, status
    // =====================================================================
    void SpendGold(int amount, Vector2 from, string floating, Vector2? to = null) {
        gold -= amount;
        if (goldRoll != null) StopCoroutine(goldRoll);
        int start = shownGold, end = gold;
        goldRoll = UiFx.Tween(this, .65f, u => {
            shownGold = Mathf.RoundToInt(Mathf.Lerp(start, end, UiFx.OutCubic(u)));
            if (goldText != null) goldText.text = shownGold.ToString();
        });
        Flash(goldText, Warn);
        Pop(goldRect, 1.25f);
        // coins leave the purse towards whatever is being paid for
        var target = to ?? from;
        var purse = UiFx.LocalCenter(fx, goldIcon.rectTransform);
        if (to.HasValue) {
            UiFx.FlyIcons(this, fx, purse, to.Value, Art("coin"), 6, 38f, .45f, () => {
                Sfx("dice-lock", .35f, UnityEngine.Random.Range(1.4f, 1.8f));
                UiFx.Burst(this, fx, to.Value, 2, UiFx.Sparkle(), new Color(1f, .95f, .55f, 1f), new Color(1f, .6f, .2f, 0f), 160f, .4f, 22f);
            });
        } else if (!string.IsNullOrEmpty(floating)) {
            UiFx.FloatText(this, fx, font, floating, purse + new Vector2(0, -30), Warn, 30, 50f, .9f);
            UiFx.FlyIcons(this, fx, purse, target, Art("coin"), 3, 30f, .4f, null);
        } else {
            UiFx.FlyIcons(this, fx, purse, UiFx.LocalCenter(fx, upgradeRect), Art("coin"), 5, 36f, .4f, () =>
                Sfx("dice-lock", .3f, UnityEngine.Random.Range(1.5f, 1.9f)));
        }
    }

    void Flash(Text text, Color color) {
        var rest = text == goldText ? Gold : (text == levelText ? Color.white : text.color);
        if (text == rerollCostText) rest = gold >= RerollCost ? Gold : Warn;
        if (text == bonusText) rest = Good;
        UiFx.Tween(this, .5f, u => { if (text != null) text.color = Color.Lerp(color, rest, UiFx.OutCubic(u)); });
    }

    void Pop(RectTransform rect, float from) {
        if (rect == null) return;
        UiFx.Tween(this, .55f, u => { if (rect != null) rect.localScale = Vector3.one * Mathf.Lerp(from, 1f, UiFx.OutElastic(u)); }, () => { if (rect != null) rect.localScale = Vector3.one; });
    }

    void SetStatus(string text) {
        statusText.text = text;
        if (statusRoutine != null) StopCoroutine(statusRoutine);
        statusRoutine = UiFx.Tween(this, .5f, u => {
            statusText.color = Color.Lerp(Color.white, Cream, u);
            statusText.rectTransform.localScale = Vector3.one * (1f + .07f * (1f - u));
        });
    }

    void Refresh(bool animate) {
        if (!animate) { shownGold = gold; goldText.text = gold.ToString(); }
        bonusText.text = "+" + bonus + "%";
        levelText.text = "Lv." + level;
        rankChip.text = "Lv." + level;
        upgradeCostText.text = UpgradeCost.ToString();
        teamText.text = "队伍 " + team + "/" + TeamMax;
        rerollCostText.color = gold >= RerollCost ? Gold : Warn;
        upgradeCostText.color = gold >= UpgradeCost ? Color.white : new Color(1f, .7f, .65f);
        badgeText.text = recruited.ToString();
        teamBadge.gameObject.SetActive(recruited > 0);
        int tier = TierOf(level);
        if (tier != shownTier) {
            rankImage.sprite = Art("rank-" + tier);
            shownTier = tier;
        }
    }

    // =====================================================================
    // Idle motion
    // =====================================================================
    void Update() {
        float t = Time.unscaledTime, dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        foreach (var card in cards)
            if (card.body != null) {
                float bob = Mathf.Sin(t * 1.3f + card.phase) * 3f;
                card.body.offsetMin = new Vector2(0, bob); card.body.offsetMax = new Vector2(0, bob);
            }
        if (upgradeGlow != null) {
            bool can = gold >= UpgradeCost && !leaving;
            upgradeGlow.color = new Color(1f, .62f, .15f, can ? .32f + .22f * Mathf.Sin(t * 3.2f) : 0f);
        }
        if (teamFill != null) {
            float target = (float)team / TeamMax;
            teamFillShown = Mathf.Lerp(teamFillShown, target, 1f - Mathf.Exp(-7f * dt));
            teamFill.anchorMax = new Vector2(Mathf.Max(.06f, teamFillShown), 1f);
        }
        if (medContent != null) medContent.localScale = Vector3.one * (1.05f + .035f * Mathf.Sin(t * .45f));
    }

    // =====================================================================
    // Small helpers
    // =====================================================================
    void Sfx(string name, float volume = 1f, float pitch = 1f) {
        AudioClip clip;
        if (!clips.TryGetValue(name, out clip)) clips[name] = clip = Resources.Load<AudioClip>("Battle/Audio/" + name);
        if (clip == null || audioSource == null) return;
        audioSource.pitch = pitch * UnityEngine.Random.Range(.97f, 1.03f);
        audioSource.PlayOneShot(clip, volume);
    }

    Sprite Art(string name) { return Resources.Load<Sprite>("Spellbook/" + name); }

    // Ring with a thin inner hole, used as a radial-filled progress arc.
    static Sprite ArcRing() {
        if (arcSprite != null) return arcSprite;
        const int n = 256;
        const float inner = .909f;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[n * n];
        float c = (n - 1) * .5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++) {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (n * .5f);
                float a = Mathf.Clamp01((1f - d) * n * .5f) * Mathf.Clamp01((d - inner) * n * .5f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        arcSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100f);
        return arcSprite;
    }

    // Fixed-size rect with its own anchor point (anchor is normalised in the parent, pos in pixels).
    RectTransform Pin(string title, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size) {
        var obj = new GameObject(title, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        return rect;
    }

    RectTransform Area(string title, Transform parent, Vector2 min, Vector2 max) {
        var obj = new GameObject(title, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    RectTransform Region(Transform parent, float x0, float y0, float x1, float y1) {
        return Area("Region", parent, new Vector2(x0, y0), new Vector2(x1, y1));
    }

    Image Picture(string title, Transform parent, Sprite sprite, Vector2 min, Vector2 max) {
        var image = Area(title, parent, min, max).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = sprite == null ? new Color(.72f, .55f, .32f) : Color.white;
        image.raycastTarget = false;
        return image;
    }

    // Nine-slice with borders shrunk so they look right at the intended on-screen height.
    void SliceByHeight(Image image, float displayHeight) {
        if (image.sprite == null || displayHeight <= 0f) return;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = Mathf.Max(1f, image.sprite.rect.height / displayHeight);
    }

    Button ButtonOf(string title, Transform parent, Sprite sprite, string caption, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action, float displayHeight) {
        var image = Picture(title, parent, sprite, min, max);
        if (sprite != null && sprite.border != Vector4.zero) SliceByHeight(image, displayHeight);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, .97f, .88f);
        colors.pressedColor = new Color(.82f, .82f, .82f);
        colors.disabledColor = new Color(.7f, .7f, .7f, 1f);
        button.colors = colors;
        image.gameObject.AddComponent<PressFeedback>();
        if (!string.IsNullOrEmpty(caption)) Label(image.transform, caption, .08f, .08f, .92f, .92f, 24, Color.white, TextAnchor.MiddleCenter);
        return button;
    }

    // Text that always fits its box (best-fit), with a dark outline only when the colour is light.
    Text Label(Transform parent, string content, float x0, float y0, float x1, float y1, int size, Color color, TextAnchor anchor) {
        var text = Area(string.IsNullOrEmpty(content) ? "Text" : content, parent, new Vector2(x0, y0), new Vector2(x1, y1)).gameObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = size;
        text.resizeTextMinSize = Mathf.Max(9, Mathf.RoundToInt(size * .55f));
        if (color.grayscale > .55f) {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.1f, .04f, .01f, .8f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }
        return text;
    }
}
