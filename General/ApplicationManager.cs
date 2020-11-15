using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplicationManager
{
    [RuntimeInitializeOnLoadMethod]
    public static void Setup()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 500;
    }
}
