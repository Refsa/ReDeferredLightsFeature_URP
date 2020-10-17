using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class DebugCameraFrustum : MonoBehaviour
{
    Vector3 lastPos;

    UnityEngine.Plane[] tempPlanes = new UnityEngine.Plane[6];

    new Camera camera;

    Mesh planeMesh;

    void OnEnable() 
    {
        camera = GetComponent<Camera>();

        lastPos = transform.position;    
        UpdateFrustum();
    }

    void UpdateFrustum()
    {
        GeometryUtility.CalculateFrustumPlanes(camera, tempPlanes);
    }
    
    void SetupPlaneMesh()
    {
        GameObject planeObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        planeMesh = planeObject.GetComponent<MeshFilter>().sharedMesh;
        GameObject.DestroyImmediate(planeObject);
    }

    void OnDrawGizmos() 
    {
        if (lastPos != transform.position)
        {
            UpdateFrustum();
            lastPos = transform.position;
        }

        if (planeMesh == null)
        {
            SetupPlaneMesh();
        }

        for (int i = 0; i < 6; i++)
        {
            var plane = tempPlanes[i];

            Gizmos.DrawWireMesh(planeMesh, 0, -plane.normal * plane.distance, Quaternion.FromToRotation(Vector3.up, plane.normal));
        }
    }
}
