using System.Collections;
using System.Collections.Generic;
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

        for (int i = 0; i < Bvh.BvhMaxDepth; i++)
        {
            Bounds b = RaytracingMeshManager.GetMinMaxAbbWorld(bvh.BvhNodes[i].bounds, transform);

            Gizmos.DrawWireCube(b.center, b.size);
            

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
