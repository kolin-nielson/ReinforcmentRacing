Shader "Custom/RaycastGlow" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2
        _MainTex ("Texture", 2D) = "white" {}
        _FlowSpeed ("Flow Speed", Range(0, 10)) = 3
        _FlowIntensity ("Flow Intensity", Range(0, 1)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 5
    }
    
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        Blend SrcAlpha One // Additive blending for glow effect
        ZWrite Off
        Cull Off
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPos : TEXCOORD2;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float _FlowSpeed;
            float _FlowIntensity;
            float _PulseSpeed;
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                // Create flowing effect along the raycast
                float flowOffset = _Time.y * _FlowSpeed;
                float2 flowUV = i.uv;
                flowUV.x = frac(flowUV.x - flowOffset);
                
                // Sample the texture with flow effect
                fixed4 col = tex2D(_MainTex, flowUV) * i.color;
                
                // Create pulse effect
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                
                // Create tapering effect (thinner at the end)
                float taper = 1.0 - i.uv.x;
                taper = pow(taper, 0.5); // Adjust power for different taper shapes
                
                // Apply flow intensity
                float flow = sin(i.uv.x * 10.0 - flowOffset * 5.0) * 0.5 + 0.5;
                flow = lerp(1.0, flow, _FlowIntensity);
                
                // Combine effects
                col.a *= flow * taper;
                
                // Add emission with pulse
                float emission = _EmissionIntensity * (0.7 + 0.3 * pulse);
                col.rgb += _EmissionColor.rgb * emission * col.a;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Sprites/Default"
}
