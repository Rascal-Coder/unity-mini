using UnityEngine;

// Campaign order. Stage N always plays the same map, the player cannot pick one.
// Edit `Maps` to change which battle map each stage uses (ids come from BattleMapCatalog).
public static class StageProgress {
    public static readonly string[] Maps = { "Forest", "Jungle", "Desert", "Snow", "Castle", "Demon" };
    const string Key = "Spellbook.Stage";
    const string ShownKey = "Spellbook.StageShown";

    public static int Count { get { return Maps.Length; } }

    public static int Index {
        get { return Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Maps.Length - 1); }
        set { PlayerPrefs.SetInt(Key, Mathf.Clamp(value, 0, Maps.Length - 1)); PlayerPrefs.Save(); }
    }

    // The stage the Spellbook last displayed; lets the book play an "unlocked" animation after a victory.
    public static int ShownIndex {
        get { return PlayerPrefs.GetInt(ShownKey, -1); }
        set { PlayerPrefs.SetInt(ShownKey, value); PlayerPrefs.Save(); }
    }

    public static BattleMap MapAt(int index) { return BattleMapCatalog.Get(Maps[Mathf.Clamp(index, 0, Maps.Length - 1)]); }
    public static BattleMap Current { get { return MapAt(Index); } }

    // Call this when the player clears the current stage. Returns false when already on the last stage.
    public static bool Advance() {
        if (Index >= Maps.Length - 1) return false;
        Index = Index + 1;
        return true;
    }

    public static void Reset() { Index = 0; ShownIndex = -1; }
}
