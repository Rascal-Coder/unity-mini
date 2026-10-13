using UnityEngine;

public sealed class BattleMap {
    public readonly string id, title;
    public readonly float groundLine;
    public readonly Color sky, shadow;
    public readonly string[] middle;
    public readonly float[] middleBottom;
    public readonly bool[] pinToGround;
    public BattleMap(string id, string title, float groundLine, Color sky, Color shadow, string[] middle, float[] middleBottom, bool[] pinToGround) {
        this.id = id; this.title = title; this.groundLine = groundLine; this.sky = sky; this.shadow = shadow;
        this.middle = middle; this.middleBottom = middleBottom; this.pinToGround = pinToGround;
    }
    public string Art(string part) { return "Battle/Art/Maps/" + id + "/" + part; }
}

public static class BattleMapCatalog {
    public const string DefaultId = "Forest";
    public static readonly BattleMap[] All = {
        new BattleMap("Forest","魔法森林",0.2954f,new Color(0.409f,0.379f,0.218f),new Color(0.675f,0.760f,0.695f),new[]{"Middle"},new[]{0.9984f},new[]{true}),
        new BattleMap("Aisle","石廊",0.5744f,new Color(0.490f,0.677f,0.978f),new Color(0.749f,0.684f,0.760f),new[]{"Middle"},new[]{0.9507f},new[]{false}),
        new BattleMap("Axe","战斧营地",0.2804f,new Color(0.108f,0.152f,0.235f),new Color(0.570f,0.651f,0.760f),new[]{"Middle"},new[]{0.9484f},new[]{false}),
        new BattleMap("Berg","冰山",0.4667f,new Color(0.342f,0.743f,0.896f),new Color(0.477f,0.662f,0.760f),new[]{"Middle"},new[]{0.9593f},new[]{false}),
        new BattleMap("Castle","城堡",0.3335f,new Color(0.232f,0.438f,0.442f),new Color(0.471f,0.732f,0.760f),new[]{"Middle"},new[]{0.9701f},new[]{false}),
        new BattleMap("Cattle","牧场",0.2126f,new Color(0.356f,0.327f,0.370f),new Color(0.760f,0.604f,0.521f),new[]{"Middle"},new[]{0.9347f},new[]{false}),
        new BattleMap("Cow","牛栏",0.4061f,new Color(0.457f,0.327f,0.283f),new Color(0.714f,0.707f,0.760f),new[]{"Middle"},new[]{0.9883f},new[]{false}),
        new BattleMap("Demon","熔岩魔域",0.3591f,new Color(0.277f,0.260f,0.312f),new Color(0.760f,0.562f,0.654f),new[]{"Middle"},new[]{0.9983f},new[]{true}),
        new BattleMap("Desert","沙漠",0.3125f,new Color(0.310f,0.541f,0.737f),new Color(0.760f,0.636f,0.497f),new[]{"Middle"},new[]{0.9664f},new[]{false}),
        new BattleMap("Egypt","埃及神殿",0.6085f,new Color(0.958f,0.517f,0.552f),new Color(0.760f,0.637f,0.618f),new[]{"Middle"},new[]{0.9231f},new[]{false}),
        new BattleMap("Gobi","戈壁",0.2438f,new Color(0.945f,0.833f,0.667f),new Color(0.760f,0.652f,0.529f),new[]{"Middle"},new[]{0.6388f},new[]{false}),
        new BattleMap("Halloween","万圣夜",0.2562f,new Color(0.245f,0.141f,0.305f),new Color(0.644f,0.583f,0.760f),new[]{"Middle"},new[]{0.9967f},new[]{false}),
        new BattleMap("Ice","冰窟",0.3763f,new Color(0.145f,0.241f,0.412f),new Color(0.491f,0.601f,0.760f),new[]{"Middle"},new[]{0.9153f},new[]{false}),
        new BattleMap("Island","海岛",0.2882f,new Color(0.765f,0.539f,0.342f),new Color(0.760f,0.612f,0.616f),new[]{"Middle"},new[]{0.9710f},new[]{false}),
        new BattleMap("Jungle","丛林",0.4807f,new Color(0.248f,0.393f,0.258f),new Color(0.637f,0.760f,0.471f),new[]{"Middle"},new[]{0.9495f},new[]{false}),
        new BattleMap("Maya","玛雅",0.2852f,new Color(0.684f,0.634f,0.332f),new Color(0.609f,0.760f,0.471f),new[]{"Middle"},new[]{0.9631f},new[]{false}),
        new BattleMap("Mountain","山脉",0.3446f,new Color(0.305f,0.733f,0.886f),new Color(0.572f,0.760f,0.471f),new[]{"Middle"},new[]{0.9566f},new[]{false}),
        new BattleMap("River","地下河",0.6270f,new Color(0.213f,0.543f,0.692f),new Color(0.471f,0.632f,0.760f),new[]{"Middle"},new[]{0.9730f},new[]{false}),
        new BattleMap("Ship","舰船",0.3916f,new Color(0.115f,0.681f,0.669f),new Color(0.471f,0.756f,0.760f),new[]{"Middle"},new[]{0.9576f},new[]{false}),
        new BattleMap("Snow","雪原",0.2140f,new Color(0.783f,0.673f,0.437f),new Color(0.497f,0.595f,0.760f),new[]{"Middle"},new[]{0.9747f},new[]{false}),
        new BattleMap("Street","夜城",0.2128f,new Color(0.348f,0.244f,0.382f),new Color(0.543f,0.576f,0.760f),new[]{"Middle"},new[]{0.9968f},new[]{true}),
        new BattleMap("Cave","洞穴",0.3605f,new Color(0.145f,0.466f,0.619f),new Color(0.471f,0.631f,0.760f),new[]{"Middle"},new[]{0.9809f},new[]{false}),
        new BattleMap("Arena","竞技场",0.2426f,new Color(0.637f,0.504f,0.627f),new Color(0.760f,0.597f,0.543f),new[]{"Middle"},new[]{0.8761f},new[]{false}),
        new BattleMap("Doomsday","末日",0.3591f,new Color(0.170f,0.124f,0.182f),new Color(0.760f,0.523f,0.629f),new[]{"Middle"},new[]{0.9623f},new[]{false}),
        new BattleMap("Treasure","宝库",0.2865f,new Color(0.099f,0.336f,0.459f),new Color(0.730f,0.760f,0.630f),new[]{"Middle"},new[]{0.9936f},new[]{false}),
    };
    public static BattleMap Get(string id) {
        foreach (var map in All) if (map.id == id) return map;
        return All[0];
    }
    public static int IndexOf(string id) {
        for (int i = 0; i < All.Length; i++) if (All[i].id == id) return i;
        return 0;
    }
}
