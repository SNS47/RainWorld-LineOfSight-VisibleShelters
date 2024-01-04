using BepInEx;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;
using LineOfSight;
using BepInEx.Logging;
using System.Runtime.CompilerServices;
using JollyCoop;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace LineOfSight
{
    [BepInPlugin("LineOfSight", "Line Of Sight", "3.1.2")] // (GUID, mod name, mod version)
	public class LineOfSightMod : BaseUnityPlugin
	{
		private OptionsMenu optionsMenuInstance;
		private bool initialized = false;

		public static Type[] blacklist = {
            typeof(PhysicalObject),
			typeof(WaterDrip),
            typeof(Smoke.SmokeSystem.SmokeSystemParticle),
            typeof(ExplosionSpikes),
            typeof(Explosion.ExplosionLight),
            typeof(Explosion.ExplosionSmoke),
            typeof(SporeCloud),
			typeof(SlimeMoldLight),
			typeof(Bubble),
			typeof(CosmeticInsect),
			typeof(WormGrass.Worm),
            typeof(GraphicsModule),
            typeof(LizardBubble),
            typeof(Spark),
            typeof(DaddyBubble),
            typeof(DaddyRipple),
            typeof(DaddyCorruption),
            typeof(MouseSpark),
            typeof(CentipedeShell),
			typeof(Spear.Umbilical)
        };

		public static Type[] whitelist = {
			typeof(PlayerGraphics),
			typeof(Smoke.SteamSmoke.SteamParticle)
		};

        public void OnEnable()
		{
            On.RainWorld.OnModsInit += OnModInit;

			On.FScreen.ctor += FScreen_ctor;
			On.FScreen.ReinitRenderTexture += FScreen_ReinitRenderTexture;

            On.RoomCamera.DrawUpdate += RoomCamera_DrawUpdate;
			On.Room.Loaded += Room_Loaded;
			On.Room.Update += Room_Update;

            LOSController.AddBlacklistedTypes(blacklist);
			LOSController.AddWhitelistedTypes(whitelist);
        }

        private void OnModInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
		{
			orig(self);

			// initialize options menu
			if (this.initialized)
			{
				return;
			}
			this.initialized = true;

			optionsMenuInstance = new OptionsMenu(this);
			try
			{
				MachineConnector.SetRegisteredOI("LineOfSight", optionsMenuInstance);
			}
			catch (Exception ex)
			{
				Debug.Log($"Remix Menu Template examples: Hook_OnModsInit options failed init error {optionsMenuInstance}{ex}");
				Logger.LogError(ex);
				Logger.LogMessage("WHOOPS");
			}
        }
        private void FScreen_ctor(On.FScreen.orig_ctor orig, FScreen self, FutileParams futileParams)
        {
			orig(self, futileParams);
			RenderTexture rt = new RenderTexture(self.renderTexture.width, self.renderTexture.height, 24);
			self.renderTexture.Release();
			self.renderTexture.DiscardContents();
			self.renderTexture = rt;
        }

        private void FScreen_ReinitRenderTexture(On.FScreen.orig_ReinitRenderTexture orig, FScreen self, int displayWidth)
        {
            orig(self, displayWidth);
            RenderTexture rt = new RenderTexture(self.renderTexture.width, self.renderTexture.height, 24);
			rt.filterMode = self.renderTexture.filterMode;
            self.renderTexture.Release();
            self.renderTexture.DiscardContents();
            self.renderTexture = rt;
        }

        private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
        {
            if (self.game != null)
            {
                LOSController owner;
                self.AddObject(owner = new LOSController(self));
            }
            orig(self);
        }

        private void Room_Update(On.Room.orig_Update orig, Room self)
        {
			orig(self);
            LOSController los = (LOSController)self.updateList.Find(x => typeof(LOSController).IsInstanceOfType(x));
            los?.LateUpdate();
        }

        private void RoomCamera_DrawUpdate(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
		{
			
			orig(self, timeStacker, timeSpeed);
			List<RoomCamera.SpriteLeaser> list = list = self.spriteLeasers;
            LOSController.hackToDelayDrawingUntilAfterTheLevelMoves = true;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].drawableObject is LOSController)
				{
					list[i].Update(timeStacker, self, Vector2.zero);
					if (list[i].deleteMeNextFrame)
					{
						list.RemoveAt(i);
					}
				}
			}
			LOSController.hackToDelayDrawingUntilAfterTheLevelMoves = false;
		}
	}
}
