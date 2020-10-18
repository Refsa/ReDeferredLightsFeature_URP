using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

[RequireComponent(typeof(BoxCollider))]
public class MoveLightsInsideBox : MonoBehaviour
{
    [SerializeField] Vector2 speedRange = new Vector2(0.1f, 5f);

    Transform[] lightTransforms;
    NativeArray<Vector3> velocities;
    TransformAccessArray transformAccessArray;

    Bounds boxBounds;

    void SpawnedLights()
    {
        lightTransforms = transform.GetComponentsInChildren<Transform>().Where(t => t != transform).ToArray();

        if (transformAccessArray.isCreated) transformAccessArray.Dispose();
        transformAccessArray = new TransformAccessArray(lightTransforms);

        if (velocities.IsCreated) velocities.Dispose();
        velocities = new NativeArray<Vector3>(lightTransforms.Length, Allocator.Persistent);
        for (int i = 0; i < lightTransforms.Length; i++)
        {
            velocities[i] = Random.insideUnitSphere * Random.Range(speedRange.x, speedRange.y);
        }

        BoxCollider rb = GetComponent<BoxCollider>();
        boxBounds = new Bounds(transform.position + rb.center, rb.size);
    }

    void OnDestroy() 
    {
        if (transformAccessArray.isCreated) transformAccessArray.Dispose();
        if (velocities.IsCreated) velocities.Dispose();    
    }

    void OnDisable() 
    {
        if (transformAccessArray.isCreated) transformAccessArray.Dispose();
        if (velocities.IsCreated) velocities.Dispose();    
    }

    void Update() 
    {
        if (lightTransforms == null) return;

        var transformJob = 
            new TransformJob{
                Velocities = velocities,
                DeltaTime = Time.deltaTime,
                BoxBounds = boxBounds
            };

        transformJob.Schedule(transformAccessArray).Complete();
    }

    public struct TransformJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Vector3> Velocities;

        [ReadOnly] public float DeltaTime;
        [ReadOnly] public Bounds BoxBounds;

        public void Execute(int index, TransformAccess transform)
        {
            Vector3 nextPos = transform.position + Velocities[index] * DeltaTime;

            if (!BoxBounds.Contains(nextPos))
            {
                Velocities[index] = -Velocities[index];
                nextPos = transform.position + Velocities[index] * DeltaTime;
            }

            transform.position = nextPos;
        }
    }
}
