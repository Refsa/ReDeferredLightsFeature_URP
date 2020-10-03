using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeferredLightsData : MonoBehaviour
{
    [SerializeField] Color color = Color.red;
    [SerializeField] float intensity = 1f;
    [SerializeField] float range = 3f;

    [SerializeField] bool randomData = true;

    public Color Color => color;
    public float Intensity => intensity;
    public float Range => range;

    void Awake()
    {
        if (randomData)
        {
            color = Random.ColorHSV();
            range = Random.Range(1f, 20f);
            intensity = Random.Range(0.25f, 1.5f);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 0.1f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
