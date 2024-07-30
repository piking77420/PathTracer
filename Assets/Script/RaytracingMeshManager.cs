using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;

public class RaytracingMeshManager : MonoBehaviour
{
    class MeshDataLists
    {
        public List<Triangle> triangles = new();
        public List<MeshInfo> meshInfo = new();
    }

    struct MeshInfo
    {
        public int firstTriangleIndex;
        public int numberOfTriangles;
        public Matrix4x4 WorldToLocalMatrix;
        public Matrix4x4 LocalToWorldMatrix;
        public RayTracingMaterial Material;

        public Vector3 boudMin;
        public Vector3 boudMax;
    }

    public ComputeBuffer modelBuffer;

    public ComputeBuffer triangleBuffer;

    MeshInfo[] meshInfo;


    public static Bounds GetMinMaxAbb(Bounds mesh,Transform transform) 
    {
        Matrix4x4 matrix = transform.localToWorldMatrix.transpose;

        Vector3 right = new Vector3(matrix.m00, matrix.m01, matrix.m02) * mesh.extents.x;
        Vector3 up = new Vector3(matrix.m10, matrix.m11, matrix.m12) * mesh.extents.y;
        Vector3 forward = new Vector3(matrix.m20, matrix.m21, matrix.m22) * mesh.extents.z;

        float i = Mathf.Abs(Vector3.Dot(right, Vector3.right) )+ Mathf.Abs(Vector3.Dot(up, Vector3.right) )+
            Mathf.Abs(Vector3.Dot(forward, Vector3.right));

        float j = Mathf.Abs(Vector3.Dot(right, Vector3.up) )+ Mathf.Abs(Vector3.Dot(up, Vector3.up) )+
           Mathf.Abs(Vector3.Dot(forward, Vector3.up));

        float k = Mathf.Abs(Vector3.Dot(right, Vector3.forward) )+ Mathf.Abs(Vector3.Dot(up, Vector3.forward) )+
           Mathf.Abs(Vector3.Dot(forward, Vector3.forward));

        Vector4 transformedCenter = transform.localToWorldMatrix * new Vector4(mesh.center.x, mesh.center.y, mesh.center.z, 0f);

        return new Bounds(transformedCenter, new Vector3(i, j, k) * 2f);
    }

    void GetTriangle(ref Triangle[] tri, Vector3[] verts, int[] indices, Vector3[] normals)
    {
        int triangleCount = indices.Length / 3;
        tri = new Triangle[triangleCount];

        for (int i = 0; i < triangleCount; i++)
        {
            int index0 = indices[i * 3];
            int index1 = indices[i * 3 + 1];
            int index2 = indices[i * 3 + 2];

            tri[i] = new Triangle(verts[index0], verts[index1], verts[index2], normals[index0], normals[index1], normals[index2]);
        }
    }
    public void UpateModelData(RayTracingModel[] models, Material rayTracingMaterial)
    {


        var data = CreateAllMeshData(models);
        meshInfo = data.meshInfo.ToArray();

        ShaderHelper.CreateStructuredBuffer<Triangle>(ref triangleBuffer, data.triangles.ToArray());
        ShaderHelper.CreateStructuredBuffer<MeshInfo>(ref modelBuffer, meshInfo);

        rayTracingMaterial.SetBuffer("triangles", triangleBuffer);
        rayTracingMaterial.SetInt("triangleCount", triangleBuffer.count);

        rayTracingMaterial.SetBuffer("modelInfo", modelBuffer);
        rayTracingMaterial.SetInt("modelCount", models.Length);
    }


    MeshDataLists CreateAllMeshData(RayTracingModel[] models)
    {
        MeshDataLists allData = new();
        Dictionary<Mesh, (int firstTriangleIndex, int triangleCount)> meshLookup = new();

        foreach (RayTracingModel model in models)
        {
            Mesh mesh = model.meshFilter.sharedMesh;

       

            if (!meshLookup.ContainsKey(mesh))
            {
                int triangleCount = mesh.triangles.Length / 3;
                meshLookup.Add(mesh,(allData.triangles.Count, triangleCount));

                Triangle[] triangles = new Triangle[triangleCount];

                GetTriangle(ref triangles, mesh.vertices, mesh.triangles, mesh.normals);
                allData.triangles.AddRange(triangles);
            }


            Bounds bounds = RaytracingMeshManager.GetMinMaxAbb(mesh.bounds, model.transform);

            // Create the mesh info
            allData.meshInfo.Add(new MeshInfo()
            {
                firstTriangleIndex = meshLookup[mesh].firstTriangleIndex,
                numberOfTriangles = meshLookup[mesh].triangleCount,
                WorldToLocalMatrix = model.transform.worldToLocalMatrix,
                LocalToWorldMatrix = model.transform.localToWorldMatrix,
                Material = model.material,
                boudMin = bounds.min,
                boudMax = bounds.max,
            });
        }

        return allData;
    }


    public void DisableBuffer()
    {
        if (modelBuffer != null)
        {
            modelBuffer.Release();
        }

        if (triangleBuffer != null)
        {
            triangleBuffer.Release();
        }
    }

}
