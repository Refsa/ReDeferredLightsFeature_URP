using UnityEngine;
using UnityEditor;
#if UNITY_EDITOR
[CustomEditor(typeof(SpawnLightsBox))]
public class SpawnLightsBoxEditor : Editor {
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();

        var targetAs = target as SpawnLightsBox;
        
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