using Menu.Remix.MixedUI;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace LineOfSight
{
    class LineOfSightRemixMenu : OptionInterface
    {
        //render settings
        public readonly Configurable<int> renderMode;
        public readonly Configurable<int> visibility;
        public readonly Configurable<int> brightness;
        public readonly Configurable<int> tileSize;
        public readonly Configurable<int> viewRange;
        public readonly Configurable<bool> ditherUsesWorldPos;
        public readonly Configurable<int> allowVisionUnconsciousSingleplayer;
        public readonly Configurable<int> allowVisionUnconsciousMultiplayer;

        //filter settings
        public readonly Configurable<bool> showOverseer;
        public readonly Configurable<bool> showInsect;
        public readonly Configurable<bool> showPolePlant;
        public readonly Configurable<bool> showRot;
        public readonly Configurable<bool> showWormGrass;
        public readonly Configurable<bool> showMonsterKelp;
        public readonly Configurable<bool> showShorcutEntrance;

        private OpTab renderingTab;
        private OpTab fancyTab;

        private OpRadioButtonGroup renderModeSelect;
        private OpSlider visibilitySlider;

        public LineOfSightRemixMenu(LineOfSightMod plugin)
        {
            renderMode = this.config.Bind<int>("LineOfSight_RenderMode", 2);
            visibility = this.config.Bind<int>("LineOfSight_Visibility", 80);
            brightness = this.config.Bind<int>("LineOfSight_Brightness", 80);
            tileSize = this.config.Bind<int>("LineOfSight_TileSize", 8);
            viewRange = this.config.Bind<int>("LineOfSight_ViewRange", 80);
            ditherUsesWorldPos = this.config.Bind<bool>("LineOfSight_DitherUsesWorldPos", false);
            allowVisionUnconsciousSingleplayer = this.config.Bind<int>("LineOfSight_AllowVisionUnconsciousSingleplayer", 1);
            allowVisionUnconsciousMultiplayer = this.config.Bind<int>("LineOfSight_AllowVisionUnconsciousMultiplayer", 0);

            showOverseer = this.config.Bind<bool>("LineOfSight_ShowOverseer", true);
            showInsect = this.config.Bind<bool>("LineOfSight_ShowInsect", false);
            showPolePlant = this.config.Bind<bool>("LineOfSight_ShowPolePant", true);
            showRot = this.config.Bind<bool>("LineOfSight_ShowRot", true);
            showWormGrass = this.config.Bind<bool>("LineOfSight_ShowWormGrass", false);
            showMonsterKelp = this.config.Bind<bool>("LineOfSight_ShowMonsterKep", false);
            showShorcutEntrance = this.config.Bind<bool>("LineOfSight_ShowShortcutEntrance", true);

            UpdateConfigs();
            typeof(OptionInterface).GetEvent("OnConfigChanged").GetAddMethod().Invoke(this, new object[] { (OnEventHandler)UpdateConfigs });
            typeof(OptionInterface).GetEvent("OnUnload").GetAddMethod().Invoke(this, new object[] { (OnEventHandler)Unload });
        }

        public void Unload()
        {
            renderModeSelect.Unload();
            renderModeSelect = null;
            visibilitySlider.Unload();
            visibilitySlider = null;
            renderingTab = null;
            fancyTab = null;
        }

        public override void Initialize()
        {
            renderingTab = new OpTab(this, "Rendering");
            fancyTab = new OpTab(this, "Fancy");
            this.Tabs = new[] { renderingTab, fancyTab };

            // Render Tab
            renderingTab.AddItems(new OpContainer(new Vector2(0, 0)));

            //render mode
            renderModeSelect = new OpRadioButtonGroup(renderMode);
            renderingTab.AddItems(renderModeSelect);
            renderModeSelect.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(89f, 520f){ description = "Classic. Unseen areas are completely blocked with a solid color. Hard. Recommended to reduce Tile Size."},
                new OpRadioButton(189f, 520f){ description = "Fast. Renders the level graphic again over the foreground layer. This blocks creatures and items efficiently, but has many unintended visual errors."},
                new OpRadioButton(289f, 520f){ description = "Fancy. Renders everything twice for the highest visual quality. Can selectively filter out specific objects."}
            });
            //disgusting reflect code. thank you bepinex.
            typeof(UIconfig).GetEvent("OnValueChanged").GetAddMethod().Invoke(renderModeSelect, new object[] { (OnValueChangeHandler)OnRenderModeChanged });

            //visibility slider
            int visibilityMin = (renderMode.Value == 1) ? 75 : 1;
            visibilitySlider = new OpSlider(visibility, new Vector2(50 + visibilityMin * 3, 440), 300 - visibilityMin * 3) { min = visibilityMin, max = 100, hideLabel = false, description = "Default: 80 - How visible unseen areas of the map are. Lower values fade into a solid color." };
            if (renderMode.Value != 0)
                renderingTab.AddItems(visibilitySlider);

            OpRadioButtonGroup visionSingleplayerSelect = new OpRadioButtonGroup(allowVisionUnconsciousSingleplayer);
            OpRadioButtonGroup visionMultiplayerSelect = new OpRadioButtonGroup(allowVisionUnconsciousMultiplayer);
            renderingTab.AddItems(visionSingleplayerSelect, visionMultiplayerSelect);
            visionSingleplayerSelect.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(69f, 50f){ description = "None. The player cannot see anything while unconscious or dead. Not even themself."},
                new OpRadioButton(149f, 50f){ description = "Self. Fancy mode only - otherwise defaults to Near. The player can only see themself while unconscious or dead."},
                new OpRadioButton(229f, 50f){ description = "Near. The player can only see a small area while unconscious or dead."},
                new OpRadioButton(309f, 50f){ description = "Far. Vision is unaffected even while unconscious or dead."}
            });
            visionMultiplayerSelect.SetButtons(new OpRadioButton[]
            {
                new OpRadioButton(69f, 20f){ description = "None. Players cannot see anything while unconscious or dead. This can prevent players from being seen entirely."},
                new OpRadioButton(149f, 20f){ description = "Self. Fancy mode only - otherwise defaults to Near. Players can only see themselves while unconscious or dead."},
                new OpRadioButton(229f, 20f){ description = "Near. Players can only see a small area while unconscious or dead."},
                new OpRadioButton(309f, 20f){ description = "Far. Vision is unaffected even while unconscious or dead."}
            });

            //the rest
            renderingTab.AddItems( //create an array of ui elements
                new OpLabel(new Vector2(365, 520), new Vector2(0, 30), "Render Mode", FLabelAlignment.Left, true),
                new OpLabel(new Vector2(50, 550), new Vector2(100, 20), "Classic", FLabelAlignment.Center),
                new OpLabel(new Vector2(150, 550), new Vector2(100, 20), "Fast", FLabelAlignment.Center),
                new OpLabel(new Vector2(250, 550), new Vector2(100, 20), "Fancy", FLabelAlignment.Center),
                new OpLabel(365, 446, "Unseen Area Visibility"),
                new OpSlider(brightness, new Vector2(50, 380), 300) { max = 100, hideLabel = false, description = "Default: 80 - Brightness of the unseen area color, which is based on the room's pallete. 0 is black." },
                new OpLabel(365, 386, "Unseen Area Brightness"),
                new OpSlider(tileSize, new Vector2(50, 320), 300) { min = 2, max = 10, hideLabel = false, description = "Default: 8 - Size of tiles when calculating line of sight. Low values improve visibility in tunnels and around corners. 10 is the actual size of tiles." },
                new OpLabel(365, 326, "Tile Size"),
                new OpSlider(viewRange, new Vector2(50, 260), 300) { min = 5, max = 100, hideLabel = false, description = "Default: 80 - Maximum radius in tiles around the player that can be visible. Recommended to reduce with more players." },
                new OpLabel(365, 266, "View Range"),
                new OpCheckBox(ditherUsesWorldPos, 309, 200) { description = "Default: false - Dither effect uses world position instead of screen position. Can cause awful flickering on some monitors." },
                new OpLabel(new Vector2(365, 200), new Vector2(0, 22), "Dither Uses World Position", FLabelAlignment.Left, false),
                new OpLabel(new Vector2(50, 120), new Vector2(300, 0), "Allow Vision While Unconscious", FLabelAlignment.Center, true),
                new OpLabel(new Vector2(40, 80), new Vector2(80, 20), "None", FLabelAlignment.Center),
                new OpLabel(new Vector2(120, 80), new Vector2(80, 20), "Self", FLabelAlignment.Center),
                new OpLabel(new Vector2(200, 80), new Vector2(80, 20), "Near", FLabelAlignment.Center),
                new OpLabel(new Vector2(280, 80), new Vector2(80, 20), "Far", FLabelAlignment.Center),
                new OpLabel(new Vector2(365, 50), new Vector2(0, 30), "Solo", FLabelAlignment.Left, true),
                new OpLabel(new Vector2(365, 20), new Vector2(0, 30), "Coop", FLabelAlignment.Left, true)
                );

            // Filter Tab
            fancyTab.AddItems(
                new OpContainer(new Vector2(0, 0)),
                new OpLabel(new Vector2(0, 550), new Vector2(600, 0), "Fancy Mode Settings", FLabelAlignment.Center, true),
                new OpLabel(new Vector2(50, 500), new Vector2(100, 0), "Plant-like", FLabelAlignment.Center),
                new OpCheckBox(showPolePlant, new Vector2(39, 450)) { description = "Default: true - Should Pole Plants be visible outside of the line of sight?" },
                new OpLabel(75, 450, "Pole Plants"),
                new OpCheckBox(showRot, new Vector2(39, 400)) { description = "Default: true - Should Rot be visible outside of the line of sight?" },
                new OpLabel(75, 400, "Rot"),
                new OpCheckBox(showWormGrass, new Vector2(39, 350)) { description = "Default: false - Should Worm Grass be visible outside of the line of sight?" },
                new OpLabel(75, 350, "Worm Grass"),
                new OpCheckBox(showMonsterKelp, new Vector2(39, 300)) { description = "Default: false - Should Monster Kelp be visible outside of the line of sight?" },
                new OpLabel(75, 300, "Monster Kelp"),
                new OpLabel(new Vector2(350, 500), new Vector2(100, 0), "Other", FLabelAlignment.Center),
                new OpCheckBox(showOverseer, new Vector2(339, 450)) { description = "Default: true - Should Overseers be visible outside of the line of sight?" },
                new OpLabel(375, 450, "Overseers"),
                new OpCheckBox(showInsect, new Vector2(339, 400)) { description = "Default: false - Should cosmetic insects be visible outside of the line of sight?" },
                new OpLabel(375, 400, "Insects"),
                new OpCheckBox(showShorcutEntrance, new Vector2(339, 350)) { description = "Default: true - Should shortcut entrances be visible outside of the line of sight?" },
                new OpLabel(375, 350, "Shortcut Entrances")
                );
        }

        public void OnRenderModeChanged(UIconfig config, string value, string oldValue)
        {
            if (int.Parse(value) == 1)
            {
                visibilitySlider.min = 75;
                visibilitySlider.pos = new Vector2(275, 440);
                visibilitySlider.size = new Vector2(75, 30);
                renderingTab.AddItems(visibilitySlider);
            }
            else if (int.Parse(value) == 2)
            {
                visibilitySlider.min = 1;
                visibilitySlider.pos = new Vector2(53, 440);
                visibilitySlider.size = new Vector2(297, 30);
                renderingTab.AddItems(visibilitySlider);
            }
            else
            {
                renderingTab.RemoveItems(visibilitySlider);
            }
        }

        //called only when configs are loaded or changed.
        public void UpdateConfigs()
        {
            //assign config values
            LOSController.renderMode = (LOSController.RenderMode)renderMode.Value;
            LOSController.visibility = visibility.Value / 100f;
            LOSController.brightness = brightness.Value / 100f;
            LOSController.tileSize = tileSize.Value;
            LOSController.viewRange = viewRange.Value * 20f;
            LOSController.ditherUsesWorldPos = ditherUsesWorldPos.Value;
            LOSController.gridSnap = ditherUsesWorldPos.Value ? 2 : 1;
            LOSController.allowVisionUnconsciousSingleplayer = allowVisionUnconsciousSingleplayer.Value;
            LOSController.allowVisionUnconsciousMultiplayer = allowVisionUnconsciousMultiplayer.Value;

            LOSController.fovShader = null;

            //update filter
            if (showOverseer.Value) LOSController.AddWhitelistedTypes(typeof(OverseerGraphics));
            else LOSController.RemoveWhitelistedTypes(typeof(OverseerGraphics));
            if (showInsect.Value) LOSController.AddWhitelistedTypes(typeof(CosmeticInsect));
            else LOSController.RemoveWhitelistedTypes(typeof(CosmeticInsect));
            //plantlike
            if (showPolePlant.Value) LOSController.AddWhitelistedTypes(typeof(PoleMimicGraphics));
            else LOSController.RemoveWhitelistedTypes(typeof(PoleMimicGraphics));
            if (showRot.Value) LOSController.AddWhitelistedTypes(typeof(DaddyCorruption));
            else LOSController.RemoveWhitelistedTypes(typeof(DaddyCorruption));
            if (showWormGrass.Value) LOSController.AddWhitelistedTypes(typeof(WormGrass.Worm));
            else LOSController.RemoveWhitelistedTypes(typeof(WormGrass.Worm));
            if (showMonsterKelp.Value) LOSController.AddWhitelistedTypes(typeof(TentaclePlantGraphics));
            else LOSController.RemoveWhitelistedTypes(typeof(TentaclePlantGraphics));
            LOSController.showShortcutEntrance = showShorcutEntrance.Value;
        }
    }
}
