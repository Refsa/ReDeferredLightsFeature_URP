﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnLightsTest : MonoBehaviour
{
    [SerializeField] GameObject lightPrefab;
    [SerializeField] int lightCount = 128;
    [SerializeField] float lightRange = 100f;

    [Header("Light Data")]
    [SerializeField] Vector2 sizeRange = new Vector2(5f, 200f);
    [SerializeField] Vector2 intensityRange = new Vector2(1f, 5f);

    void Start() 
    {
        DeleteLights();
        SpawnLights();    
    }

    public void SpawnLights()
    {
        for (int i = 0; i < lightCount; i++)
        {
            var go = GameObject.Instantiate(lightPrefab, transform);

            Vector2 pos = Random.insideUnitCircle * lightRange;

            float range = Random.Range(sizeRange.x, sizeRange.y);

            go.transform.position = new Vector3(pos.x, range * 0.25f, pos.y);

            var light = go.GetComponent<DeferredLightsData>();
            light.SetData(Random.ColorHSV(), Random.Range(intensityRange.x, intensityRange.y), range);
        }
    }

    public void DeleteLights()
    {
        while (transform.childCount > 0)
        {
            GameObject.DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    void OnGUI() 
    {
        Rect rect = new Rect(Screen.width - 200f, 50f, 150f, 75f);

        using (new GUILayout.AreaScope(rect))
        {
            GUILayout.Label($"Light Count ({lightCount})");
            lightCount = (int)GUILayout.HorizontalSlider(lightCount, 1, 1 << 16);

            if (GUILayout.Button("Respawn Lights"))
            {
                DeleteLights();
                SpawnLights();
            }
        }
    }
}
