using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class DeferredLightsManager : MonoBehaviour
{
    static DeferredLightsManager instance;
    public static DeferredLightsManager Instance => instance;

    List<DeferredLightsData> deferredLights;
    public static List<DeferredLightsData> Lights => instance.deferredLights;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        deferredLights = new List<DeferredLightsData>();

        deferredLights = GameObject.FindObjectsOfType<DeferredLightsData>().ToList();
    }

    public static void AddLight(DeferredLightsData light)
    {
        if (instance is null) return;

        instance.deferredLights.Add(light);
    }

    public static void RemoveLight(DeferredLightsData light)
    {
        if (instance is null) return;

        instance.deferredLights.Remove(light);
    }
}
