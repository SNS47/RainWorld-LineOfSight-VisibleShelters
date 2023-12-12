using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LineOfSight
{
    class OptionsMenu : OptionInterface
    {
        public readonly Configurable<int> visibility;
        public readonly Configurable<int> brightness;
        public readonly Configurable<int> tileSize;

        public OptionsMenu(LineOfSightMod plugin)
        {
            visibility = this.config.Bind<int>("LineOfSight_Visibility", 65);
            brightness = this.config.Bind<int>("LineOfSight_Brightness", 65);
            tileSize = this.config.Bind<int>("LineOfSight_TileSize", 8);
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
                new OpSlider(visibility, new Vector2(50, 500), 200){max = 100, hideLabel = false, description = "How visible unseen areas of the map are. Lower values fade into a solid color."},
                new OpLabel(265, 506, "Unseen area visibility"),
                new OpSlider(brightness, new Vector2(50,420),200){max = 100, hideLabel = false, description = "Brightness of the unseen area color, which is based on the room's pallete. 0 is black."},
                new OpLabel(265, 426, "Unseen area brightness"),
                new OpSlider(tileSize, new Vector2(50,340),200){min = 2, max = 10, hideLabel = false, description = "Size of tiles when calculating line of sight. Low values improve visibility in tunnels and around corners. 10 is the actual size of tiles."},
                new OpLabel(265, 346, "Tile Size")
            };
            opTab1.AddItems(UIArrayElements);
        }
    }
}
