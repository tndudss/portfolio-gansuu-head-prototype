Shader "GansuuHead/CurvedEyelidUI"
{
    Properties
    {
        _MainTex("UI texture",2D)="white"{}
        _Closure("Closure",Range(0,1))=0
        _Curvature("Curvature",Float)=1.7
        _UpperShare("Upper motion share",Range(0,1))=.65
        _Feather("Feather",Float)=.008
        _OpeningWidth("Opening width",Float)=1
        _LateLift("Late aperture lift",Float)=.21
        _Tremor("Tremor offset",Float)=0
        _BlurStrength("Peripheral blur mix",Range(0,1))=.18
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "CurvedEyelid"
            ZWrite Off ZTest Always Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float _Closure,_Curvature,_UpperShare,_Feather,_OpeningWidth,_Tremor,_BlurStrength,_LateLift;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float c=saturate(_Closure);
                if(c<=.00001) return half4(0,0,0,0);
                if(c>=.999999) return half4(0,0,0,1);
                float2 uv=input.uv;
                float x=(uv.x*2-1)/max(.25,_OpeningWidth);
                float bow=max(0,_Curvature)*c*x*x;
                float upper=saturate(_UpperShare),lower=1-upper;
                float lift=_LateLift*c*c*c;
                float top=1-upper*(c+bow+_Tremor)+lift;
                float bottom=lower*(c+bow+_Tremor)+lift;
                float distance=min(top-uv.y,uv.y-bottom);
                float feather=max(.0005,_Feather);
                float visible=smoothstep(-feather,feather,distance);
                float mask=1-visible;
                // Scene luminance is never globally multiplied or faded.
                float blur=visible*saturate(_BlurStrength)*c*
                    (1-smoothstep(feather,feather*8+.025,distance));
                float3 blurred=0;
                if(blur>.0001)
                {
                    float2 d=_CameraOpaqueTexture_TexelSize.xy*1.25;
                    blurred=SampleSceneColor(uv)*.4;
                    blurred+=SampleSceneColor(uv+float2(d.x,0))*.15;
                    blurred+=SampleSceneColor(uv-float2(d.x,0))*.15;
                    blurred+=SampleSceneColor(uv+float2(0,d.y))*.15;
                    blurred+=SampleSceneColor(uv-float2(0,d.y))*.15;
                }
                float alpha=mask+visible*blur;
                float3 color=visible*blur*blurred/max(alpha,.00001);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}

