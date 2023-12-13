using BepInEx;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace LineOfSight
{
    [BepInPlugin("LineOfSight", "Line Of Sight", "1.2.0")] // (GUID, mod name, mod version)
	public class LineOfSightMod : BaseUnityPlugin
	{
		private OptionsMenu optionsMenuInstance;
		private bool initialized = false;

		public static bool classic = false;

		// FOR MULTIPLE PLAYERS:
		// Same mesh generation process repeated for all players
		// The shader process goes like this:
		// 1 - Draw LOS mesh, set bit 1 of the stencil mask
		// 2 - Draw fullscreen quad, if bit 1 is not set then set bit 0
		// 3 - Draw fullscreen quad, unset bit 1
		// 4 - Repeat steps 1-3 for each player
		// 5 - Draw fullscreen quad, if bit 0 is set then draw LOS blocker

		public void OnEnable()
		{
            On.RoomCamera.DrawUpdate += RoomCamera_DrawUpdate;
			On.Room.Loaded += Room_Loaded;
			On.RainWorld.OnModsInit += OnModInit;
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

		private FieldInfo _RoomCamera_spriteLeasers = typeof(RoomCamera).GetField("spriteLeasers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private void RoomCamera_DrawUpdate(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
		{
			
			orig(self, timeStacker, timeSpeed);
			List<RoomCamera.SpriteLeaser> list = (List<RoomCamera.SpriteLeaser>)this._RoomCamera_spriteLeasers.GetValue(self);
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

		private void Room_Loaded(On.Room.orig_Loaded orig, Room self)
		{
			classic = optionsMenuInstance.classic.Value;
			if (self.game != null)
			{
				LOSController owner;
				self.AddObject(owner = new LOSController(self, optionsMenuInstance));
				self.AddObject(new ShortcutDisplay(owner));
			}
			orig(self);
		}
	}
}
