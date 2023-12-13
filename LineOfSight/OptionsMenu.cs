using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LineOfSight
{
    class OptionsMenu : OptionInterface
    {
        public readonly Configurable<bool> classic;
        public readonly Configurable<int> visibility;
        public readonly Configurable<int> brightness;
        public readonly Configurable<int> tileSize;

        public OptionsMenu(LineOfSightMod plugin)
        {
            classic = this.config.Bind<bool>("LineOfSight_ClassicMode", false);
            visibility = this.config.Bind<int>("LineOfSight_Visibility", 100);
            brightness = this.config.Bind<int>("LineOfSight_Brightness", 65);
            tileSize = this.config.Bind<int>("LineOfSight_TileSize", 8);

            On.OptionInterface._LoadConfigFile += OnLoad;
            On.OptionInterface._SaveConfigFile += OnSave;
        }

        public override void Initialize()
        {
            var opTab1 = new OpTab(this, "Options");
            this.Tabs = new[] { opTab1 };

            // Tab 1
            OpContainer tab1Container = new OpContainer(new Vector2(0, 0));
            opTab1.AddItems(tab1Container);

            UIelement[] UIArrayElements = new UIelement[] //create an array of ui elements
            {
                new OpSlider(visibility, new Vector2(50, 500), 200){min = 75, max = 100, hideLabel = false, description = "How visible unseen areas of the map are. Lower values fade into a solid color."},
                new OpLabel(265, 506, "Unseen Area Visibility"),
                new OpCheckBox(classic, new Vector2(150, 460)){description = "Replaces unseen areas with solid color. Effectively 0 visibility. Hard."},
                new OpLabel(265, 466, "Classic Mode"),
                new OpSlider(brightness, new Vector2(50,380),200){max = 100, hideLabel = false, description = "Brightness of the unseen area color, which is based on the room's pallete. 0 is black."},
                new OpLabel(265, 386, "Unseen Area Brightness"),
                new OpSlider(tileSize, new Vector2(50,300),200){min = 2, max = 10, hideLabel = false, description = "Size of tiles when calculating line of sight. Low values improve visibility in tunnels and around corners. 10 is the actual size of tiles."},
                new OpLabel(265, 306, "Tile Size")
            };
            opTab1.AddItems(UIArrayElements);
        }

        private void OnLoad(On.OptionInterface.orig__LoadConfigFile orig, OptionInterface self)
        {
            orig(self);
            if (self == this)
                UpdateConfigs();
        }

        private void OnSave(On.OptionInterface.orig__SaveConfigFile orig, OptionInterface self)
        {
            orig(self);
            if (self == this)
                UpdateConfigs();
        }

        //I don't like how long it is to type Options.tileSize.Value.
        //called only when configs are loaded or changed.
        public void UpdateConfigs()
        {
            LOSController.classic = classic.Value;
            LOSController.visibility = visibility.Value / 100f;
            LOSController.brightness = brightness.Value / 100f;
            LOSController.tileSize = tileSize.Value;
            LOSController.fovShader = null;
        }
    }
}
