using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class BattleHud : MonoBehaviour {
    public RectTransform Root;
    public Text Target, Round, Damage, BattleState;
    public Button PauseButton, SpeedButton, AudioButton, RestartButton;
    public GameObject PauseOverlay;
    public Image Health;
    public readonly Image[] EnergyBars = new Image[3];
    public readonly Text[] MemberState = new Text[3];
    readonly Text[] energyText = new Text[3];
    Font font;
    Button resume;
    Rect lastSafe;
    int lastWidth, lastHeight;
    static readonly Color Ink = new Color(.025f, .07f, .095f, .92f);
    static readonly Color Gold = new Color(.88f, .76f, .45f);
    public void Build(DiceBattle game) {
        font = Resources.Load<Font>("Battle/Fonts/NotoSansCJKsc-Regular");
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>(); Root = Rect("Safe Area", transform, Vector2.zero, Vector2.one); UpdateSafeArea();
        var title = Card(Root, "Training title", new Vector2(.02f, .89f), new Vector2(.305f, .98f));
        Label(title, "三人小队 · 魔法森林", new Vector2(.045f, .38f), new Vector2(.98f, .95f), 27, Gold);
        Round = Label(title, "训练场  /  第 1 轮", new Vector2(.045f, .06f), new Vector2(.98f, .4f), 18, new Color(.73f, .8f, .83f));
        var controls = Card(Root, "Battle controls", new Vector2(.355f, .908f), new Vector2(.68f, .976f));
        BattleState = Label(controls, "自动战斗", new Vector2(.03f, 0), new Vector2(.39f, 1), 25, Color.white);
        SpeedButton = ButtonAt(controls, "×1", new Vector2(.4f, .06f), new Vector2(.58f, .94f), game.ToggleSpeed);
        PauseButton = ButtonAt(controls, "暂停", new Vector2(.61f, .06f), new Vector2(.97f, .94f), game.TogglePause);
        var target = Card(Root, "Target status", new Vector2(.735f, .897f), new Vector2(.98f, .977f));
        Target = Label(target, "", new Vector2(.04f, .39f), new Vector2(.96f, .98f), 22, Color.white);
        var healthTrack = Panel(target, "Target health track", new Vector2(.04f, .14f), new Vector2(.96f, .33f), new Color(.14f, .08f, .1f));
        Health = Panel(healthTrack.rectTransform, "Target health", Vector2.zero, Vector2.one, new Color(.9f, .27f, .28f));
        var row = Rect("Three party cards", Root, new Vector2(.22f, .025f), new Vector2(.78f, .15f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
        string[] skills = { "裂空斩", "三连箭", "奥术爆发" };
        Color[] accents = { Gold, new Color(.48f, .81f, .47f), new Color(.73f, .57f, 1) };
        for (int i = 0; i < 3; i++) {
            var card = Card(row, BattleHeroVisual.Titles[i] + " status", Vector2.zero, Vector2.one);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var portrait = Panel(card, "Portrait", new Vector2(.025f, .13f), new Vector2(.29f, .88f), Color.white);
            portrait.sprite = Resources.Load<Sprite>("Battle/Art/Portraits/" + BattleHeroVisual.PortraitNames[i]); portrait.preserveAspect = true;
            if (portrait.sprite == null) { portrait.color = accents[i]; Label(portrait.transform, BattleHeroVisual.Titles[i].Substring(0, 1), Vector2.zero, Vector2.one, 24, Ink).alignment = TextAnchor.MiddleCenter; }
            Label(card, BattleHeroVisual.Titles[i], new Vector2(.32f, .67f), new Vector2(.57f, .97f), 24, Color.white);
            Label(card, skills[i], new Vector2(.58f, .68f), new Vector2(.97f, .97f), 16, accents[i]).alignment = TextAnchor.MiddleRight;
            var hp = Panel(card, "Member health", new Vector2(.33f, .49f), new Vector2(.96f, .64f), new Color(.8f, .24f, .26f));
            var hpText = Label(hp.transform, game.MaxHealth[i] + " / " + game.MaxHealth[i], Vector2.zero, Vector2.one, 13, Color.white); hpText.alignment = TextAnchor.MiddleCenter;
            var energy = Panel(card, "Skill charge track", new Vector2(.33f, .3f), new Vector2(.96f, .43f), new Color(.05f, .14f, .19f));
            EnergyBars[i] = Panel(energy.transform, "Skill charge", Vector2.zero, Vector2.one, new Color(.14f, .69f, .85f));
            energyText[i] = Label(energy.transform, "0 / 100", Vector2.zero, Vector2.one, 12, Color.white); energyText[i].alignment = TextAnchor.MiddleCenter;
            MemberState[i] = Label(card, "准备就绪", new Vector2(.33f, .03f), new Vector2(.97f, .26f), 15, new Color(.7f, .79f, .82f));
        }
        Damage = Label(Root, "训练伤害  0", new Vector2(.022f, .03f), new Vector2(.2f, .084f), 20, Color.white);
        AudioButton = ButtonAt(Root, "声音：开", new Vector2(.025f, .1f), new Vector2(.14f, .153f), game.ToggleAudio);
        RestartButton = ButtonAt(Root, "重新训练", new Vector2(.84f, .045f), new Vector2(.975f, .12f), game.RestartTraining);
        PauseOverlay = Panel(Root, "Pause overlay", Vector2.zero, Vector2.one, new Color(.01f, .04f, .07f, .82f)).gameObject;
        Label(PauseOverlay.transform, "训练已暂停", new Vector2(.25f, .56f), new Vector2(.75f, .7f), 40, Gold).alignment = TextAnchor.MiddleCenter;
        resume = ButtonAt(PauseOverlay.transform, "继续训练", new Vector2(.39f, .36f), new Vector2(.61f, .47f), game.TogglePause);
        PauseOverlay.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(PauseButton.gameObject);
    }
    public void SetTarget(int wave, int health, int maxHealth, int total) {
        Round.text = "训练场  /  第 " + wave + " 轮"; Target.text = "训练木桩  " + health + " / " + maxHealth;
        Health.rectTransform.anchorMax = new Vector2(health / (float)maxHealth, 1); Damage.text = "训练伤害  " + total;
    }
    public void SetMember(int role, int health, int energy, string state) {
        EnergyBars[role].rectTransform.anchorMax = new Vector2(energy / 100f, 1); energyText[role].text = energy + " / 100"; MemberState[role].text = state;
    }
    public void SetBattleState(string text) { BattleState.text = text; }
    public void SetControls(bool paused, float speed, bool muted) {
        SetButton(SpeedButton, "×" + speed.ToString("0")); SetButton(PauseButton, paused ? "继续" : "暂停"); SetButton(AudioButton, muted ? "声音：关" : "声音：开");
        bool changed = PauseOverlay.activeSelf != paused; PauseOverlay.SetActive(paused);
        if (changed && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(paused ? resume.gameObject : PauseButton.gameObject);
    }
    public void ShowDamage(int amount, Vector3 point, Camera camera) {
        var text = Label(Root, amount.ToString(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), 31, new Color(1, .85f, .43f));
        text.alignment = TextAnchor.MiddleCenter; text.rectTransform.sizeDelta = new Vector2(150, 65);
        Vector2 local; RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, camera.WorldToScreenPoint(point), null, out local);
        text.rectTransform.anchoredPosition = local; StartCoroutine(FloatDamage(text));
    }
    IEnumerator FloatDamage(Text text) {
        for (float t = 0; t < .75f; t += Time.deltaTime) {
            text.rectTransform.anchoredPosition += Vector2.up * Time.deltaTime * 55;
            var color = text.color; color.a = 1 - t / .75f; text.color = color; yield return null;
        }
        Destroy(text.gameObject);
    }
    public static void SetButton(Button button, string text) { button.GetComponentInChildren<Text>().text = text; }
    void Update() { UpdateSafeArea(); }
    void UpdateSafeArea() {
        if (Root == null || Screen.width == 0 || Screen.height == 0) return;
        Rect safe = Screen.safeArea; if (safe.width <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        if (safe == lastSafe && lastWidth == Screen.width && lastHeight == Screen.height) return;
        lastSafe = safe; lastWidth = Screen.width; lastHeight = Screen.height;
        Root.anchorMin = new Vector2(safe.x / Screen.width, safe.y / Screen.height); Root.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
    }
    RectTransform Rect(string title, Transform parent, Vector2 min, Vector2 max) {
        var obj = new GameObject(title, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
    Image Panel(Transform parent, string title, Vector2 min, Vector2 max, Color color) {
        var image = Rect(title, parent, min, max).gameObject.AddComponent<Image>(); image.color = color; return image;
    }
    RectTransform Card(Transform parent, string title, Vector2 min, Vector2 max) {
        var image = Panel(parent, title, min, max, Ink);
        var border = image.gameObject.AddComponent<Outline>(); border.effectColor = new Color(.5f, .59f, .59f, .8f); border.effectDistance = new Vector2(1, -1); return image.rectTransform;
    }
    Text Label(Transform parent, string content, Vector2 min, Vector2 max, int size, Color color) {
        var text = Rect(content, parent, min, max).gameObject.AddComponent<Text>(); text.text = content; text.font = font; text.fontSize = size;
        text.color = color; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = text.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .85f); shadow.effectDistance = new Vector2(1, -1); return text;
    }
    Button ButtonAt(Transform parent, string content, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action) {
        var rect = Card(parent, content, min, max); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
        var colors = button.colors; colors.highlightedColor = new Color(.8f, .91f, 1); colors.pressedColor = new Color(.5f, .7f, .8f); button.colors = colors;
        Label(rect, content, new Vector2(.03f, .04f), new Vector2(.97f, .96f), 22, Gold).alignment = TextAnchor.MiddleCenter; return button;
    }
}
