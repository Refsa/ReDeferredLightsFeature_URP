using UnityEngine;
using UnityEditor;
#if UNITY_EDITOR
[CustomEditor(typeof(SpawnLightsTest))]
public class SpawnLightsTestEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var targetAs = target as SpawnLightsTest;
        
        if (GUILayout.Button("Spawn Lights"))
        {
            targetAs.SpawnLights();
        }
        if (GUILayout.Button("Clear Lights"))
        {
            targetAs.DeleteLights();
        }
    }
}
#endif