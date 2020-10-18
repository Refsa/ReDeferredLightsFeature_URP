using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SpawnLightsBox : MonoBehaviour
{
    [SerializeField] GameObject lightPrefab;
    [SerializeField] int lightCount = 128;

    [Header("Light Data")]
    [SerializeField] Vector2 sizeRange = new Vector2(5f, 200f);
    [SerializeField] Vector2 intensityRange = new Vector2(1f, 5f);

    BoxCollider boxCollider;

    void Start() 
    {
        DeleteLights();
        SpawnLights();
    }

    public void SpawnLights()
    {
        boxCollider = GetComponent<BoxCollider>();
        Vector3 halfSize = boxCollider.size * 0.5f;

        for (int i = 0; i < lightCount; i++)
        {
            var go = GameObject.Instantiate(lightPrefab, transform);

            Vector3 pos = 
                new Vector3(
                    Random.Range(-halfSize.x, halfSize.x),
                    Random.Range(-halfSize.y, halfSize.y),
                    Random.Range(-halfSize.z, halfSize.z)
                );

            go.transform.position = transform.position + boxCollider.center + pos;

            float range = Random.Range(sizeRange.x, sizeRange.y);
            var light = go.GetComponent<DeferredLightsData>();
            light.SetData(Random.ColorHSV(), Random.Range(intensityRange.x, intensityRange.y), range);
        }

        SendMessage("SpawnedLights", SendMessageOptions.DontRequireReceiver);
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
