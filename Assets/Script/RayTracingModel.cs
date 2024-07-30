using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingModel : MonoBehaviour
{
    public MeshFilter meshFilter;
    public RayTracingMaterial material;
    private MeshRenderer meshRenderer;

    private int ModelMeshId;

    [SerializeField]
    bool drawAABB;

    private void OnDrawGizmos()
    {
        if (!drawAABB)
            return;

        Bounds bound = RaytracingMeshManager.GetMinMaxAbb(meshFilter.sharedMesh.bounds, transform);
        Gizmos.DrawWireCube(bound.center, bound.size);
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
