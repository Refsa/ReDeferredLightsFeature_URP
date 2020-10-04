using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeferredLightsData : MonoBehaviour
{
    [SerializeField] Color color = Color.red;
    [SerializeField] float intensity = 1f;
    [SerializeField] float range = 3f;

    [Header("Debug and Testing")]
    [SerializeField] bool randomData = true;
    [SerializeField] bool displayVolume;

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
        if (displayVolume)
        {
            color.a = 0.25f;
            Gizmos.color = color;
            color.a = 1f;
            Gizmos.DrawSphere(transform.position, range);
        }
        
        Gizmos.color = color;
        Gizmos.DrawSphere(transform.position, 0.1f);
        Gizmos.DrawWireSphere(transform.position, range);
    }

    void OnEnable() 
    {
        DeferredLightsManager.AddLight(this);
    }

    void OnDestroy() 
    {
        DeferredLightsManager.RemoveLight(this);
    }
}
