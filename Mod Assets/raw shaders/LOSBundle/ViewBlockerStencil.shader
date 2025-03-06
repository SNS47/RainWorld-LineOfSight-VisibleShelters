//from http://forum.unity3d.com/threads/68402-Making-a-2D-game-for-iPhone-iPad-and-need-better-performance

Shader "Custom/ViewBlockerStencil" //Unlit Transparent Vertex Colored
{
	Properties 
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}
	
	Category 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		Blend Zero One
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
					Ref 1
					Comp Equal
					Pass IncrSat
				}
			
				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"
				
				sampler2D _MainTex;
				
				uniform int2 _los_camPos;
				
				struct appdata {
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
					fixed4 color : COLOR;
				};

				struct v2f {
					float4 pos : SV_POSITION;
					fixed4 color : COLOR;
				};

				float4 _MainTex_ST;

				v2f vert(appdata v)
				{
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.color = v.color;
					return o;
				}
				
				float Bayer2(int2 a) {
					a = a % 2;
					return frac(a.x * 0.5 + a.y * 0.75);
				}

				#define Bayer4(a)   ( Bayer2( a/2)*0.25 + Bayer2(a) )
				#define Bayer8(a)   ( Bayer4( a/2)*0.25 + Bayer2(a) )
				#define Bayer16(a)  ( Bayer8( a/2)*0.25 + Bayer2(a) )
				#define Bayer32(a)  ( Bayer16(a/2)*0.25 + Bayer2(a) )
				#define Bayer64(a)  ( Bayer32(a/2)*0.25 + Bayer2(a) )
				
				half4 frag(v2f i) : COLOR
				{
					int2 pixel = int2(i.pos.xy) + _los_camPos;
					if (pow(Bayer64(pixel), 1.5) >= smoothstep(0, 1, i.color.a))
						discard;
					return 0;
				}
				ENDCG
			}
		}
	}
}