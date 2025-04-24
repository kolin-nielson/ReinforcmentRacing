Shader "Custom/CheckpointGlow" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2
        _MainTex ("Texture", 2D) = "white" {}
        _ScanLineSpeed ("Scan Line Speed", Range(0, 10)) = 2
        _ScanLineWidth ("Scan Line Width", Range(0, 1)) = 0.1
        _ScanLineIntensity ("Scan Line Intensity", Range(0, 5)) = 2
        _GridSize ("Grid Size", Range(0.1, 10)) = 2
        _GridThickness ("Grid Thickness", Range(0, 0.5)) = 0.05
    }
    
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
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
            float _ScanLineSpeed;
            float _ScanLineWidth;
            float _ScanLineIntensity;
            float _GridSize;
            float _GridThickness;
            
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
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // Create grid pattern
                float2 grid = frac(i.worldPos.xz * _GridSize);
                float gridPattern = step(grid.x, _GridThickness) || step(grid.y, _GridThickness);
                
                // Create scan line effect
                float scanLine = frac(i.worldPos.y * 0.5 + _Time.y * _ScanLineSpeed);
                scanLine = step(1.0 - _ScanLineWidth, scanLine);
                
                // Create pulse effect
                float pulse = 0.5 + 0.5 * sin(_Time.y * 2.0);
                
                // Combine effects
                float finalPattern = max(gridPattern, scanLine * _ScanLineIntensity);
                
                // Apply pattern
                col.a *= max(0.3, finalPattern);
                
                // Add emission with pulse
                float emission = _EmissionIntensity * (0.7 + 0.3 * pulse);
                col.rgb += _EmissionColor.rgb * emission * finalPattern;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Sprites/Default"
}
