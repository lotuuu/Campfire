Shader "Garden/LivingCanvas"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.53, 0.81, 0.92, 1)
        _BottomColor ("Bottom Color", Color) = (0.94, 0.97, 1.0, 1)

        [Header(Mist)]
        _MistOpacity ("Mist Opacity", Range(0, 0.3)) = 0.08
        _MistSpeed ("Mist Speed", Range(0, 1)) = 0.15
        _MistScale ("Mist Scale", Range(1, 20)) = 6

        [Header(Particles)]
        _ParticleSpeed ("Particle Rise Speed", Range(0, 2)) = 0.3
        _ParticleDrift ("Particle Horizontal Drift", Range(0, 1)) = 0.1
        _ParticleSize ("Particle Size", Range(0.003, 0.05)) = 0.012
        _ParticleOpacity ("Particle Opacity", Range(0, 0.5)) = 0.12

        [Header(Vignette)]
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.35
        _VignetteRadius ("Vignette Softness", Range(0.2, 1.0)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off

        Pass
        {
            Name "LivingCanvas"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half _MistOpacity;
                half _MistSpeed;
                half _MistScale;
                half _ParticleSpeed;
                half _ParticleDrift;
                half _ParticleSize;
                half _ParticleOpacity;
                half _VignetteStrength;
                half _VignetteRadius;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // ---- Noise (mobile-friendly hash-based) ----

            half hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            half valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                half a = hash21(i);
                half b = hash21(i + float2(1.0, 0.0));
                half c = hash21(i + float2(0.0, 1.0));
                half d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Two-octave FBM - good enough for mist, cheap on mobile
            half fbm2(float2 p)
            {
                half v = 0.5 * valueNoise(p);
                v += 0.25 * valueNoise(p * 2.03 + float2(17.3, 31.7));
                return v;
            }

            // ---- Vertex ----

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            // ---- Fragment ----

            // Fixed particle count - keeps the GPU loop predictable on mobile
            #define PARTICLE_COUNT 12

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;

                // --- 1. Vertical gradient ---
                half3 col = lerp(_BottomColor.rgb, _TopColor.rgb, uv.y);

                // --- 2. Atmospheric mist (Layer 1) ---
                float2 mistUV = uv * _MistScale;
                mistUV += float2(t * _MistSpeed * 0.3, t * _MistSpeed * 0.08);
                half mist = fbm2(mistUV);
                col += (mist - 0.375) * _MistOpacity;

                // --- 3. Drifting particles (Layer 2) ---
                half particles = 0.0;

                UNITY_UNROLL
                for (int i = 0; i < PARTICLE_COUNT; i++)
                {
                    half seed = (half)i * 7.239;
                    half rX = hash21(float2(seed, 0.0));
                    half rY = hash21(float2(seed, 1.0));
                    half rSpd = hash21(float2(seed, 2.0));
                    half rDrf = hash21(float2(seed, 3.0));
                    half rSz = hash21(float2(seed, 4.0));
                    half rBr = hash21(float2(seed, 5.0));

                    // Particle position: wraps via frac for seamless looping
                    float2 pPos;
                    pPos.x = frac(rX + t * _ParticleDrift * (rDrf - 0.5) * 0.1);
                    pPos.y = frac(rY + t * _ParticleSpeed * rSpd * 0.08);

                    half dist = length(uv - pPos);
                    half size = _ParticleSize * (0.5 + rSz);

                    // Soft-edged circle via smoothstep
                    particles += smoothstep(size, size * 0.15, dist) * (0.4 + 0.6 * rBr);
                }

                col += particles * _ParticleOpacity;

                // --- 4. Vignette ---
                float2 vc = uv - 0.5;
                half vDist = length(vc);
                half vignette = smoothstep(_VignetteRadius + 0.2, _VignetteRadius - 0.3, vDist);
                col *= lerp(1.0, vignette, _VignetteStrength);

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
