Shader "Custom/WireframeGlow" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 2
        _MainTex ("Texture", 2D) = "white" {}
        _PatternTex ("Pattern Texture", 2D) = "white" {}
        _PatternSpeed ("Pattern Speed", Range(0, 5)) = 1
        _PatternScale ("Pattern Scale", Range(0.1, 10)) = 1
        _WireThickness ("Wire Thickness", Range(0, 1)) = 0.5
        _WireSpacing ("Wire Spacing", Range(0.1, 10)) = 2
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
                float2 patternUv : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 worldPos : TEXCOORD3;
            };
            
            sampler2D _MainTex;
            sampler2D _PatternTex;
            float4 _MainTex_ST;
            float4 _PatternTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float _PatternSpeed;
            float _PatternScale;
            float _WireThickness;
            float _WireSpacing;
            
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Calculate world position for pattern
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                
                // Create scrolling pattern UV based on world position
                o.patternUv.x = o.worldPos.x * _PatternScale + _Time.y * _PatternSpeed;
                o.patternUv.y = o.worldPos.z * _PatternScale + _Time.y * _PatternSpeed * 0.5;
                
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                
                // Create wireframe pattern
                float2 patternUV = i.patternUv;
                float pattern = tex2D(_PatternTex, patternUV).r;
                
                // Create wireframe effect
                float wirePattern = frac(i.worldPos.x * _WireSpacing) < _WireThickness || 
                                   frac(i.worldPos.z * _WireSpacing) < _WireThickness;
                
                // Combine patterns
                float finalPattern = max(pattern * 0.5, wirePattern);
                
                // Apply pattern to alpha
                col.a *= finalPattern;
                
                // Add pulsing emission
                float pulse = 0.5 + 0.5 * sin(_Time.y * 2.0);
                float emission = _EmissionIntensity * (0.7 + 0.3 * pulse);
                
                // Add emission
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
