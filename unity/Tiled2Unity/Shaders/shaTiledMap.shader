Shader "Tiled/TextureTintSnap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Tiled Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AlphaColorKey ("Alpha Color Key", Color) = (0,0,0,0)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 1
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Fog { Mode Off }
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile DUMMY PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                half2 texcoord  : TEXCOORD0;
            };


            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = mul(UNITY_MATRIX_MVP, IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                // Supports animations through z-component of tile
                if (IN.vertex.z < 0)
                {
                    // "Hide" frames of a tile animation that are not active
                    OUT.vertex.w = 0;
                }
                else
                {
                    OUT.vertex.z = 0;
                }

                return OUT;
            }

            sampler2D _MainTex;
            float4 _AlphaColorKey;

            fixed4 frag(v2f IN) : COLOR
            {
                half4 texcol = tex2D(_MainTex, IN.texcoord);

                // The alpha color key is 'enabled' if it has solid alpha
                if (_AlphaColorKey.a == 1 &&
                    _AlphaColorKey.r == texcol.r &&
                    _AlphaColorKey.g == texcol.g &&
                    _AlphaColorKey.b == texcol.b)
                {
                    texcol.a = 0;
                }
                else
                {
                    texcol = texcol * IN.color;
                }

                return texcol;
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}