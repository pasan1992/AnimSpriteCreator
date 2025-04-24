Shader "Custom/PixelArtShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Range(1, 100)) = 10
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThickness ("Outline Thickness", Range(0, 10)) = 1
        _OutlineThreshold ("Outline Threshold", Range(0, 1)) = 0.1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Transparent"
        }
        
        // First pass: Render the pixelated image
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _PixelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate pixelated UV coordinates
                float2 pixelUV = floor(i.uv * _PixelSize) / _PixelSize;
                
                // Sample the texture with pixelated coordinates
                fixed4 col = tex2D(_MainTex, pixelUV);
                return col;
            }
            ENDCG
        }
        
        // Second pass: Add outlines
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _PixelSize;
            float4 _OutlineColor;
            float _OutlineThickness;
            float _OutlineThreshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Pixelate UV coordinates
                float2 pixelUV = floor(i.uv * _PixelSize) / _PixelSize;
                
                // Calculate pixel size in UV space
                float2 pixelSize = 1.0 / _PixelSize;
                
                // Sample the center pixel
                fixed4 center = tex2D(_MainTex, pixelUV);
                
                // Sample the neighboring pixels (for edge detection)
                fixed4 up = tex2D(_MainTex, pixelUV + float2(0, pixelSize.y) * _OutlineThickness);
                fixed4 right = tex2D(_MainTex, pixelUV + float2(pixelSize.x, 0) * _OutlineThickness);
                fixed4 down = tex2D(_MainTex, pixelUV + float2(0, -pixelSize.y) * _OutlineThickness);
                fixed4 left = tex2D(_MainTex, pixelUV + float2(-pixelSize.x, 0) * _OutlineThickness);
                
                // Check for edges (significant difference in color)
                float edgeH = distance(center.rgb, right.rgb) + distance(center.rgb, left.rgb);
                float edgeV = distance(center.rgb, up.rgb) + distance(center.rgb, down.rgb);
                
                // Combine the horizontal and vertical edges
                float edge = (edgeH + edgeV) / 2;
                
                // Apply threshold to create bold outlines
                edge = step(_OutlineThreshold, edge);
                
                // Combine the outline with the original color
                fixed4 col = lerp(center, _OutlineColor, edge);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}