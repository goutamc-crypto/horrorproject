// Mobile night sky: no textures, one pass. The horizon colour equals the fog colour, so terrain dissolves into the sky.
Shader "Kuldhara/NightSkyMobile"
{
    Properties
    {
        _Horizon ("Horizon (match fog)", Color) = (0.045, 0.06, 0.09, 1)
        _Zenith ("Zenith", Color) = (0.008, 0.012, 0.028, 1)
        _MoonColor ("Moon", Color) = (0.86, 0.9, 1, 1)
        _MoonDir ("Moon Direction", Vector) = (0, 0.5, 0.8, 0)
        _MoonSize ("Moon Size", Range(0.005, 0.1)) = 0.0085
        _HaloColor ("Halo", Color) = (0.22, 0.3, 0.45, 1)
        _CloudColor ("Cloud", Color) = (0.02, 0.025, 0.04, 1)
        _CloudAmount ("Cloud Amount", Range(0, 1)) = 0.55
        _CloudSpeed ("Cloud Speed", Float) = 0.006
        _StarAmount ("Stars", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Horizon, _Zenith, _MoonColor, _HaloColor, _CloudColor;
            float4 _MoonDir;
            float _MoonSize, _CloudAmount, _CloudSpeed, _StarAmount;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float hash3(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = saturate(d.y);
                half3 col = lerp(_Horizon.rgb, _Zenith.rgb, pow(h, 0.5));

                // Stars live on the sphere, not the cloud plane: tiny round points that never stretch.
                float3 sp = d * 110.0;
                float rnd = hash3(floor(sp));
                float dotShape = 1.0 - smoothstep(0.05, 0.22, length(frac(sp) - 0.5));
                col += step(0.985, rnd) * dotShape * _StarAmount * saturate(h * 4.0) * (rnd - 0.9) * 10.0;

                float moon = dot(d, normalize(_MoonDir.xyz));
                col += _HaloColor.rgb * pow(saturate(moon), 90.0) * 0.6;

                // Sky-plane projection; the +0.3 stops clouds stretching to infinity at the horizon.
                float2 c = d.xz / (h + 0.3) * 4.5 + _Time.y * _CloudSpeed;
                float n = noise(c) * 0.6 + noise(c * 2.7 + 3.1) * 0.3 + noise(c * 6.1 + 7.7) * 0.1;
                float clouds = smoothstep(1.0 - _CloudAmount, 1.0 - _CloudAmount + 0.45, n) * saturate(h * 6.0);
                col = lerp(col, _CloudColor.rgb, clouds * 0.75);

                float m = _MoonSize * _MoonSize;
                col = lerp(col, _MoonColor.rgb, smoothstep(1.0 - m, 1.0 - m * 0.75, moon) * (1.0 - clouds * 0.5));

                // Without HDR a dark gradient bands in 8 bits; per-pixel noise hides the steps (scaled for linear space).
                float ign = frac(52.9829189 * frac(dot(i.pos.xy, float2(0.06711056, 0.00583715))));
                col += (ign - 0.5) * 4.0 / 255.0 * sqrt(col + 0.0005);

                return half4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
