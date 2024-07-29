using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.GraphicsBuffer;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTracingManager : MonoBehaviour
{
    [SerializeField, Range(1, 1000)]
    int nbrOfRayBound = 10;

    [SerializeField, Range(1, 1000)]
    int nbrOfRayPerPixel = 1;

    [SerializeField]
    bool UseShaderInEditor = true;

    [SerializeField]
    Material RayTracingMaterial;

    [SerializeField]
    Material AccumulateMaterial;

    [SerializeField]
    List<RaytracingMat> rayTracingSpheres;

    ComputeBuffer computeBuffer;

    [SerializeField]
    int numRenderedFrames;

    RenderTexture resultTexture;

    struct RayTracingSphere
    {
        public Vector3 pos;
        public float radius;
        public Vector4 color;
        public Vector4 emissiveColor;
        public float emissiveStrength;
    }

    [ContextMenu("GetMaterial")]
    private void GetMaterial()
    {
        rayTracingSpheres = FindObjectsOfType<RaytracingMat>().ToList();
    }

    void ResizeRenderTarget(RenderTexture renderTexture)
    {
        if (renderTexture == null || renderTexture.width != Screen.width || renderTexture.height != Screen.height)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }

            renderTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.DefaultHDR);
            renderTexture.enableRandomWrite = true;
            renderTexture.Create();
        }
    }

    void InitFrame()
    {
        if (resultTexture == null)
        {
            RenderTextureDescriptor renderTextureDesc = new RenderTextureDescriptor
            {
                width = Screen.width,
                height = Screen.height,
                colorFormat = RenderTextureFormat.DefaultHDR, // Use appropriate format
                depthBufferBits = 24, // Depth buffer bits, adjust as needed
                msaaSamples = 1, // Anti-aliasing samples, adjust as needed
                volumeDepth = 1, // For 2D textures, volume depth should be 1
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D, // Assuming a 2D texture
                useMipMap = false, // Set to true if mipmaps are needed
                autoGenerateMips = false, // Set to true if mipmaps should be generated automatically
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear // Ensure sRGB is correctly set based on the active color space
            };

            resultTexture = new RenderTexture(renderTextureDesc)
            {
                enableRandomWrite = true
            };
            resultTexture.Create();
        }

        ResizeRenderTarget(resultTexture);
        UpdateCameraInfo(Camera.current);
        SetSphereInfo();
        SetSettings();
    }

    private void OnRenderImage(RenderTexture src, RenderTexture target)
    {
        bool isSceneCam = Camera.current.name == "SceneCamera";

        if (isSceneCam)
        {
            if (UseShaderInEditor)
            {
                InitFrame();
                Graphics.Blit(null, target, RayTracingMaterial);
            }
            else
            {
                Graphics.Blit(src, target); // Draw the unaltered camera render to the screen
            }
        }
        else
        {
            InitFrame();

            // Create copy of the previous frame
            RenderTexture prevFrameCopy = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
            Graphics.Blit(resultTexture, prevFrameCopy);

            // Run the ray tracing shader and draw the result to a temporary texture
            RayTracingMaterial.SetInt("Frame", numRenderedFrames);
            RenderTexture currentFrame = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, GraphicsFormat.R32G32B32A32_SFloat);
            Graphics.Blit(null, currentFrame, RayTracingMaterial);

            // Accumulate
            AccumulateMaterial.SetInt("_Frame", numRenderedFrames);
            AccumulateMaterial.SetTexture("_PrevFrame", prevFrameCopy);
            Graphics.Blit(currentFrame, resultTexture, AccumulateMaterial);

            // Draw result to screen
            Graphics.Blit(resultTexture, target);

            // Release temporary render textures
            RenderTexture.ReleaseTemporary(currentFrame);
            RenderTexture.ReleaseTemporary(prevFrameCopy);

            numRenderedFrames += Application.isPlaying ? 1 : 0;
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
                emissiveColor = rayTracingSpheres[i].emissiveColor,
                emissiveStrength = rayTracingSpheres[i].emissiveStrength
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

        if (resultTexture != null)
        {
            resultTexture.Release();
        }
    }

    private void Start()
    {
        numRenderedFrames = 0;
    }
}
