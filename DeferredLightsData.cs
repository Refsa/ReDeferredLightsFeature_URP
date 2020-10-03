using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeferredLightsData : MonoBehaviour
{   
    [SerializeField] Color color = Color.white;
    [SerializeField] float intensity = 1f;
    [SerializeField] float range = 3f;

    public Color Color => color;
    public float Intensity => intensity;
    public float Range => range;

    void OnDrawGizmos()
    {
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 0.1f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
