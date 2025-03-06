//from http://forum.unity3d.com/threads/68402-Making-a-2D-game-for-iPhone-iPad-and-need-better-performance

Shader "Custom/RenderOutOfFOV" //Unlit Transparent Vertex Colored
{
	Properties 
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}
	
	Category 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		Blend One Zero
		Fog { Color(0,0,0,0) }
		Lighting Off
		Cull Off //we can turn backface culling off because we know nothing will be facing backwards

		BindChannels 
		{
			Bind "Vertex", vertex
			Bind "texcoord", texcoord 
			Bind "Color", color 
		}

		/*
		0 = Visible
		1 = Invisible to all
		2 = Invisible to me
		*/
		
		SubShader   
		{
			Pass
			{
				Stencil
				{
					Ref 0
					Comp NotEqual
				}
			
				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"
				
				sampler2D _MainTex;
				
				uniform float _los_visibility;
				
				struct appdata {
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
					fixed4 color : COLOR;
				};

				struct v2f {
					float4 pos : SV_POSITION;
					float2 uv : TEXCOORD0;
					fixed4 color : COLOR;
				};

				float4 _MainTex_ST;

				v2f vert(appdata v)
				{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
					o.color = v.color;
					return o;
				}
				
				half4 frag(v2f i) : COLOR
				{
					half4 col = tex2D(_MainTex, i.uv);
					col.rgb = lerp(i.color.rgb, col.rgb, _los_visibility);
					return col;
				}
				ENDCG
			}
		} 
	}
}



//Blend SrcAlpha OneMinusSrcAlpha 