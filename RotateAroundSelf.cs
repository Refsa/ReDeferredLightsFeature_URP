using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateAroundSelf : MonoBehaviour
{
    float speed;

    void Awake() 
    {
        speed = Random.Range(-50f, 50f);
    }

    void Update() 
    {
        transform.rotation *= Quaternion.Euler(0f, speed * Time.deltaTime, 0f);
    }
}
