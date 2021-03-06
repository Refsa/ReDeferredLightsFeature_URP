﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class DeferredLightsData : MonoBehaviour
{
    [SerializeField] Color color = Color.red;
    [SerializeField] float intensity = 1f;
    [SerializeField] float range = 3f;

    [Header("Debug and Testing")]
    [SerializeField] bool randomData = true;
    [SerializeField] bool displayVolume;

    public Color Color => color * intensity;
    public float RangeSqr => range * range;
    public float Range => range;
    public Vector2 Attenuation
    {
        get
        {
            float lightRangeSqr = range * range;
            float fadeStartDistanceSqr = 0.8f * 0.8f * lightRangeSqr;
            float fadeRangeSqr = (fadeStartDistanceSqr - lightRangeSqr);
            float oneOverFadeRangeSqr = 1.0f / fadeRangeSqr;
            float lightRangeSqrOverFadeRangeSqr = -lightRangeSqr / fadeRangeSqr;
            float oneOverLightRangeSqr = 1.0f / Mathf.Max(0.0001f, lightRangeSqr);

            return new Vector2(
                Application.isMobilePlatform || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Switch ? oneOverFadeRangeSqr : oneOverLightRangeSqr, 
                lightRangeSqrOverFadeRangeSqr
            );
        }
    }

    public void SetData(Color color, float intensity, float range)
    { 
        this.color = color;
        this.intensity = intensity;
        this.range = range;
    }

    void Awake()
    {
        // if (randomData)
        // {
        //     color = Random.ColorHSV();
        //     range = Random.Range(5f, 100f);
        //     intensity = Random.Range(1f, 5f);
        // }
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
        Gizmos.DrawSphere(transform.position, Mathf.Min(5f, range * 0.05f));
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
