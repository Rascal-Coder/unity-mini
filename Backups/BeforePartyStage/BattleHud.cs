using UnityEngine;
using UnityEngine.UI;

public sealed class BattleDiceFace : MaskableGraphic {
    public int Value;
    public void Show(int value) { Value=value; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh) {
        vh.Clear(); if(Value<1||Value>6)return;
        if(Value%2==1) Dot(vh,.5f,.5f);
        if(Value>=2){Dot(vh,.25f,.75f);Dot(vh,.75f,.25f);}
        if(Value>=4){Dot(vh,.75f,.75f);Dot(vh,.25f,.25f);}
        if(Value==6){Dot(vh,.25f,.5f);Dot(vh,.75f,.5f);}
    }
    void Dot(VertexHelper vh,float x,float y) {
        Rect r=rectTransform.rect; Vector2 center=new Vector2(r.x+x*r.width,r.y+y*r.height);
        float radius=Mathf.Min(r.width,r.height)*.065f; int start=vh.currentVertCount;
        vh.AddVert(center,color,Vector2.zero);
        for(int i=0;i<=14;i++){float a=i*Mathf.PI*2/14;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
        for(int i=0;i<14;i++)vh.AddTriangle(start,start+i+1,start+i+2);
    }
}

public sealed class BattleHud : MonoBehaviour {
    public RectTransform Root;
    public Text Round, Hint, Combo, Damage, Target, Career, PauseLabel, AudioLabel;
    public Button Roll, Attack;
    public readonly Button[] Dice=new Button[5];
    public readonly BattleDiceFace[] Faces=new BattleDiceFace[5];
    public readonly Text[] Holds=new Text[5];
    public readonly Text[] Unknown=new Text[5];
    public readonly Button[] Roles=new Button[3];
    public readonly Button[] RewardChoices=new Button[3];
    public Image Health;
    public GameObject Rewards, PauseOverlay;
    public Text RewardTitle;
    Font font;
    Rect lastSafe;
    int lastW,lastH;
    static readonly Color Ink=new Color(.065f,.085f,.10f,.98f);
    static readonly Color Gold=new Color(.94f,.70f,.30f);
    public void Build(DiceBattle game) {
        font=Resources.Load<Font>("Battle/Fonts/NotoSansCJKsc-Regular");
        if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},24);
        var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        Root=Rect("Safe Area",transform,Vector2.zero,Vector2.one); UpdateSafeArea();
        Panel(Root,new Vector2(0,.865f),Vector2.one,Ink);
        TextAt(Root,"骰境 · 训练场",new Vector2(.02f,.875f),new Vector2(.2f,.995f),28,Gold);
        Round=TextAt(Root,"第 1 回合",new Vector2(.02f,.80f),new Vector2(.22f,.86f),23,Color.white);
        string[] roles={"战士","游侠","法师"};
        for(int i=0;i<3;i++){int index=i;Roles[i]=ButtonAt(Root,roles[i],new Vector2(.24f+i*.102f,.875f),new Vector2(.333f+i*.102f,.995f),()=>game.SelectRole(index));}
        Button audio=ButtonAt(Root,"声音：开",new Vector2(.77f,.875f),new Vector2(.875f,.995f),game.ToggleAudio);AudioLabel=audio.GetComponentInChildren<Text>();
        Button pause=ButtonAt(Root,"暂停",new Vector2(.89f,.875f),new Vector2(.98f,.995f),game.TogglePause);PauseLabel=pause.GetComponentInChildren<Text>();
        Target=TextAt(Root,"练习木桩",new Vector2(.65f,.805f),new Vector2(.94f,.865f),24,Color.white);
        var track=Panel(Root,new Vector2(.65f,.788f),new Vector2(.94f,.802f),new Color(.09f,.07f,.06f,.8f));
        Health=Panel(track.rectTransform,Vector2.zero,Vector2.one,new Color(.83f,.39f,.24f));
        Panel(Root,Vector2.zero,new Vector2(1,.335f),Ink);
        Panel(Root,new Vector2(0,.333f),new Vector2(1,.337f),Gold);
        Career=TextAt(Root,"战士",new Vector2(.02f,.23f),new Vector2(.19f,.30f),30,Gold);
        Damage=TextAt(Root,"准备掷骰",new Vector2(.02f,.12f),new Vector2(.20f,.23f),25,Color.white);
        Hint=TextAt(Root,"点击骰子保留",new Vector2(.02f,.025f),new Vector2(.205f,.12f),19,new Color(.64f,.71f,.70f));
        Combo=TextAt(Root,"掷出你的第一组骰子",new Vector2(.235f,.275f),new Vector2(.755f,.328f),25,Gold);Combo.alignment=TextAnchor.MiddleCenter;
        for(int i=0;i<5;i++) {
            int index=i;float x=.24f+i*.103f;
            Dice[i]=ButtonAt(Root,"",new Vector2(x,.09f),new Vector2(x+.09f,.258f),()=>game.ToggleHold(index));
            Dice[i].image.color=new Color(.92f,.89f,.78f);
            var faceRect=Rect("Pips",Dice[i].transform,new Vector2(.1f,.06f),new Vector2(.9f,.94f));
            Faces[i]=faceRect.gameObject.AddComponent<BattleDiceFace>();Faces[i].color=new Color(.13f,.18f,.19f);Faces[i].raycastTarget=false;
            Unknown[i]=TextAt(Dice[i].transform,"?",Vector2.zero,Vector2.one,42,new Color(.25f,.29f,.28f));Unknown[i].alignment=TextAnchor.MiddleCenter;
            Holds[i]=TextAt(Root,"点击保留",new Vector2(x,.031f),new Vector2(x+.09f,.081f),18,new Color(.55f,.63f,.62f));Holds[i].alignment=TextAnchor.MiddleCenter;
        }
        Roll=ButtonAt(Root,"掷骰",new Vector2(.785f,.18f),new Vector2(.98f,.303f),game.RollDice);
        Attack=ButtonAt(Root,"攻击",new Vector2(.785f,.025f),new Vector2(.98f,.163f),game.Attack);Attack.image.color=Gold;Attack.GetComponentInChildren<Text>().color=Ink;
        Rewards=Panel(Root,Vector2.zero,Vector2.one,new Color(0,0,0,.78f)).gameObject;
        RewardTitle=TextAt(Rewards.transform,"训练完成 · 选择契约",new Vector2(.15f,.65f),new Vector2(.85f,.79f),36,Gold);RewardTitle.alignment=TextAnchor.MiddleCenter;
        string[] titles={"锐刃契约\n基础伤害 +8","命运契约\n每轮掷骰次数 +1\n（最多 5 次）","共鸣契约\n组合倍率 +0.2"};
        for(int i=0;i<3;i++){int choice=i;RewardChoices[i]=ButtonAt(Rewards.transform,titles[i],new Vector2(.13f+i*.255f,.30f),new Vector2(.36f+i*.255f,.60f),()=>game.ChooseReward(choice));}
        TextAt(Rewards.transform,"选取后迎战下一只木桩",new Vector2(.33f,.18f),new Vector2(.75f,.26f),22,Color.white);
        Rewards.SetActive(false);
        PauseOverlay=Panel(Root,Vector2.zero,Vector2.one,new Color(0,0,0,.82f)).gameObject;
        var title=TextAt(PauseOverlay.transform,"训练已暂停",new Vector2(.25f,.57f),new Vector2(.75f,.72f),38,Gold);title.alignment=TextAnchor.MiddleCenter;
        ButtonAt(PauseOverlay.transform,"继续训练",new Vector2(.36f,.32f),new Vector2(.64f,.49f),game.TogglePause);PauseOverlay.SetActive(false);
    }
    public Text FloatingText(string text,Vector3 world,Camera camera,Color color) {
        var screen=camera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Root,screen,null,out var local);
        var label=TextAt(Root,text,new Vector2(.5f,.5f),new Vector2(.5f,.5f),48,color);
        label.alignment=TextAnchor.MiddleCenter;label.rectTransform.sizeDelta=new Vector2(280,90);label.rectTransform.anchoredPosition=local;return label;
    }
    public static void SetButton(Button b,string text){b.GetComponentInChildren<Text>().text=text;}
    void Update(){UpdateSafeArea();}
    void UpdateSafeArea(){if(Root==null)return;Rect safe=Screen.safeArea;if(safe==lastSafe&&lastW==Screen.width&&lastH==Screen.height)return;lastSafe=safe;lastW=Screen.width;lastH=Screen.height;Root.anchorMin=new Vector2(safe.x/Screen.width,safe.y/Screen.height);Root.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);}
    RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max){var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var r=obj.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;return r;}
    Image Panel(Transform parent,Vector2 min,Vector2 max,Color color){var rect=Rect("Panel",parent,min,max);var im=rect.gameObject.AddComponent<Image>();im.color=color;return im;}
    Text TextAt(Transform parent,string value,Vector2 min,Vector2 max,int size,Color color){var rect=Rect(value,parent,min,max);var text=rect.gameObject.AddComponent<Text>();text.text=value;text.font=font;text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
    Button ButtonAt(Transform parent,string text,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action){var im=Panel(parent,min,max,new Color(.18f,.24f,.25f));im.gameObject.name=text;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;b.onClick.AddListener(action);var colors=b.colors;colors.highlightedColor=new Color(1.1f,1.1f,1.1f);colors.pressedColor=new Color(.75f,.8f,.8f);colors.disabledColor=new Color(.45f,.45f,.45f,.7f);b.colors=colors;var label=TextAt(im.transform,text,new Vector2(.04f,.02f),new Vector2(.96f,.98f),25,Color.white);label.alignment=TextAnchor.MiddleCenter;return b;}
}

