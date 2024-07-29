using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RaytracingManager : MonoBehaviour
{
    [SerializeField,Range(1,1000)]
    int nbrOfRayBound;

    [SerializeField, Range(1, 1000)]
    int nbrOfRayPerPixel;

    [SerializeField]
    bool UseShaderInEditor;

    [SerializeField]
    Material RayTracingMaterial;

    [SerializeField]
    List<RaytracingMat> rayTracingSpheres;

    ComputeBuffer computeBuffer;

    struct RayTracingSphere
    {
        public Vector3 pos;
        public float radius;
        public Vector4 color;
        public Vector4 emmisiveColor;
        public float emmisiveStrenght;
    }
    [ContextMenu("GetMaterial")]
    private void GetMaterial() 
    {
        rayTracingSpheres = FindObjectsOfType<RaytracingMat>().ToList();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (UseShaderInEditor || (Camera.current != null && Camera.current.name != "SceneCamera"))
        {
            UpdateCameraInfo(Camera.current);
            SetSphereInfo();
            SetSettings();
            Graphics.Blit(null, destination, RayTracingMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }
    private void SetSettings() 
    {
        RayTracingMaterial.SetInt("nbrOfRayBound", nbrOfRayBound);
        RayTracingMaterial.SetInt("nbrOfRayPerPixel", nbrOfRayPerPixel);

    }

    private void SetSphereInfo()
    {
        if (computeBuffer != null)
        {
            computeBuffer.Release();
        }

        int sizeOf = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RayTracingSphere));
        computeBuffer = new ComputeBuffer(rayTracingSpheres.Count, sizeOf);

        RayTracingSphere[] sphereData = new RayTracingSphere[rayTracingSpheres.Count];
        for (int i = 0; i < rayTracingSpheres.Count; i++)
        {
            sphereData[i] = new RayTracingSphere
            {
                pos = rayTracingSpheres[i].transform.position,
                radius = rayTracingSpheres[i].radius,
                color = rayTracingSpheres[i].color,
                emmisiveColor = rayTracingSpheres[i].emmisiveColor,
                emmisiveStrenght = rayTracingSpheres[i].emmisiveStrenght
            };
        }

        computeBuffer.SetData(sphereData);

        RayTracingMaterial.SetBuffer("spheres", computeBuffer);
        RayTracingMaterial.SetInt("nbrOfSphere", rayTracingSpheres.Count);
    }

    private void UpdateCameraInfo(Camera cam)
    {
        float planeHeight = cam.nearClipPlane * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2f;
        float planeWidth = planeHeight * cam.aspect;

        RayTracingMaterial.SetVector("CameraPos", cam.transform.position);
        RayTracingMaterial.SetVector("ViewParams", new Vector3(planeWidth, planeHeight, cam.nearClipPlane));
        RayTracingMaterial.SetMatrix("CamLocalToWorldMatrix", cam.transform.localToWorldMatrix);
    }

    private void OnDisable()
    {
        if (computeBuffer != null)
        {
            computeBuffer.Release();
        }
    }
}
