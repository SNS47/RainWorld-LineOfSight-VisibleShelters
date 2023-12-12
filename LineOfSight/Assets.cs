using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LineOfSight
{
	public static class Assets
	{
		public static AssetBundle AssetBundle;
		public const string bundleName = "losbundle";
		private static Shader Shader;
		public static string AssetBundlePath
		{
			get
			{
				return AssetManager.ResolveFilePath(bundleName);
			}
		}

		public static Shader LOSShader
        {
            get
            {
				if (AssetBundle == null)
					AssetBundle = AssetBundle.LoadFromFile(AssetBundlePath);
				if (Shader == null)
					Shader = AssetBundle.LoadAsset<Shader>("LOSShader.shader");
				return Shader;
			}
        }
	}
}
