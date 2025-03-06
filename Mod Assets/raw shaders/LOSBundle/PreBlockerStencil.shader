//from http://forum.unity3d.com/threads/68402-Making-a-2D-game-for-iPhone-iPad-and-need-better-performance

Shader "Custom/PreBlockerStencil" //Unlit Transparent Vertex Colored
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
					Comp Always
					Pass Replace
				}
			}
		}
	}
}