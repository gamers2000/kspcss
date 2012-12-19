using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class PropObject : MonoBehaviour
{
    public PropTools.Prop prop;

    public static PropObject Create(Transform parent, PropTools.Prop prop)
    {
        GameObject newPropGO = new GameObject();
        newPropGO.name = prop.propName;
        newPropGO.transform.parent = parent;

        PropObject newProp = newPropGO.AddComponent<PropObject>();
        newProp.prop = prop;

        Bounds propBounds = CreateProxies(newPropGO, prop);

        BoxCollider collider = newPropGO.AddComponent<BoxCollider>();
        collider.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
        collider.center = propBounds.center;
        collider.size = propBounds.size;

        return newProp;
    }

    public static Bounds CreateProxies(GameObject go, PropTools.Prop prop)
    {
        List<Mesh> proxyMeshes = new List<Mesh>();
        List<Material> proxyMaterials = new List<Material>();

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter cubeMF = cube.GetComponent<MeshFilter>();
        Mesh cubeMesh = cubeMF.sharedMesh;

        foreach (PropTools.Proxy proxy in prop.proxies)
        {
            Vector3[] verts = cubeMesh.vertices;

            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Scale(proxy.size);
                verts[i] += proxy.center;
            }

            Mesh proxyMesh = new Mesh();
            proxyMesh.vertices = verts;
            proxyMesh.triangles = cubeMesh.triangles;
            proxyMesh.uv = cubeMesh.uv;
            proxyMesh.normals = cubeMesh.normals;


            Material proxyMat = new Material(Shader.Find("Diffuse"));
            proxyMat.color = proxy.color;
            proxyMat.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
            proxyMeshes.Add(proxyMesh);
            proxyMaterials.Add(proxyMat);
        }


        CombineInstance[] cI = new CombineInstance[proxyMeshes.Count];
        for (int i = 0; i < proxyMeshes.Count; i++)
        {
            cI[i].mesh = proxyMeshes[i];
        }


        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(cI, false, false);
        combinedMesh.RecalculateBounds();

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
        mf.sharedMesh = combinedMesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.hideFlags = HideFlags.HideInInspector | HideFlags.NotEditable;
        mr.sharedMaterials = proxyMaterials.ToArray();


        DestroyImmediate(cube);
        for (int i = 0; i < proxyMeshes.Count; i++)
        {
            DestroyImmediate(proxyMeshes[i]);
        }

        return combinedMesh.bounds;
    }
}