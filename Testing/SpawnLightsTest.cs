using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnLightsTest : MonoBehaviour
{
    [SerializeField] GameObject lightPrefab;
    [SerializeField] int lightCount = 128;
    [SerializeField] float lightRange = 100f;

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

            go.transform.position = new Vector3(pos.x, Random.Range(3f, 30f), pos.y);

            var light = go.GetComponent<DeferredLightsData>();
            light.SetData(Random.ColorHSV(), Random.Range(1f, 5f), Random.Range(5f, 200f));
        }
    }

    public void DeleteLights()
    {
        while (transform.childCount > 0)
        {
            GameObject.DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}
