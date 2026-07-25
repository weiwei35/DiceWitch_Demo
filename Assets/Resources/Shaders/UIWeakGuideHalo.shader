Shader "DiceWitch/UI/WeakGuideHalo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _SpriteUvRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _SpriteUvCenter ("Sprite UV Center", Vector) = (0.5,0.5,0,0)
        _SpriteUvScale ("Sprite UV Scale", Vector) = (1,1,0,0)
        _HaloRadius ("Halo Radius", Float) = 4
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_TexelSize;
            float4 _SpriteUvRect;
            float4 _SpriteUvCenter;
            float4 _SpriteUvScale;
            float _HaloRadius;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed SampleSpriteAlpha(float2 uv)
            {
                fixed inside = step(_SpriteUvRect.x, uv.x)
                    * step(_SpriteUvRect.y, uv.y)
                    * step(uv.x, _SpriteUvRect.z)
                    * step(uv.y, _SpriteUvRect.w);
                fixed4 sampleColor = tex2D(
                    _MainTex,
                    clamp(uv, _SpriteUvRect.xy, _SpriteUvRect.zw))
                    + _TextureSampleAdd;
                return sampleColor.a * inside;
            }

            fixed SampleRing(float2 uv, float2 offset)
            {
                fixed ring = 0;
                ring = max(ring, SampleSpriteAlpha(uv + float2(offset.x, 0)));
                ring = max(ring, SampleSpriteAlpha(uv + float2(-offset.x, 0)));
                ring = max(ring, SampleSpriteAlpha(uv + float2(0, offset.y)));
                ring = max(ring, SampleSpriteAlpha(uv + float2(0, -offset.y)));
                ring = max(ring, SampleSpriteAlpha(uv + offset));
                ring = max(ring, SampleSpriteAlpha(uv + float2(-offset.x, offset.y)));
                ring = max(ring, SampleSpriteAlpha(uv + float2(offset.x, -offset.y)));
                ring = max(ring, SampleSpriteAlpha(uv - offset));
                return ring;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 sourceUv = (input.texcoord - _SpriteUvCenter.xy)
                    * _SpriteUvScale.xy
                    + _SpriteUvCenter.xy;
                fixed centerAlpha = SampleSpriteAlpha(sourceUv);
                float2 nearOffset = _MainTex_TexelSize.xy * max(1, _HaloRadius * 0.45);
                float2 farOffset = _MainTex_TexelSize.xy * max(1, _HaloRadius);
                fixed nearRing = saturate(SampleRing(sourceUv, nearOffset) - centerAlpha);
                fixed farRing = saturate(SampleRing(sourceUv, farOffset) - centerAlpha);
                fixed halo = max(nearRing, farRing * 0.42);
                fixed alpha = halo * input.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return fixed4(input.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
