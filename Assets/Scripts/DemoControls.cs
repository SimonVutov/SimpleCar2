using UnityEngine;
using UnityEngine.SceneManagement;

public class DemoControls : MonoBehaviour
{
    bool showHelp;
    bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var owner = new GameObject("Demo controls");
        owner.AddComponent<DemoControls>();
        DontDestroyOnLoad(owner);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 270, 12, 258, showHelp ? 260 : 95), GUI.skin.box);
        GUILayout.Label("SimpleCar2  |  " + SceneManager.GetActiveScene().name);
        GUI.enabled = !loading;
        GUILayout.BeginHorizontal();
        foreach (string scene in new[] { "Road", "Offroad", "RoadF1" })
            if (GUILayout.Button(scene))
            {
                loading = true;
                SceneManager.LoadSceneAsync(scene).completed += _ => loading = false;
            }
        GUILayout.EndHorizontal();
        GUI.enabled = true;
        if (GUILayout.Button(showHelp ? "Hide controls" : "Controls")) showHelp = !showHelp;
        if (showHelp)
            GUILayout.Label("W/S: accelerate, brake/reverse\nA/D: steer | Space: handbrake\nR: recover vehicle | Q/E: shift\nC: free/follow camera | Scroll: zoom\nFree camera: arrows + right mouse\nEsc: release cursor | Click: capture\nGamepad: left stick, triggers, A\nRight stick: chase-camera view");
        GUILayout.EndArea();
    }
}
