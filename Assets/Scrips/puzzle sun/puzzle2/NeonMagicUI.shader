// Shader สำหรับ UI Image: เปลี่ยน "เส้นดำ" ของวงเวทย์ให้กลายเป็นแสงนีออนเรืองๆ
// ใช้ผ่านสคริปต์ MagicCirclePuzzle (ลากไฟล์นี้ใส่ช่อง Neon Shader) ไม่ต้องสร้าง Material เอง
// _Neon = 0 -> แสดงภาพปกติ, _Neon = 1 -> เป็นแสงนีออนเต็มที่
Shader "UI/NeonMagic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Neon ("Neon Amount", Range(0,1)) = 0
        _ColA ("Neon A", Color) = (0.1,1,1,1)
        _ColB ("Neon B", Color) = (0.65,0.3,1,1)
        _ColC ("Neon C", Color) = (1,0.25,0.85,1)
        _Intensity ("Core Intensity", Float) = 1.6
        _GlowStrength ("Glow Strength", Float) = 1.3
        _GlowRadius ("Glow Radius (texture px)", Float) = 6
        _Texel ("Texel (1/w, 1/h)", Vector) = (0.001,0.001,0,0)
        _UTime ("Unscaled Time", Float) = 0
        _Pulse ("Pulse", Range(0,1)) = 0.25
        _AlphaOnly ("Use Alpha Only As Mask", Float) = 0
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
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha   // premultiplied: ค่า rgb สูงกว่า alpha = ให้ผลคล้าย additive (เรืองแสง)

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 lpos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _Neon;
            float4 _ColA;
            float4 _ColB;
            float4 _ColC;
            float _Intensity;
            float _GlowStrength;
            float _GlowRadius;
            float4 _Texel;
            float _UTime;
            float _Pulse;
            float _AlphaOnly;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.lpos = v.vertex.xy; // ตำแหน่ง local ใช้คำนวณมุมรอบศูนย์กลางเพื่อไล่สีนีออน
                return o;
            }

            // เส้นดำ (มืด + ทึบ) = 1, พื้นโปร่ง/พื้นขาว = 0
            float Mask(float2 uv)
            {
                float4 c = tex2D(_MainTex, uv);
                float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                return lerp(c.a * (1.0 - lum), c.a, _AlphaOnly);
            }

            // เฉลี่ยรอบวง 8 ทิศ ที่ระยะ px
            float Ring8(float2 uv, float2 px)
            {
                float s = 0;
                s += Mask(uv + float2( 1.0,     0.0)    * px);
                s += Mask(uv + float2( 0.7071,  0.7071) * px);
                s += Mask(uv + float2( 0.0,     1.0)    * px);
                s += Mask(uv + float2(-0.7071,  0.7071) * px);
                s += Mask(uv + float2(-1.0,     0.0)    * px);
                s += Mask(uv + float2(-0.7071, -0.7071) * px);
                s += Mask(uv + float2( 0.0,    -1.0)    * px);
                s += Mask(uv + float2( 0.7071, -0.7071) * px);
                return s * 0.125;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
                float origA = tex.a * i.color.a;
                float3 origRGB = tex.rgb * i.color.rgb * origA;

                if (_Neon <= 0.001)
                    return half4(origRGB, origA);

                float core = Mask(i.uv);

                // เรืองแสงรอบเส้น (blur หลายระยะ)
                float2 px = _Texel.xy * _GlowRadius;
                float halo = Ring8(i.uv, px * 0.6) * 0.45
                           + Ring8(i.uv, px * 1.4) * 0.35
                           + Ring8(i.uv, px * 2.6) * 0.20;

                // สีนีออนไล่ตามมุมรอบวง และเลื่อนไหลตามเวลา
                float ang = atan2(i.lpos.y, i.lpos.x);
                float w1 = 0.5 + 0.5 * sin(ang * 2.0 + _UTime * 1.4);
                float w2 = 0.5 + 0.5 * sin(ang * 3.0 - _UTime * 1.1 + 2.0);
                float3 neon = lerp(lerp(_ColA.rgb, _ColB.rgb, w1), _ColC.rgb, w2 * 0.55);

                float pulse = 1.0 + _Pulse * sin(_UTime * 3.2);
                float3 hot = lerp(neon, float3(1, 1, 1), 0.55); // แกนเส้นขาวร้อน ขอบเป็นสีนีออน

                float3 nRGB = (hot * core * _Intensity + neon * halo * _GlowStrength) * pulse;
                float nA = saturate(core * 0.9 + halo * _GlowStrength * 0.45);
                nRGB *= i.color.a;
                nA *= i.color.a;

                return half4(lerp(origRGB, nRGB, _Neon), lerp(origA, nA, _Neon));
            }
            ENDCG
        }
    }
}
