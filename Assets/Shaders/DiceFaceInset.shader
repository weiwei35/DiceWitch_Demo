Shader "DiceWitch/Dice Face Inset"
{
    Properties
    {
        _MainTex ("Face Texture", 2D) = "white" {}
        _FaceScale ("Face Scale", Range(0.5, 1)) = 0.84
    }

    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        half _FaceScale;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float2 distanceFromCenter = abs(input.uv_MainTex - 0.5);
            clip(_FaceScale * 0.5 - max(distanceFromCenter.x, distanceFromCenter.y));

            float2 faceUv = (input.uv_MainTex - 0.5) / _FaceScale + 0.5;
            fixed4 color = tex2D(_MainTex, faceUv);
            output.Albedo = color.rgb;
            output.Alpha = 1;
            output.Smoothness = 0.05;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
