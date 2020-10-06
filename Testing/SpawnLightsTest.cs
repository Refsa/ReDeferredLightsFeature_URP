using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnLightsTest : MonoBehaviour
{
    [SerializeField] GameObject lightPrefab;
    [SerializeField] int spawnCount = 256;
    [SerializeField] float spawnRange = 256f;

    void Start()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            var lightgo = GameObject.Instantiate(lightPrefab, transform);

            Vector2 pos = Random.insideUnitCircle * spawnRange;
            lightgo.transform.localPosition = new Vector3(pos.x, 0, pos.y);
        }
    }

    void Update()
    {
        
    }
}
