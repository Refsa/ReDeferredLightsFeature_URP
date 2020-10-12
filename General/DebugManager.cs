using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugManager : MonoBehaviour
{
    [SerializeField] DeferredLightsFeature.DebugMode debugMode;

    GUIStyle buttonSelected;
    GUIStyle buttonUnselected;

    GUIStyle current;

    void SetupSkins() 
    {
        buttonSelected = new GUIStyle(GUI.skin.button);
        buttonSelected.normal.textColor = Color.blue;
        buttonUnselected = new GUIStyle(GUI.skin.button);

        current = buttonUnselected;
    }

    void OnGUI()
    {
        if (buttonSelected == null || buttonUnselected == null) SetupSkins();

        var modes = System.Enum.GetValues(typeof(DeferredLightsFeature.DebugMode));

        Rect rect = new Rect(0, 50, 200, 50 * modes.Length);

        using (new GUILayout.AreaScope(rect))
        {
            foreach (var val in modes)
            {
                var eval = (DeferredLightsFeature.DebugMode) val;

                if (debugMode == eval) current = buttonSelected;
                else current = buttonUnselected;

                if (GUILayout.Button(eval.ToString(), current))
                {
                    debugMode = (DeferredLightsFeature.DebugMode)val;
                    Shader.SetGlobalInt("_DebugMode", (int)debugMode);
                }
            }
        }
    }
}
