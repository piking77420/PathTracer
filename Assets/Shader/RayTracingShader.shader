Shader "Unlit/NewUnlitShader"
{

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }


            struct Ray
            {
                float3 ori;
                float3 dir;
            };

            struct RayTracingMaterial
            {
                float4 color;
                float4 emmisiveColor;
                float emmisiveStrenght;
            };

            struct Sphere
            {
                float3 pos;
                float radius;
                RayTracingMaterial material;
            };

            struct HitInfo
            {
                float3 hitPoint;
                bool didHit;
                float3 normal;
                float dst;
                RayTracingMaterial material;
            };

            int Frame;

            float3 CameraPos;
            float3 ViewParams;
            float4x4 CamLocalToWorldMatrix;
            int nbrOfRayBound;
            int nbrOfRayPerPixel;

            StructuredBuffer<Sphere> spheres;
            int nbrOfSphere;

            uint GetPixelIndex(float2 uv)
            {
                uint2 numPixels = (uint2)_ScreenParams.xy;
                uint2 pixelCoord = (uint2)(numPixels * uv);
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;

                return (pixelIndex) + Frame * 719393;
            }

	        uint NextRandom(inout uint state)
			{
				state = state * 747796405 + 2891336453;
				uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
				result = (result >> 22) ^ result;
				return result;
			}

			float RandomValue(inout uint state)
			{
				return NextRandom(state) / 4294967295.0; // 2^32 - 1
			}

            float RandomValueNormalDistribution(inout uint seed)
            {
                // Using Box-Muller transform
                float theta = 2 * 3.1415926 * RandomValue(seed);
                float rho = sqrt(-2 * log(RandomValue(seed)));
                return rho * cos(theta);
            }

           
            float3 RandomDir(inout uint seed)
            {
               // Thanks to https://math.stackexchange.com/a/1585996
				float x = RandomValueNormalDistribution(seed);
				float y = RandomValueNormalDistribution(seed);
				float z = RandomValueNormalDistribution(seed);
				return normalize(float3(x, y, z));
            }

            float3 RandomRayHemisphere(float3 normal, inout uint seed)
            {
                float3 randDir = RandomDir(seed);
                return randDir * sign(dot(normal, randDir));
            }


            HitInfo RaySphere(Ray r, Sphere sphere)
            {
                HitInfo hit = (HitInfo)0;
                // Add offset to the ray in order to use the equation of sphere  
                float3 offsetRay = r.ori - sphere.pos;

                float a = dot(r.dir, r.dir);
                float b = 2.f * dot(offsetRay, r.dir);
                float c = dot(offsetRay, offsetRay) - sphere.radius * sphere.radius;
                float disc = b * b - 4.f * a * c;

                if (disc >= 0) // there is at least one solution in R 
                {
                    float dst = (-b - sqrt(disc)) / 2.f * a;

                    // only use hit that are in front of the ray not behin
                    if (dst >= 0)
                    {
                        hit.didHit = true;
                        hit.dst = dst;
                        hit.hitPoint = r.ori + r.dir * dst;
                        hit.normal = normalize(hit.hitPoint - sphere.pos);
                        hit.material = sphere.material;
                    }
                }
                return hit;
            }

            HitInfo ComputeRaySphere(Ray r)
            {
                HitInfo returnHitInfo = (HitInfo)0;
                returnHitInfo.dst = 1.0 / 0.0;

                for (int i = 0; i < nbrOfSphere; i++)
                {
                    HitInfo currentHitInfo = (HitInfo)0;
                    currentHitInfo = RaySphere(r, spheres[i]);

                    if (currentHitInfo.didHit && returnHitInfo.dst > currentHitInfo.dst)
                    {
                        returnHitInfo = currentHitInfo;
                    }
                }

                return returnHitInfo;
            }

            float3 TraceRay(Ray r, inout uint seed)
            {
                float3 incomingLight = float3(0, 0, 0);
                float3 rayColor = float3(1, 1, 1);

                bool hasHitSomething = false;

                for (int i = 0; i <= nbrOfRayBound; i++)
                {
                    HitInfo hit = ComputeRaySphere(r);

                    if (hit.didHit)
                    {
                        r.ori = hit.hitPoint;
                        r.dir = RandomRayHemisphere(hit.normal, seed);

                        RayTracingMaterial mat = hit.material;
                        float3 emmitedLight = mat.emmisiveColor * mat.emmisiveStrenght;

                        float lightStreng = dot(hit.normal, r.dir);

                        incomingLight += emmitedLight * rayColor;
                        rayColor *= mat.color * lightStreng;

                        hasHitSomething = true;
                    }
                    else
                    {
                        //incomingLight += float3(0.1, 0.2, 0.4);
                        break;
                    }
                }


                return incomingLight;
            }


            fixed4 frag(const v2f i) : SV_Target
            {
                uint seed = GetPixelIndex(i.uv);

                
                const float3 viewPointLocal = float3(i.uv - 0.5, 1) * ViewParams;
                const float3 viewPoint = mul(CamLocalToWorldMatrix, float4(viewPointLocal, 1));

                Ray ray;
                ray.ori = CameraPos;
                ray.dir = normalize(viewPoint - ray.ori);

                float3 luminance = 0;

                for (int rayIndex = 0; rayIndex <= nbrOfRayPerPixel; rayIndex++)
                {
                    luminance += TraceRay(ray, seed);
                }
                luminance /= nbrOfRayPerPixel;

                return float4(luminance, 1.0);
            }
            ENDCG
        }
    }
}