using System.Collections.Generic;
using UnityEngine;

// Shallow 3/4 ground plane. Actors and props sort by their foot Y.
public sealed class BattleStage : MonoBehaviour {
    public const int CurrentLayoutVersion=4;
    [SerializeField] int layoutVersion;
    public bool NeedsRebuild { get { return layoutVersion!=CurrentLayoutVersion||heroPoint==null; } }
    public Transform heroPoint,dummyPoint;
    public Transform[] allySlots,enemySlots;
    public SpriteRenderer dummy;
    readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
    static readonly string[] GeneratedGroups={"01 Far landscape","02 Walkable ground","03 Rear props","04 Actors","05 Foreground props","06 Formation slots"};
    public void Build() {
        if(!NeedsRebuild)return;
        LoadSprites();
        if(!sprites.ContainsKey("Runestone")||!sprites.ContainsKey("Ground_0_0")) {Debug.LogError("Enchanted sprite sheets are not imported yet.",this);return;}
        for(int i=transform.childCount-1;i>=0;i--) {
            var child=transform.GetChild(i);
            if(System.Array.IndexOf(GeneratedGroups,child.name)<0)continue;
            child.gameObject.SetActive(false);
            if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
        }
        var far=Group(GeneratedGroups[0]);var ground=Group(GeneratedGroups[1]);
        var rear=Group(GeneratedGroups[2]);var actors=Group(GeneratedGroups[3]);
        var front=Group(GeneratedGroups[4]);var formation=Group(GeneratedGroups[5]);
        Place(far,"Distant arcane gateway",Resources.Load<Sprite>("Battle/Art/Enchanted/Background"),new Vector2(0,-2.8f),23.4f,-1000,false);
        // Six adjacent panels with extruded gutters; floor continues below the viewport.
        for(int row=0;row<2;row++)for(int col=0;col<3;col++)
            Place(ground,"Ground panel "+row+" "+col,sprites["Ground_"+row+"_"+col],new Vector2(-12+col*8,1.05f-row*3),8,-800,false);
        Place(rear,"Left rune guardian",sprites["Runestone"],new Vector2(-8.8f,.45f),2.8f,0,true);
        Place(rear,"Right crystal cluster",sprites["Crystal"],new Vector2(8.8f,.42f),2.6f,0,true);
        Place(rear,"Rear moon flowers left",sprites["MoonBush"],new Vector2(-6.9f,.6f),1.9f,0,true);
        Place(rear,"Rear moon flowers right",sprites["MoonBush"],new Vector2(6.9f,.6f),1.7f,0,true).flipX=true;
        var left=Place(front,"Foreground left roots",sprites["RootLeft"],new Vector2(-9,-3.85f),5.5f,0,true);
        var right=Place(front,"Foreground right roots",sprites["RootRight"],new Vector2(9,-3.85f),5.5f,0,true);
        left.color=right.color=new Color(.57f,.65f,.75f,1);
        Place(front,"Near magic mushrooms",sprites["Mushrooms"],new Vector2(7.1f,-3.35f),1.9f,0,true).color=new Color(.72f,.8f,.88f,1);
        heroPoint=Anchor(actors,"Hero foot anchor",new Vector2(-3.5f,-1.1f));
        dummyPoint=Anchor(actors,"Dummy foot anchor",new Vector2(3.5f,-1.1f));dummyPoint.gameObject.AddComponent<BattleDepth>();
        dummy=Place(dummyPoint,"Wooden training dummy",Resources.Load<Sprite>("Battle/Art/training-dummy"),Vector2.zero,1.68f,0,false);
        AddContactShadow(heroPoint,1.25f);AddContactShadow(dummyPoint,1.2f);
        allySlots=new Transform[8];enemySlots=new Transform[8];
        // Three staggered depth rows. Markers never render in game.
        Vector2[] positions={new Vector2(-6.7f,-.25f),new Vector2(-4.6f,-.25f),new Vector2(-2.5f,-.25f),new Vector2(-5.7f,-1.15f),new Vector2(-3.5f,-1.15f),new Vector2(-6.7f,-2.05f),new Vector2(-4.6f,-2.05f),new Vector2(-2.5f,-2.05f)};
        for(int i=0;i<8;i++) {
            allySlots[i]=Anchor(formation,"Ally slot "+(i+1),positions[i]);
            enemySlots[i]=Anchor(formation,"Enemy slot "+(i+1),new Vector2(-positions[i].x,positions[i].y));
        }
        layoutVersion=CurrentLayoutVersion;
    }
    void LoadSprites() {
        sprites.Clear();foreach(string sheet in new[]{"Vegetation","Tileset"})
            foreach(var sprite in Resources.LoadAll<Sprite>("Battle/Art/Enchanted/"+sheet))sprites[sprite.name]=sprite;
    }
    public void AddContactShadow(Transform foot,float width){Place(foot,"Contact shadow",Resources.Load<Sprite>("Battle/Art/Enchanted/ContactShadow"),Vector2.zero,width,-20,false);}
    Transform Group(string title){var obj=new GameObject(title);obj.transform.SetParent(transform,false);return obj.transform;}
    static Transform Anchor(Transform parent,string name,Vector2 position){var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;return obj.transform;}
    SpriteRenderer Place(Transform parent,string title,Sprite sprite,Vector2 position,float width,int order,bool depth) {
        var obj=Anchor(parent,title,position).gameObject;var sr=obj.AddComponent<SpriteRenderer>();sr.sprite=sprite;
        if(sprite==null){Debug.LogError("Missing forest sprite for "+title,this);return sr;}
        obj.transform.localScale=Vector3.one*(width/sprite.bounds.size.x);sr.sortingOrder=order;
        var material=Resources.Load<Material>("Battle/SpriteMaterial");if(material!=null)sr.sharedMaterial=material;
        if(depth)obj.AddComponent<BattleDepth>();return sr;
    }
    void OnDrawGizmosSelected(){DrawSlots(allySlots,new Color(.3f,.8f,1));DrawSlots(enemySlots,new Color(1,.5f,.3f));}
    static void DrawSlots(Transform[] slots,Color color){if(slots==null)return;Gizmos.color=color;foreach(var slot in slots)if(slot!=null)Gizmos.DrawWireCube(slot.position,new Vector3(1.2f,.45f,.01f));}
}
