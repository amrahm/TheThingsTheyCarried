Shader "Sprites/Diffuse with Shadows" {
    Properties {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
        _Cutoff("Shadow Alpha Cutoff", Range(0,1)) = 0.5
        [Toggle(SOFT_EDGE_ON)] _SoftEdge("Enable SoftEdge Pass", Float) = 1
    }

    SubShader { //Soft edge pass
        Tags{ "Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        LOD 300
        Lighting On
        ZWrite Off
        Fog{ Mode Off }
        Cull Back


        CGPROGRAM
            #pragma surface surf Lambert vertex:vert alpha
            #pragma shader_feature SOFT_EDGE_ON

            sampler2D _MainTex;
            fixed4 _Color;

            struct Input
            {
                float2 uv_MainTex;
                fixed4 color;
            };

#if defined(SOFT_EDGE_ON)
            void vert(inout appdata_full v, out Input o) {
#if defined(PIXELSNAP_ON) && !defined(SHADER_API_FLASH)
                v.vertex = UnityPixelSnap(v.vertex);
#endif
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.color = _Color;
            }

            void surf(Input IN, inout SurfaceOutput o) {
                fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
                o.Albedo = c.rgb;
                o.Alpha = c.a;
            }
#else
            void vert(inout appdata_full v, out Input o) { UNITY_INITIALIZE_OUTPUT(Input, o); }
            void surf(Input IN, inout SurfaceOutput o) { }
#endif
        ENDCG

        Cull Off

        CGPROGRAM
            #pragma surface surf Lambert vertex:vert addshadow fullforwardshadows alphatest:_Cutoff
            #pragma multi_compile DUMMY PIXELSNAP_ON 

            sampler2D _MainTex;
            fixed4 _Color;

            struct Input
            {
                float2 uv_MainTex;
                fixed4 color;
            };

            void vert(inout appdata_full v, out Input o) {
#if defined(PIXELSNAP_ON) && !defined(SHADER_API_FLASH)
                v.vertex = UnityPixelSnap(v.vertex);
#endif
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.color = _Color;
            }

            void surf(Input IN, inout SurfaceOutput o) {
                fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * IN.color;
                o.Albedo = c.rgb;
                o.Alpha = c.a;
            }
        ENDCG
    }

    Fallback "Sprites/Diffuse"
}
