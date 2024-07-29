Shader "Unlit/AccumulateFrameShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
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

	        sampler2D _MainTex;
            sampler2D _PrevFrame;
            int _Frame;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.uv.x, 1.0 - v.uv.y); 
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv);
                float4 colPrev = tex2D(_PrevFrame, i.uv);

                float weight = 1.0 / (_Frame + 1);
                float4 accumulatedCol = saturate(colPrev * (1 - weight) + col * weight);

                return accumulatedCol;
            }
            ENDCG
        }
    }
}
