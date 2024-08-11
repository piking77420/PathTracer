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

            struct BvhNode
            {
                float3 boxMin;
                int child1Index;
                float3 boxMax;
                int child2Index;
                int triangleIndex;
                int triangleCount;
            };

          

            struct ModelInfo
            {
                int nodeOffset;
                int triangleOffSet;
                float4x4 WorldToLocalMatrix;
                float4x4 LocalToWorldMatrix;
                RayTracingMaterial material;
            };

            struct TriangleHitInfo
            {
                float3 hitPoint;
                bool didHit;
                float3 normal;
                float dst;
            };

           struct ModelHitInfo
			{
				float3 hitPoint;
				bool didHit;
				float3 normal;
				float dst;
				RayTracingMaterial material;
			};

            struct Triangle
            {
                float3 posA;
                float3 posB;
                float3 posC;

                float3 normalA;
                float3 normalB;
                float3 normalC;
            };
            // Info
            int Frame;

            // CameraInfo
            float3 CameraPos;
            float3 ViewParams;
            float4x4 CamLocalToWorldMatrix;

            //Settings
            int nbrOfRayBound;
            int nbrOfRayPerPixel;

            // Buffers
            StructuredBuffer<ModelInfo> modelInfo;
            int modelCount;

            StructuredBuffer<Triangle> triangles;
            int triangleCount;


            StructuredBuffer<BvhNode> nodes;

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

           

            // Thanks to https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
			bool RayBoundingBox(Ray ray, float3 boxMin, float3 boxMax)
			{
				float3 invDir = 1 / ray.dir;
				float3 tMin = (boxMin - ray.ori) * invDir;
				float3 tMax = (boxMax - ray.ori) * invDir;
				float3 t1 = min(tMin, tMax);
				float3 t2 = max(tMin, tMax);
				float tNear = max(max(t1.x, t1.y), t1.z);
				float tFar = min(min(t2.x, t2.y), t2.z);
				return tNear <= tFar;
			};


           // Calculate the intersection of a ray with a triangle using Möller–Trumbore algorithm
			// Thanks to https://stackoverflow.com/a/42752998
			TriangleHitInfo RayTriangle(Ray ray, Triangle tri)
			{
				float3 edgeAB = tri.posB - tri.posA;
				float3 edgeAC = tri.posC - tri.posA;
				float3 normalVector = cross(edgeAB, edgeAC);
				float3 ao = ray.ori - tri.posA;
				float3 dao = cross(ao, ray.dir);

				float determinant = -dot(ray.dir, normalVector);
				float invDet = 1 / determinant;

				// Calculate dst to triangle & barycentric coordinates of intersection point
				float dst = dot(ao, normalVector) * invDet;
				float u = dot(edgeAC, dao) * invDet;
				float v = -dot(edgeAB, dao) * invDet;
				float w = 1 - u - v;

				// Initialize hit info
				TriangleHitInfo hitInfo;
				hitInfo.didHit = determinant >= 1E-8 && dst >= 0 && u >= 0 && v >= 0 && w >= 0;
				hitInfo.hitPoint = ray.ori + ray.dir * dst;
				hitInfo.normal = normalize(tri.normalA * w + tri.normalB * u + tri.normalC * v);
				hitInfo.dst = dst;
				return hitInfo;
			}


            bool isLeafNode(BvhNode n)
            {
                return n.child1Index == 0 && n.child2Index == 0; 
            }

           
          
            TriangleHitInfo RayTriangleBVH(inout Ray ray, float rayLength, int nodeOffset, int triOffset)
            {
                TriangleHitInfo result = (TriangleHitInfo)0;
                result.dst = rayLength;
    
                int stackIndex[16];
                int stackIterator = 0;
                stackIndex[stackIterator++] = nodeOffset;

                bool isParentChildA = false;

                while(stackIterator > 0)
                {
                    BvhNode node = nodes[stackIndex[--stackIterator]];
                    bool intersect = RayBoundingBox(ray, node.boxMin, node.boxMax);

                    if (intersect)
                    {
                        if (isLeafNode(node))
                        {
                            for (int i = triOffset + node.triangleIndex; i <  triOffset + node.triangleIndex + node.triangleCount; i++)
                            {
                                Triangle tri = triangles[i];

                                TriangleHitInfo hit = RayTriangle(ray, tri); 

                                if (hit.didHit && result.dst > hit.dst)
                                {
                                    result = hit;
                                }
                            }
                        }
                        else
                        {
                            
                            stackIndex[stackIterator++] = nodeOffset + node.child1Index;
                            stackIndex[stackIterator++] = nodeOffset + node.child2Index;
                        }
                    }
                 
                        
                }
                return result;
            } 
                
          

              
            

            ModelHitInfo CalculateRayCollisionBruteForce(Ray worldRay)
            {
                ModelHitInfo result;
                result.didHit = false;
                result.dst = 1.0 / 0.0; // Initialize to infinity

                Ray localRay;

                for (int i = 0; i < modelCount; i++)
                {
                    ModelInfo model = modelInfo[i];
                    localRay.ori = mul(model.WorldToLocalMatrix, float4(worldRay.ori,1)).xyz;
                    localRay.dir = mul(model.WorldToLocalMatrix, float4(worldRay.dir,0)).xyz;

                    int nodeoff = model.nodeOffset;
                    if (!RayBoundingBox(localRay,  nodes[nodeoff].boxMin, nodes[nodeoff].boxMax))
                       continue;

                    for (int k = model.triangleOffSet; k < model.triangleOffSet + nodes[nodeoff].triangleCount; k++)
                    {          
                        TriangleHitInfo hit = RayTriangle(localRay, triangles[k]); 

                        if (hit.didHit && result.dst > hit.dst)
                        {
                            result.didHit = true;
						    result.dst = hit.dst;
						    result.normal = normalize(mul(model.LocalToWorldMatrix, float4(hit.normal, 0)));
						    result.hitPoint = worldRay.ori + worldRay.dir * hit.dst;
						    result.material = model.material;
                        }
                    }
                }

                return result;
            }


            ModelHitInfo CalculateRayCollision(Ray worldRay)
            {
                ModelHitInfo result;
                result.didHit = false;
                result.dst = 3.402823e+38; // Initialize to infinity

                Ray localRay;

                for (int i = 0; i < modelCount; i++)
                {
                    ModelInfo model = modelInfo[i];
                    localRay.ori = mul(model.WorldToLocalMatrix, float4(worldRay.ori,1)).xyz;
                    localRay.dir = mul(model.WorldToLocalMatrix, float4(worldRay.dir,0)).xyz;

                    TriangleHitInfo hit = RayTriangleBVH(localRay, result.dst , model.nodeOffset, model.triangleOffSet);

                    if (result.dst > hit.dst)
                    {
                        result.didHit = true;
						result.dst = hit.dst;
						result.normal = normalize(mul(model.LocalToWorldMatrix, float4(hit.normal, 0)));
						result.hitPoint = worldRay.ori + worldRay.dir * hit.dst;
						result.material = model.material;
                    }

                }

                return result;
            }

            float3 TraceRay(Ray r, inout uint seed)
            {

                bool useBvh = true;

                float3 incomingLight = float3(0, 0, 0);
                float3 rayColor = float3(1, 1, 1);

                bool hasHitSomething = false;

                for (int i = 0; i <= nbrOfRayBound; i++)
                {
                    ModelHitInfo hit = (ModelHitInfo)0;

                    if (useBvh)
                    {
                       hit = CalculateRayCollision(r);
                    }
                    else
                    {
                        hit = CalculateRayCollisionBruteForce(r);
                    }

                    if (hit.didHit)
                    {
                       RayTracingMaterial mat = hit.material;

                       r.ori = hit.hitPoint;
                       //r.dir = reflect(r.dir,hit.normal);
                       r.dir = RandomRayHemisphere(hit.normal, seed);

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

                for (int rayIndex = 0; rayIndex < nbrOfRayPerPixel; rayIndex++)
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