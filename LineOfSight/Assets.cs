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
		
		public static string AssetBundlePath
		{
			get
			{
				return AssetManager.ResolveFilePath(bundleName);
			}
		}

        private static Shader LevelOutOfFOVShader;
        public static Shader LevelOutOfFOV
        {
            get
            {
				if (AssetBundle == null)
					AssetBundle = AssetBundle.LoadFromFile(AssetBundlePath);
				if (LevelOutOfFOVShader == null)
                    LevelOutOfFOVShader = AssetBundle.LoadAsset<Shader>("LevelOutOfFOV.shader");
				return LevelOutOfFOVShader;
			}
        }

        private static Shader RenderOutOfFOVShader;
        public static Shader RenderOutOfFOV
        {
            get
            {
                if (AssetBundle == null)
                    AssetBundle = AssetBundle.LoadFromFile(AssetBundlePath);
                if (RenderOutOfFOVShader == null)
                    RenderOutOfFOVShader = AssetBundle.LoadAsset<Shader>("RenderOutOfFOV.shader");
                return RenderOutOfFOVShader;
            }
        }
    }
}
