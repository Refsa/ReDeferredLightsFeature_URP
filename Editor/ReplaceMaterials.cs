#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class ReplaceMaterials
{
    [MenuItem("ReLightsURP/Replace Materials")]
    public static void ReplaceMaterialsInHierarchy()
    {
        var selected = Selection.activeGameObject;
        if (selected is null) return;

        var dflMaterial = new Material(Shader.Find("Refsa/Deferred Lit"));

        var renderers = selected.GetComponentsInChildren<MeshRenderer>();

        foreach (var r in renderers)
        {
            if (r.sharedMaterial != dflMaterial)
            {
                ReplaceMaterial(r, dflMaterial);
            }
        }
    }

    static void ReplaceMaterial(MeshRenderer renderer, Material material)
    {
        var materials = new Material[renderer.sharedMaterials.Length];

        for (int i = 0; i < renderer.sharedMaterials.Length; i++)
        {
            var cmat = renderer.sharedMaterials[i];
            var nmat = new Material(material);

            nmat.mainTexture = cmat.mainTexture;
            nmat.color = cmat.color;
            nmat.name = cmat.name;

            materials[i] = nmat;
        }

        renderer.sharedMaterials = materials;
    }
}
#endif