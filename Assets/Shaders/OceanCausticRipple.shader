Shader "Battle/OceanCausticRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WaveAmp ("Wave Amp", Range(0, 0.05)) = 0.012
        _WaveFreq ("Wave Freq", Float) = 4.2
        _WaveSpeed ("Wave Speed", Float) = 0.24
        _WaveAmp2 ("Wave Amp 2", Range(0, 0.05)) = 0.007
        _WaveFreq2 ("Wave Freq 2", Float) = 2.6
        _WaveSpeed2 ("Wave Speed 2", Float) = 0.4
        _FlowSpeed ("Flow Speed", Float) = 1.25
        _Phase ("Phase", Float) = 0
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
            float _FlowSpeed;
            float _Phase;

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
                float phase = _Phase;

                // 沿右上到左下来回，正弦到头就退回。位移有上限，不会把接缝卷进画面。
                float2 flowDir = normalize(float2(1.0, 1.0));
                float2 across = float2(-flowDir.y, flowDir.x);
                float travel = sin(t * _FlowSpeed + phase) * _WaveAmp;
                float side = dot(uv, across);
                float wobble = sin(side * _WaveFreq + t * _WaveSpeed) * _WaveAmp2;
                float2 sampleUv = uv + flowDir * travel + across * wobble;

                fixed4 c = tex2D(_MainTex, sampleUv) * IN.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
