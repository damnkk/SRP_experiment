#define PI 3.14159265359

// D
float Trowbridge_Reitz_GGX(float NdotH, float a)
{
    float a2     = a * a;
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / denom;
}

// F
float3 SchlickFresnel(float HdotV, float3 F0)
{
    float m = clamp(1-HdotV, 0, 1);
    float m2 = m * m;
    float m5 = m2 * m2 * m; // pow(m,5)
    return F0 + (1.0 - F0) * m5;
}

// G
float SchlickGGX(float NdotV, float k)
{
    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

// Unity Use this as IBL F
float3 FresnelSchlickRoughness(float NdotV, float3 f0, float roughness)
{
    float r1 = 1.0f - roughness;
    return f0 + (max(float3(r1, r1, r1), f0) - f0) * pow(1 - NdotV, 5.0f);
}

// 直接光照
float3 PBR(float3 N, float3 V, float3 L, float3 albedo, float3 radiance, float roughness, float metallic)
{
    roughness = max(roughness, 0.05);   // 保证光滑物体也有高光

    float3 H = normalize(L+V);
    float NdotL = max(dot(N, L), 0);
    float NdotV = max(dot(N, V), 0);
    float NdotH = max(dot(N, H), 0);
    float HdotV = max(dot(H, V), 0);
    float alpha = roughness * roughness;
    float k = ((alpha+1) * (alpha+1)) / 8.0;
    float3 F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);

    float  D = Trowbridge_Reitz_GGX(NdotH, alpha);
    float3 F = SchlickFresnel(HdotV, F0);
    float  G = SchlickGGX(NdotV, k) * SchlickGGX(NdotL, k);

    float3 k_s = F;
    float3 k_d = (1.0 - k_s) * (1.0 - metallic);
    float3 f_diffuse = albedo / PI;
    float3 f_specular = (D * F * G) / (4.0 * NdotV * NdotL + 0.0001);

    // Unity Albedo 没乘 PI, 为保持 d 和 s 的比例, Specular 也乘以 PI
    f_diffuse *= PI;
    f_specular *= PI;

    float3 color = (k_d * f_diffuse + f_specular) * radiance * NdotL;

    return color;
}

float3 FrasnelSchlickRoughness(float NdotV,float3 f0,float roughness)
{
    float r1 =  1.0f-roughness;
    return f0+(max(float3(r1,r1,r1),f0)-f0)*pow(1-NdotV,5.0f);
}

float3 IBL(
    float3 N,float3 V,
    float3 albedo,float roughness,float metallic,
    samplerCUBE _diffuseIBL,samplerCUBE _specularIBL,sampler2D _brdfLut)
    {
        roughness = min(roughness,0.99);
        float3 H = normalize(N);
        float NdotV = max(dot(N,V),0);
        float HdotV = max(dot(H,V),0);
        float3 R = normalize(reflect(-V,N));//反射向量

        float3 F0 = lerp(float3(0.04,0.04,0.04),albedo,metallic);
        float3 F = FrasnelSchlickRoughness(HdotV,F0,roughness);
        float3 k_s = F;
        float3 k_d = (1.0-k_s)*(1.0-metallic);

        //漫反射
        float3 IBLd = texCUBE(_diffuseIBL,N).rgb;//根据法向量去漫反射环境光贴图当中采样即可获得漫反射强度
        float3 diffuse = k_d*albedo*IBLd;//

        //镜面反射
        float rgh = roughness*(1.7-0.7*roughness);//1.7r或者1.0r
        float lod = 6.0*rgh;//unity默认6级mipmap
        float3 IBLs = texCUBElod(_specularIBL,float4(R,lod)).rgb;//这个函数非常聪明,第二个参数是四维向量
        //前面三维进行采样,第四维代表mipmap的层级,且可以为浮点数,进行三线性插值。
        float2 brdf = tex2D(_brdfLut,float2(NdotV,roughness)).rg;
        float3 specular = IBLs*(F0*brdf.x+brdf.y);

        float3 ambient = diffuse+specular;
        return ambient;

    }


