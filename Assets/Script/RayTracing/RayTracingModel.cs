using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RayTracingModel : MonoBehaviour
{
    public MeshFilter meshFilter;
    public RayTracingMaterial material;
    private MeshRenderer meshRenderer;

    [SerializeField]
    bool DrawBVH;

    [SerializeField,Range(0, Bvh.BvhMaxDepth)]
    int CurrentDrawDepth;


    private void OnDrawGizmos()
    {
        if (!DrawBVH)
            return;

        Gizmos.color = Color.green;
        int triangleCount = meshFilter.sharedMesh.vertices.Length / 3;
        Triangle[] triangles = new Triangle[triangleCount];
        RaytracingMeshManager.GetTriangle(ref triangles, meshFilter.sharedMesh.vertices, meshFilter.sharedMesh.triangles, meshFilter.sharedMesh.normals);

        Bvh bvh = new Bvh(meshFilter.sharedMesh.vertices, meshFilter.sharedMesh.triangles, meshFilter.sharedMesh.normals, meshFilter.sharedMesh.bounds);

        var nodes = bvh.BvhNodes;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i].depht == CurrentDrawDepth)
            {
                Bounds b1 = new Bounds();
                b1.SetMinMax(nodes[i].bounds.Min, nodes[i].bounds.Max);
                Bounds b = RaytracingMeshManager.GetMinMaxAbbWorld(b1, transform);
                Gizmos.DrawWireCube(b.center, b.size);
            }

         
        }
    }

    private void OnValidate()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
