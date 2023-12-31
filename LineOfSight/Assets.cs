using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using UnityEngine;

namespace LineOfSight
{
	public static class Assets
	{
		public const string bundleName = "losbundle";
		public static string AssetBundlePath
		{
			get
			{
				return AssetManager.ResolveFilePath(bundleName);
			}
		}

        private static AssetBundle _AssetBundle;
        public static AssetBundle AssetBundle
        {
            get
            {
                if (_AssetBundle == null)
                    _AssetBundle = AssetBundle.LoadFromFile(AssetBundlePath);
                return _AssetBundle;
            }
        }

        private static Shader _LevelOutOfFOV;
        public static Shader LevelOutOfFOV
        {
            get
            {
				if (_LevelOutOfFOV == null)
                    _LevelOutOfFOV = AssetBundle.LoadAsset<Shader>("LevelOutOfFOV.shader");
				return _LevelOutOfFOV;
			}
        }

        private static Shader _RenderOutOfFOV;
        public static Shader RenderOutOfFOV
        {
            get
            {
                if (_RenderOutOfFOV == null)
                    _RenderOutOfFOV = AssetBundle.LoadAsset<Shader>("RenderOutOfFOV.shader");
                return _RenderOutOfFOV;
            }
        }

        private static FShader _PreBlockerStencil;
        public static FShader PreBlockerStencil
        {
            get
            {
                if (_PreBlockerStencil == null)
                    _PreBlockerStencil = FShader.CreateShader("PreBlockerStencil", AssetBundle.LoadAsset<Shader>("PreBlockerStencil.shader"));
                return _PreBlockerStencil;
            }
        }

        private static FShader _ViewBlockerStencil;
        public static FShader ViewBlockerStencil
        {
            get
            {
                if (_ViewBlockerStencil == null)
                    _ViewBlockerStencil = FShader.CreateShader("ViewBlockerStencil", AssetBundle.LoadAsset<Shader>("ViewBlockerStencil.shader"));
                return _ViewBlockerStencil;
            }
        }

        private static FShader _UnblockerStencil;
        public static FShader UnblockerStencil
        {
            get
            {
                if (_UnblockerStencil == null)
                    _UnblockerStencil = FShader.CreateShader("UnblockerStencil", AssetBundle.LoadAsset<Shader>("UnblockerStencil.shader"));
                return _UnblockerStencil;
            }
        }

        private static FShader _ScreenBlockerStencil;
        public static FShader ScreenBlockerStencil
        {
            get
            {
                if (_ScreenBlockerStencil == null)
                    _ScreenBlockerStencil = FShader.CreateShader("ScreenBlockerStencil", AssetBundle.LoadAsset<Shader>("ScreenBlockerStencil.shader"));
                return _ScreenBlockerStencil;
            }
        }
    }
}
