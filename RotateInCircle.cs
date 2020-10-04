using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateInCircle : MonoBehaviour
{
    Vector3 center;
    float speed;

    void Awake()
    {
        center = transform.position + Vector3.right * Random.Range(-10f, 10f);
        speed = Random.Range(-25f, 25f);
    }

    void Update()
    {
        transform.RotateAround(center, Vector3.up, speed * Time.deltaTime);
    }
} 
