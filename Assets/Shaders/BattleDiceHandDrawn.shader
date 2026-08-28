Shader "DiceWitch/Battle Dice Hand Drawn"
{
    Properties
    {
        _Color ("Body Color", Color) = (1,1,1,1)
        _OutlineColor ("Main Outline", Color) = (0.025,0.018,0.03,1)
        _OutlineWidth ("Main Outline Width (px)", Range(0, 8)) = 2.4
        _SecondaryOutlineColor ("Secondary Stroke", Color) = (0.12,0.055,0.1,0.42)
        _SecondaryOutlineWidth ("Secondary Stroke Width (px)", Range(0, 10)) = 4.1
        _SecondaryBreakup ("Frayed Edge Breakup", Range(0, 0.9)) = 0.42
        _FuzzPixelScale ("Frayed Edge Pixel Scale", Range(0.5, 4)) = 1
        _OutlineRoughness ("Static Outline Roughness (px)", Range(0, 2)) = 0.45
        _JitterStrength ("Settled Jitter (px)", Range(0, 3)) = 0.8
        _JitterFPS ("Settled Jitter FPS", Range(1, 20)) = 8

        _GrainTex ("Optional Paper / Crayon Grain", 2D) = "gray" {}
        _GrainScale ("Grain Scale", Range(1, 60)) = 22
        _GrainStrength ("Procedural Grain", Range(0, 0.4)) = 0.12
        _GrainTextureStrength ("Texture Grain", Range(0, 0.4)) = 0

        _ShadowBand ("Toon Shadow Threshold", Range(0, 1)) = 0.48
        _ShadowStrength ("Rolling Shadow Strength", Range(0, 0.8)) = 0.28
        _SettledFlatness ("Settled Face Flatness", Range(0, 1)) = 0.88
        [HideInInspector] _HandDrawSettled ("Settled", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 250

        CGINCLUDE
        #include "UnityCG.cginc"

        fixed4 _OutlineColor;
        fixed4 _SecondaryOutlineColor;
        float _OutlineWidth;
        float _SecondaryOutlineWidth;
        float _SecondaryBreakup;
        float _FuzzPixelScale;
        float _OutlineRoughness;
        float _JitterStrength;
        float _JitterFPS;
        float _HandDrawSettled;

        struct OutlineV2F
        {
            float4 pos : SV_POSITION;
        };

        float OutlineHash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        OutlineV2F ExpandOutline(appdata_base v, float width, float seed)
        {
            OutlineV2F output;
            float4 clipPosition = UnityObjectToClipPos(v.vertex);
            float4 clipCenter = UnityObjectToClipPos(float4(0, 0, 0, 1));
            float2 pixelDirection = (clipPosition.xy / clipPosition.w - clipCenter.xy / clipCenter.w)
                * _ScreenParams.xy;
            float2 direction = pixelDirection * rsqrt(max(dot(pixelDirection, pixelDirection), 0.000001));

            float staticNoise = (OutlineHash(v.vertex.xyz * 29.0 + seed) - 0.5) * 2.0 * _OutlineRoughness;
            float tick = floor(_Time.y * max(1.0, _JitterFPS));
            float movingNoise = (OutlineHash(v.vertex.zyx * 47.0 + tick + seed) - 0.5) * 2.0;
            float widthPixels = max(0.0, width + staticNoise + movingNoise * _JitterStrength * _HandDrawSettled);

            clipPosition.xy += direction * widthPixels * (2.0 / _ScreenParams.xy) * clipPosition.w;
            output.pos = clipPosition;
            return output;
        }

        OutlineV2F VertSecondary(appdata_base v)
        {
            return ExpandOutline(v, _SecondaryOutlineWidth, 19.7);
        }

        OutlineV2F VertMain(appdata_base v)
        {
            return ExpandOutline(v, _OutlineWidth, 3.1);
        }

        fixed4 FragSecondary(OutlineV2F input) : SV_Target
        {
            float2 pixelCell = floor(input.pos.xy / max(0.5, _FuzzPixelScale));
            float fineBreakup = OutlineHash(float3(pixelCell, 31.7));
            float coarseBreakup = OutlineHash(float3(floor(pixelCell * 0.37), 73.1));
            clip(lerp(fineBreakup, coarseBreakup, 0.35) - _SecondaryBreakup);
            return _SecondaryOutlineColor;
        }

        fixed4 FragMain(OutlineV2F input) : SV_Target
        {
            return _OutlineColor;
        }
        ENDCG

        Pass
        {
            Name "SECONDARY_STROKE"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex VertSecondary
            #pragma fragment FragSecondary
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "MAIN_OUTLINE"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex VertMain
            #pragma fragment FragMain
            #pragma target 3.0
            ENDCG
        }

        CGPROGRAM
        #pragma surface Surf DiceToon fullforwardshadows vertex:BodyVertex
        #pragma target 3.0

        sampler2D _GrainTex;
        fixed4 _Color;
        half _GrainScale;
        half _GrainStrength;
        half _GrainTextureStrength;
        half _ShadowBand;
        half _ShadowStrength;
        half _SettledFlatness;

        struct Input
        {
            float2 uv_GrainTex;
            float3 localPosition;
        };

        void BodyVertex(inout appdata_full vertex, out Input output)
        {
            UNITY_INITIALIZE_OUTPUT(Input, output);
            output.localPosition = vertex.vertex.xyz;
        }

        float GrainHash(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.yzx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        void Surf(Input input, inout SurfaceOutput output)
        {
            float coarse = GrainHash(floor(input.localPosition * _GrainScale * 7.0));
            float fine = GrainHash(floor(input.localPosition * _GrainScale * 23.0) + 9.7);
            float proceduralGrain = lerp(coarse, fine, 0.65) - 0.5;
            float textureGrain = tex2D(_GrainTex, input.uv_GrainTex).r - 0.5;
            float grain = 1.0 + proceduralGrain * 2.0 * _GrainStrength
                + textureGrain * 2.0 * _GrainTextureStrength;

            output.Albedo = saturate(_Color.rgb * grain);
            output.Alpha = _Color.a;
            output.Gloss = 0;
            output.Specular = 0;
        }

        half4 LightingDiceToon(SurfaceOutput surface, half3 lightDirection, half attenuation)
        {
            half normalLight = saturate(dot(surface.Normal, lightDirection));
            half litBand = step(_ShadowBand, normalLight);
            half toonLight = lerp(1.0h - _ShadowStrength, 1.0h, litBand);
            toonLight = saturate(0.32h + toonLight * attenuation * 0.68h);
            toonLight = lerp(toonLight, 1.0h, _HandDrawSettled * _SettledFlatness);
            return half4(surface.Albedo * toonLight, surface.Alpha);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
