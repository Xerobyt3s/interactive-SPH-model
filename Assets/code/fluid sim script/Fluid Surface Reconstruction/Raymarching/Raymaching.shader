Shader "Fluid/Raymaching"
{
    Properties
    {
        _DensityTexture ("Density Texture", 3D) = "" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct meshData
            {
                float4 position : POSITION;  // Object-space vertex position
                float2 uv : TEXCOORD0;       // UV coordinates
            };

            struct interpolators
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler3D _DensityTexture;  // The SPH fluid density texture

            // Vertex Shader: Passes data to the fragment shader
            interpolators vert(meshData v)
            {
                interpolators o;
                o.vertex = UnityObjectToClipPos(v.position); // Convert object-space to clip-space
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.position).xyz; // Convert to world space
                return o;
            }

            // Helper function to sample the density field
            float SampleDensity(float3 pos)
            {
                return tex3D(_DensityTexture, pos).r; // Sample red channel for density
            }

            // Raymarching function to find the surface of the fluid
            float RaymarchFluid(float3 ro, float3 rd, out float totalDepth)
            {
                totalDepth = 0.0;
                const int MAX_STEPS = 100;
                const float STEP_SIZE = 0.02;
                const float DENSITY_THRESHOLD = 0.5;

                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = ro + rd * totalDepth;
                    float density = SampleDensity(p);

                    if (density > DENSITY_THRESHOLD)
                    {
                        return totalDepth; // Hit fluid surface
                    }

                    totalDepth += STEP_SIZE;
                    if (totalDepth > 10.0) break;
                }
                return -1.0; // No hit
            }

            // Estimate normal for shading
            float3 GetNormal(float3 p)
            {
                float d = SampleDensity(p);
                float3 n = float3(
                    SampleDensity(p + float3(0.01, 0, 0)) - d,
                    SampleDensity(p + float3(0, 0.01, 0)) - d,
                    SampleDensity(p + float3(0, 0, 0.01)) - d
                );
                return normalize(n);
            }

            // Fragment Shader: Handles color absorption & refraction
            fixed4 frag(interpolators i) : SV_Target
            {
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.worldPos - _WorldSpaceCameraPos);

                float totalDepth;
                float hitDepth = RaymarchFluid(rayOrigin, rayDir, totalDepth);

                if (hitDepth > 0.0)
                {
                    // Absorb colors based on depth (Beer’s Law)
                    float3 absorption = exp(-totalDepth * float3(2.0, 5.0, 8.0)); // Water absorption
                    float3 baseColor = float3(0.0, 0.5, 1.0); // Blue color for water
                    float3 refractedColor = baseColor * absorption;

                    // Compute normal & refraction
                    float3 hitPos = rayOrigin + rayDir * hitDepth;
                    float3 normal = GetNormal(hitPos);
                    float3 refractedDir = refract(rayDir, normal, 1.0 / 1.33); // Water IOR = 1.33

                    return fixed4(refractedColor, 1.0);
                }
                return fixed4(0, 0, 0, 1); // Background
            }

            ENDCG
        }
    }
}
