Shader "DiceWitch/Battle Dice Face Hand Drawn"
{
    Properties
    {
        _MainTex ("Face Texture", 2D) = "white" {}
        _FaceScale ("Face Scale", Range(0.5, 0.95)) = 0.95

        _GrainTex ("Optional Paper / Crayon Grain", 2D) = "gray" {}
        _GrainScale ("New Crayon Stroke Scale", Range(1, 40)) = 9
        _GrainStrength ("New Crayon Stroke Strength", Range(0, 0.35)) = 0.19
        _GrainTextureStrength ("Texture Grain", Range(0, 0.35)) = 0
        _SimplifyRadius ("Discard Source Texture", Range(1, 24)) = 10
        _ShapePreservation ("Preserve Color Shapes", Range(1, 40)) = 6
        _ColorSteps ("Retained Light Steps", Range(2, 8)) = 5
        _Saturation ("Retained Color Saturation", Range(0, 1.5)) = 0.9
        _InkColor ("Detected Edge Ink", Color) = (0.025,0.018,0.03,1)
        _InkStrength ("Shape Ink Strength", Range(0, 1)) = 0.58
        _InkThreshold ("Shape Edge Threshold", Range(0.01, 0.5)) = 0.11

        _ShadowBand ("Toon Shadow Threshold", Range(0, 1)) = 0.48
        _ShadowStrength ("Rolling Shadow Strength", Range(0, 0.7)) = 0.2
        _SettledFlatness ("Settled Face Flatness", Range(0, 1)) = 0.95
        [HideInInspector] _HandDrawSettled ("Settled", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest+1" "RenderType"="TransparentCutout" }
        LOD 220

        CGPROGRAM
        #pragma surface Surf DiceFaceToon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _GrainTex;
        half _FaceScale;
        half _GrainScale;
        half _GrainStrength;
        half _GrainTextureStrength;
        half _SimplifyRadius;
        half _ShapePreservation;
        half _ColorSteps;
        half _Saturation;
        fixed4 _InkColor;
        half _InkStrength;
        half _InkThreshold;
        half _ShadowBand;
        half _ShadowStrength;
        half _SettledFlatness;
        half _HandDrawSettled;

        struct Input
        {
            float2 uv_MainTex;
        };

        float GrainHash(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        void AddShapeSample(fixed3 sampleColor, fixed3 referenceColor, float sampleWeight,
            inout fixed3 colorSum, inout float weightSum)
        {
            float3 difference = sampleColor - referenceColor;
            float similarity = saturate(1.0 - dot(difference, difference) * _ShapePreservation);
            float weight = similarity * similarity * sampleWeight;
            colorSum += sampleColor * weight;
            weightSum += weight;
        }

        void Surf(Input input, inout SurfaceOutput output)
        {
            float2 centered = input.uv_MainTex - 0.5;
            float maxDistance = max(abs(centered.x), abs(centered.y));
            clip(_FaceScale * 0.5 - maxDistance);

            float2 faceUv = centered / _FaceScale + 0.5;
            fixed4 centerSample = tex2D(_MainTex, faceUv);
            clip(centerSample.a - 0.02);

            // Throw away source grain, paper and gradients before rebuilding our own surface.
            float2 radius = _MainTex_TexelSize.xy * _SimplifyRadius;
            fixed4 left = tex2D(_MainTex, saturate(faceUv - float2(radius.x, 0)));
            fixed4 right = tex2D(_MainTex, saturate(faceUv + float2(radius.x, 0)));
            fixed4 down = tex2D(_MainTex, saturate(faceUv - float2(0, radius.y)));
            fixed4 up = tex2D(_MainTex, saturate(faceUv + float2(0, radius.y)));
            fixed4 downLeft = tex2D(_MainTex, saturate(faceUv - radius));
            fixed4 upRight = tex2D(_MainTex, saturate(faceUv + radius));
            fixed4 upLeft = tex2D(_MainTex, saturate(faceUv + float2(-radius.x, radius.y)));
            fixed4 downRight = tex2D(_MainTex, saturate(faceUv + float2(radius.x, -radius.y)));
            fixed4 farLeft = tex2D(_MainTex, saturate(faceUv - float2(radius.x * 2.0, 0)));
            fixed4 farRight = tex2D(_MainTex, saturate(faceUv + float2(radius.x * 2.0, 0)));
            fixed4 farDown = tex2D(_MainTex, saturate(faceUv - float2(0, radius.y * 2.0)));
            fixed4 farUp = tex2D(_MainTex, saturate(faceUv + float2(0, radius.y * 2.0)));

            fixed3 colorSum = centerSample.rgb * 2.0;
            float weightSum = 2.0;
            AddShapeSample(left.rgb, centerSample.rgb, 1.5, colorSum, weightSum);
            AddShapeSample(right.rgb, centerSample.rgb, 1.5, colorSum, weightSum);
            AddShapeSample(down.rgb, centerSample.rgb, 1.5, colorSum, weightSum);
            AddShapeSample(up.rgb, centerSample.rgb, 1.5, colorSum, weightSum);
            AddShapeSample(downLeft.rgb, centerSample.rgb, 1.0, colorSum, weightSum);
            AddShapeSample(upRight.rgb, centerSample.rgb, 1.0, colorSum, weightSum);
            AddShapeSample(upLeft.rgb, centerSample.rgb, 1.0, colorSum, weightSum);
            AddShapeSample(downRight.rgb, centerSample.rgb, 1.0, colorSum, weightSum);
            AddShapeSample(farLeft.rgb, centerSample.rgb, 0.75, colorSum, weightSum);
            AddShapeSample(farRight.rgb, centerSample.rgb, 0.75, colorSum, weightSum);
            AddShapeSample(farDown.rgb, centerSample.rgb, 0.75, colorSum, weightSum);
            AddShapeSample(farUp.rgb, centerSample.rgb, 0.75, colorSum, weightSum);
            fixed3 simplified = colorSum / max(0.001, weightSum);

            float luminance = dot(simplified, float3(0.299, 0.587, 0.114));
            simplified = lerp(luminance.xxx, simplified, _Saturation);
            float lightSteps = max(2.0, _ColorSteps) - 1.0;
            float steppedLuminance = floor(luminance * lightSteps + 0.5) / lightSteps;
            fixed3 artwork = saturate(simplified
                * clamp((steppedLuminance + 0.025) / (luminance + 0.025), 0.68, 1.32));

            float3 horizontalDifference = abs(right.rgb - left.rgb);
            float3 verticalDifference = abs(up.rgb - down.rgb);
            float detectedEdge = max(
                max(horizontalDifference.r, max(horizontalDifference.g, horizontalDifference.b)),
                max(verticalDifference.r, max(verticalDifference.g, verticalDifference.b)));
            float ink = smoothstep(_InkThreshold, _InkThreshold + 0.2, detectedEdge) * _InkStrength;
            artwork = lerp(artwork, _InkColor.rgb, ink);

            float coarse = GrainHash(floor(faceUv * _GrainScale * 2.0));
            float fine = GrainHash(floor(faceUv * _GrainScale * 7.0) + 7.0);
            float diagonalStroke = GrainHash(floor(float2(
                (faceUv.x + faceUv.y * 0.38) * _GrainScale,
                faceUv.y * _GrainScale * 0.3)) + 19.0);
            float proceduralGrain = lerp(coarse, fine, 0.55) + diagonalStroke * 0.65 - 0.825;
            float textureGrain = tex2D(_GrainTex, faceUv * _GrainScale * 0.1).r - 0.5;
            float grain = 1.0 + proceduralGrain * 2.0 * _GrainStrength
                + textureGrain * 2.0 * _GrainTextureStrength;

            output.Albedo = saturate(artwork * grain);
            output.Alpha = 1;
            output.Gloss = 0;
            output.Specular = 0;
        }

        half4 LightingDiceFaceToon(SurfaceOutput surface, half3 lightDirection, half attenuation)
        {
            half normalLight = saturate(dot(surface.Normal, lightDirection));
            half litBand = step(_ShadowBand, normalLight);
            half toonLight = lerp(1.0h - _ShadowStrength, 1.0h, litBand);
            toonLight = saturate(0.45h + toonLight * attenuation * 0.55h);
            toonLight = lerp(toonLight, 1.0h, _HandDrawSettled * _SettledFlatness);
            return half4(surface.Albedo * toonLight, surface.Alpha);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
