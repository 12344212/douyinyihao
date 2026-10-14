Shader "Battle/WaterWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WaveAmp ("Wave Amp", Range(0, 0.05)) = 0.012
        _WaveFreq ("Wave Freq", Float) = 5.5
        _WaveSpeed ("Wave Speed", Float) = 1.1
        _WaveAmp2 ("Wave Amp 2", Range(0, 0.05)) = 0.007
        _WaveFreq2 ("Wave Freq 2", Float) = 3.2
        _WaveSpeed2 ("Wave Speed 2", Float) = 0.75
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _WaveAmp;
            float _WaveFreq;
            float _WaveSpeed;
            float _WaveAmp2;
            float _WaveFreq2;
            float _WaveSpeed2;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float t = _Time.y;

                // Dual sine UV warp. Mesh bounds unchanged — camera coverage stays full.
                // BattleSpriteWater uses Clamp wrap, so out-of-range UVs stay water-colored.
                float2 warp;
                warp.x = sin((uv.y + t * _WaveSpeed) * _WaveFreq) * _WaveAmp
                       + sin((uv.y * 1.37 + t * _WaveSpeed2) * _WaveFreq2) * _WaveAmp2;
                warp.y = cos((uv.x + t * _WaveSpeed * 0.85) * _WaveFreq * 0.9) * _WaveAmp * 0.85
                       + cos((uv.x * 1.21 - t * _WaveSpeed2) * _WaveFreq2) * _WaveAmp2;
                uv += warp;

                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}