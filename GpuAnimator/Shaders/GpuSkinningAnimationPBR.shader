Shader "Custom/GpuSkinningAnimationPBR"
{
    Properties
    {

        _MainTex ("Albedo (RGB)", 2D) = "white" { }
        _Mask ("Mask (RGB)", 2D) = "white" { }
        _Color ("Color", Color) = (1, 1, 1, 1)

        _Diffuse ("Diffuse", Color) = (1, 1, 1, 1)

        [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0 //������Ҫ����٤��У��
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _LUT ("LUT", 2D) = "white" { }

        //��������
        _AnimationTex ("Animation Texture", 2D) = "white" { }
        _AnimationTexSize ("Animation Texture Size", Vector) = (0, 0, 0, 0)

        _BoneNum ("Bone Num", Int) = 0
        _FrameIndex ("Frame Index", Range(1, 1000)) = 1
        _BlendFrameIndex ("Blend Frame Index", Range(1, 1000)) = 1
        _BlendProgress ("Blend Progress", Range(0.0, 1.0)) = 0.0
        
        _IsPlayer ("IsPlayer", Int) = 1
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200
        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityPBSLighting.cginc"

            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            sampler2D _Mask;
            float4 _Mask_ST;
            fixed4 _Color;
            fixed4 _Diffuse;

            sampler2D _NormalTex;
            float4 _NormalMap_TexelSize;
            float _BumpScale;

            float4 _Tint;
            float _Metallic;
            float _Smoothness;
            sampler2D _LUT;

            //  ��������
            int _BoneNum;
            sampler2D _AnimationTex;
            float4 _AnimationTex_ST;
            float4 _AnimationTexSize;
            
            int _IsPlayer;

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(int, _FrameIndex)
            UNITY_DEFINE_INSTANCED_PROP(int, _BlendFrameIndex)
            UNITY_DEFINE_INSTANCED_PROP(float, _BlendProgress)
            UNITY_INSTANCING_BUFFER_END(Props)

            float4x4 QuaternionToMatrix(float4 vec)
            {
                float4x4 ret;
                ret._11 = 2.0 * (vec.x * vec.x + vec.w * vec.w) - 1;
                ret._21 = 2.0 * (vec.x * vec.y + vec.z * vec.w);
                ret._31 = 2.0 * (vec.x * vec.z - vec.y * vec.w);
                ret._41 = 0.0;
                ret._12 = 2.0 * (vec.x * vec.y - vec.z * vec.w);
                ret._22 = 2.0 * (vec.y * vec.y + vec.w * vec.w) - 1;
                ret._32 = 2.0 * (vec.y * vec.z + vec.x * vec.w);
                ret._42 = 0.0;
                ret._13 = 2.0 * (vec.x * vec.z + vec.y * vec.w);
                ret._23 = 2.0 * (vec.y * vec.z - vec.x * vec.w);
                ret._33 = 2.0 * (vec.z * vec.z + vec.w * vec.w) - 1;
                ret._43 = 0.0;
                ret._14 = 0.0;
                ret._24 = 0.0;
                ret._34 = 0.0;
                ret._44 = 1.0;
                return ret;
            }

            float4x4 DualQuaternionToMatrix(float4 m_dual, float4 m_real)
            {
                float4x4 rotationMatrix = QuaternionToMatrix(float4(m_dual.x, m_dual.y, m_dual.z, m_dual.w));
                float4x4 translationMatrix;
                translationMatrix._11_12_13_14 = float4(1, 0, 0, 0);
                translationMatrix._21_22_23_24 = float4(0, 1, 0, 0);
                translationMatrix._31_32_33_34 = float4(0, 0, 1, 0);
                translationMatrix._41_42_43_44 = float4(0, 0, 0, 1);
                translationMatrix._14 = m_real.x;
                translationMatrix._24 = m_real.y;
                translationMatrix._34 = m_real.z;
                float4x4 scaleMatrix;
                scaleMatrix._11_12_13_14 = float4(1, 0, 0, 0);
                scaleMatrix._21_22_23_24 = float4(0, 1, 0, 0);
                scaleMatrix._31_32_33_34 = float4(0, 0, 1, 0);
                scaleMatrix._41_42_43_44 = float4(0, 0, 0, 1);
                scaleMatrix._11 = m_real.w;
                scaleMatrix._22 = m_real.w;
                scaleMatrix._33 = m_real.w;
                scaleMatrix._44 = 1;
                float4x4 M = mul(translationMatrix, mul(rotationMatrix, scaleMatrix));

                return M;
            }

            float ConvertColorToFloat32(float4 color)
            {
                float integers = color.r * 255;
                int integer = (int)integers;

                int flag = (integer / 128);//����  <=128 flagΪ0 //1: ��  0:��
                
                integer = integer - flag * 128; // ��������
                float floats0 = color.g;
                float floats1 = color.b / 255.0;
                float floats2 = color.a / 65025.0;
                
                float result = integer + floats0 + floats1 + floats2;
                result = result - 2 * flag * result;		//1: ��  0:��

                return result;
            }

            float4 ConvertColors2Halfs(float4 color1, float4 color2, float4 color3, float4 color4)
            {
                return float4(
                    ConvertColorToFloat32(color1),
                    ConvertColorToFloat32(color2),
                    ConvertColorToFloat32(color3),
                    ConvertColorToFloat32(color4)
                );
            }



            float4 indexToUV(float index)
            {
                int iIndex = trunc(index + 0.5);
                int row = (int) (iIndex / _AnimationTexSize.x);
                float col = iIndex - row * _AnimationTexSize.x;
                return float4((col + 0.5) / _AnimationTexSize.x, (row + 0.5) / _AnimationTexSize.y, 0, 0);
            }
            

            struct appdata
            {
                float4 vertex: POSITION;
                float3 normal: NORMAL;
                float2 uv: TEXCOORD0;
                //float4 color : COLOR;

                float4 boneIndices: TEXCOORD1;
                float4 boneWeights: TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;
                float2 uv: TEXCOORD0;
                float3 normal: TEXCOORD1;
                float3 worldPos: TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                //UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 boneIndices = v.boneIndices;
                float4 boneWeights = v.boneWeights;
                float4 boneUV1;
                float4 boneUV2;
                float4 boneUV3;
                float4 boneUV4;
                float4 boneUV5;
                float4 boneUV6;
                float4 boneUV7;
                float4 boneUV8;
                int frameDataPixelIndex;
                static const int DEFAULT_PER_FRAME_BONE_DATASPACE = 8;
                
                float4 a;
                float4 b ;
                float4 c ;
                float4 d ;
                float4 e ;
                float4 f ;
                float4 g ;
                float4 h ;

                int FrameIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _FrameIndex);
                // ���ڲ��ŵĶ���
                frameDataPixelIndex = ((_BoneNum * (FrameIndex)) * DEFAULT_PER_FRAME_BONE_DATASPACE);
                // bone0
                boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                boneUV5 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 4);
                boneUV6 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 5);
                boneUV7 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 6);
                boneUV8 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 7);

                a = tex2Dlod(_AnimationTex, boneUV1);
                b = tex2Dlod(_AnimationTex, boneUV2);
                c = tex2Dlod(_AnimationTex, boneUV3);
                d = tex2Dlod(_AnimationTex, boneUV4);
                e = tex2Dlod(_AnimationTex, boneUV5);
                f = tex2Dlod(_AnimationTex, boneUV6);
                g = tex2Dlod(_AnimationTex, boneUV7);
                h = tex2Dlod(_AnimationTex, boneUV8);
                float4x4 bone0_matrix = DualQuaternionToMatrix(ConvertColors2Halfs(a, b, c, d), ConvertColors2Halfs(e, f, g, h));
                // bone1
                boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                boneUV5 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 4);
                boneUV6 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 5);
                boneUV7 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 6);
                boneUV8 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 7);
                
                a = tex2Dlod(_AnimationTex, boneUV1);
                b = tex2Dlod(_AnimationTex, boneUV2);
                c = tex2Dlod(_AnimationTex, boneUV3);
                d = tex2Dlod(_AnimationTex, boneUV4);
                e = tex2Dlod(_AnimationTex, boneUV5);
                f = tex2Dlod(_AnimationTex, boneUV6);
                g = tex2Dlod(_AnimationTex, boneUV7);
                h = tex2Dlod(_AnimationTex, boneUV8);
                float4x4 bone1_matrix = DualQuaternionToMatrix(ConvertColors2Halfs(a, b, c, d), ConvertColors2Halfs(e, f, g, h));
                
                // bone2
                boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                boneUV5 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 4);
                boneUV6 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 5);
                boneUV7 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 6);
                boneUV8 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 7);
                
                a = tex2Dlod(_AnimationTex, boneUV1);
                b = tex2Dlod(_AnimationTex, boneUV2);
                c = tex2Dlod(_AnimationTex, boneUV3);
                d = tex2Dlod(_AnimationTex, boneUV4);
                e = tex2Dlod(_AnimationTex, boneUV5);
                f = tex2Dlod(_AnimationTex, boneUV6);
                g = tex2Dlod(_AnimationTex, boneUV7);
                h = tex2Dlod(_AnimationTex, boneUV8);
                float4x4 bone2_matrix = DualQuaternionToMatrix(ConvertColors2Halfs(a, b, c, d), ConvertColors2Halfs(e, f, g, h));
                
                // bone3
                boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                boneUV5 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 4);
                boneUV6 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 5);
                boneUV7 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 6);
                boneUV8 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 7);
                
                a = tex2Dlod(_AnimationTex, boneUV1);
                b = tex2Dlod(_AnimationTex, boneUV2);
                c = tex2Dlod(_AnimationTex, boneUV3);
                d = tex2Dlod(_AnimationTex, boneUV4);
                e = tex2Dlod(_AnimationTex, boneUV5);
                f = tex2Dlod(_AnimationTex, boneUV6);
                g = tex2Dlod(_AnimationTex, boneUV7);
                h = tex2Dlod(_AnimationTex, boneUV8);
                float4x4 bone3_matrix = DualQuaternionToMatrix(ConvertColors2Halfs(a, b, c, d), ConvertColors2Halfs(e, f, g, h));
                
                
                //// ����Blend
                //int BlendFrameIndex = UNITY_ACCESS_INSTANCED_PROP(Props, _BlendFrameIndex);
                ////BlendFrameIndex = FrameIndex - 1;
                //frameDataPixelIndex = (_BoneNum * BlendFrameIndex * DEFAULT_PER_FRAME_BONE_DATASPACE) ;

                //// bone0
                //boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                //boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                //boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                //boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[0] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                //float4x4 bone0_matrix_blend = DualQuaternionToMatrix(convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2)), convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV3), tex2Dlod(_AnimationTex, boneUV4)));
                //// bone1
                //boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                //boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                //boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                //boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[1] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                //float4x4 bone1_matrix_blend = DualQuaternionToMatrix(convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2)), convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV3), tex2Dlod(_AnimationTex, boneUV4)));
                //// bone2
                //boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                //boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                //boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                //boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[2] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                //float4x4 bone2_matrix_blend = DualQuaternionToMatrix(convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2)), convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV3), tex2Dlod(_AnimationTex, boneUV4)));
                //// bone3
                //boneUV1 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE);
                //boneUV2 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 1);
                //boneUV3 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 2);
                //boneUV4 = indexToUV(frameDataPixelIndex + boneIndices[3] * DEFAULT_PER_FRAME_BONE_DATASPACE + 3);
                //float4x4 bone3_matrix_blend = DualQuaternionToMatrix(convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV1), tex2Dlod(_AnimationTex, boneUV2)), convertColors2Halfs(tex2Dlod(_AnimationTex, boneUV3), tex2Dlod(_AnimationTex, boneUV4)));
                
                //float BlendProgress = UNITY_ACCESS_INSTANCED_PROP(Props, _BlendProgress);
                //BlendProgress = 0.5;
                //bone0_matrix = lerp(bone0_matrix, bone0_matrix_blend, BlendProgress);
                //bone1_matrix = lerp(bone1_matrix, bone1_matrix_blend, BlendProgress);
                //bone2_matrix = lerp(bone2_matrix, bone2_matrix_blend, BlendProgress);
                //bone3_matrix = lerp(bone3_matrix, bone3_matrix_blend, BlendProgress);


                float4 pos = mul(bone0_matrix, v.vertex) * boneWeights[0] +
                mul(bone1_matrix, v.vertex) * boneWeights[1] +
                mul(bone2_matrix, v.vertex) * boneWeights[2] +
                mul(bone3_matrix, v.vertex) * boneWeights[3];

                //////////////////////////

                o.vertex = UnityObjectToClipPos(pos);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.normal = normalize(o.normal);
                
                return o;
            }
            

            half4 frag(v2f i): SV_Target
            {
                //UNITY_SETUP_INSTANCE_ID(i);
                i.normal = normalize(i.normal);
                fixed4 Albedo = _Tint * tex2D(_MainTex, i.uv);
                fixed4 Mask = _Tint * tex2D(_Mask, i.uv);
                float r = Mask.r;
                r = r * (1 - _IsPlayer) ;
                Albedo = (1 - r) * Albedo + r * _Color;
                //fixed4 packedNormal = tex2D(_NormalTex, i.uv);
                //fixed3 tangentNormal = UnpackNormal(packedNormal);
                
                ///PBR
                half3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos.xyz);
                half3 lightColor = _LightColor0.rgb;
                half3 halfVector = normalize(lightDir + viewDir);  //�������

                half perceptualRoughness = 1 - _Smoothness;//SmoothnessToPerceptualRoughness

                half roughness = perceptualRoughness * perceptualRoughness;
                half squareRoughness = roughness * roughness;

                half nl = max(saturate(dot(i.normal, lightDir)), 0.000001);//��ֹ��0
                half nv = max(saturate(dot(i.normal, viewDir)), 0.000001);
                half vh = max(saturate(dot(viewDir, halfVector)), 0.000001);
                half lh = max(saturate(dot(lightDir, halfVector)), 0.000001);
                half nh = max(saturate(dot(i.normal, halfVector)), 0.000001);
                

                half lerpSquareRoughness = pow(lerp(0.002, 1, roughness), 2);//Unity��roughness lerp����0.002
                half D = lerpSquareRoughness / (pow((pow(nh, 2) * (lerpSquareRoughness - 1) + 1), 2) * UNITY_PI);

                half kInDirectLight = pow(squareRoughness + 1, 2) / 8;
                half kInIBL = pow(squareRoughness, 2) / 8;
                half GLeft = nl / lerp(nl, 1, kInDirectLight);
                half GRight = nv / lerp(nv, 1, kInDirectLight);
                half G = GLeft * GRight;

                half3 F0 = lerp(unity_ColorSpaceDielectricSpec.rgb, Albedo, _Metallic);
                half3 F = F0 + (1 - F0) * exp2((-5.55473 * vh - 6.98316) * vh);

                half3 SpecularResult = (D * G * F * 0.25) / (nv * nl);

                //������ϵ��
                half3 kd = (1 - F) * (1 - _Metallic);

                //ֱ�ӹ��ղ��ֽ��
                //fixed3 diffuse = saturate(dot(tangentNormal, lightDir));//������ͼ
                half3 specColor = SpecularResult * lightColor * nl * UNITY_PI;
                //half3 diffColor = kd * Albedo * lightColor * diffuse;
                //half3 diffColor = kd * Albedo * lightColor * (nl + diffuse);
                half3 diffColor = kd * Albedo * lightColor * (nl + 0.2);
                half3 DirectLightResult = diffColor + specColor;
                
                half3 ambient_contrib = ShadeSH9(half4(i.normal, 1));
                //half3 ambient = 0.03 * Albedo;
                fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz * _Diffuse.xyz ;
                
                half3 iblDiffuse = max(half3(0, 0, 0), ambient.rgb + ambient_contrib);
                
                half mip_roughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
                half3 reflectVec = reflect(-viewDir, i.normal);
                
                half mip = mip_roughness * UNITY_SPECCUBE_LOD_STEPS;
                half4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectVec, mip);
                
                half3 iblSpecular = DecodeHDR(rgbm, unity_SpecCube0_HDR);
                
                half2 envBDRF = tex2D(_LUT, half2(lerp(0, 0.99, nv), lerp(0, 0.99, roughness))).rg; // LUT����
                
                //half3 Flast = fresnelSchlickRoughness(max(nv, 0.0), F0, roughness);
                half3 Flast = FresnelTerm(specColor, lh);
                half kdLast = (1 - Flast) * (1 - _Metallic);
                
                half3 iblDiffuseResult = iblDiffuse * kdLast * Albedo;
                half3 iblSpecularResult = iblSpecular * (Flast * envBDRF.r + envBDRF.g);
                half3 IndirectResult = iblDiffuseResult + iblSpecularResult;
                half4 result = half4(DirectLightResult + IndirectResult, 1);
                return result;

                //UnityLight ulight={DirectLightResult,lightDir,0};
                //UnityIndirect gi={iblDiffuseResult,iblSpecularResult};
                //half4 oneMinusReflectivity=	OneMinusReflectivityFromMetallic(_Metallic);
                
                //UnityGI ugi={ulight,gi};
                ////��һ��������half3�͵�diffColor����ʾ��������ɫ��ֵ��
                ////�ڶ���������half3�͵�specColor����ʾ���淴����ɫֵ��
                ////������������half�͵�oneMinusReflectivity����ʾ1��ȥ�����ʵ�ֵ��
                ////���ĸ�������half�͵�oneMinusRoughness����ʾ1��ȥ�ֲڶȵ�ֵ��
                ////����β�����half3�͵�normal����ʾ���ߵķ���
                ////������������half3�͵�viewDir����ʾ���ߵķ���
                ////���߸�������UnityLight�͵�light����ʾUnity�й�Դ�����Ľṹ�壬����half3�͵Ĺ�Դ��ɫcolor��half3�͵Ĺ�Դ����dir��half�͵�����ⷽ��͵�ǰ���淨�߷���ĵ�˵Ļ�ndotl��
                ////�ڰ˸�������UnityIndirect���͵�gi ��һ��������half3�͵���������ɫdiffuse��half3�͵ľ��淴����ɫspecular�Ĺ��߷���ṹ�壬��ʾ��ӹ�����Ϣ��
                //half4 c = UNITY_BRDF_PBS (diffColor, specColor, oneMinusReflectivity, _Smoothness,i.normal, viewDir, ugi.light, ugi.indirect);
                
                ////SurfaceOutputStandard s=
                ////{
                    ////	Albedo,
                    ////	i.normal,
                    ////	half3(0,0,0),
                    ////	_Metallic,
                    ////	_Smoothness,
                    ////	1,
                    ////	0
                    ////};
                    ////half4 c = LightingStandard(s,viewDir,ugi);
                    //return c;
                }
                ENDCG

            }
        }
        FallBack "Diffuse"
    }
