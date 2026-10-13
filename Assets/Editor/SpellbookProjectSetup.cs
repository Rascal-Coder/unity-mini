using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SpellbookProjectSetup {
    const string ScenePath = "Assets/Scenes/Spellbook.unity";

    [MenuItem("Expedition/Open Spellbook")]
    public static void Open() {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        if (!scenes.Any(s => s.path == "Assets/Scenes/Battle.unity")) scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/Battle.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
    }
}
