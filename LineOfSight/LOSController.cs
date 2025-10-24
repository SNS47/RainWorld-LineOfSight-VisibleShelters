using RWCustom;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System.Security.Policy;
using System;
using System.Collections;
using System.Xml;
using MoreSlugcats;
using System.Linq;
using System.ComponentModel;
using JetBrains.Annotations;
using System.Linq.Expressions;

namespace LineOfSight
{
    public class LOSController : CosmeticSprite
    {
        internal static bool hackToDelayDrawingUntilAfterTheLevelMoves;

        public enum RenderMode
        {
            Classic,
            Fast,
            Fancy
        }

        //change this to enable the log
        public static bool DEBUG_LOG = false; 

        //config variables
        public static RenderMode renderMode;
        public static float visibility;
        public static float brightness;
        public static float tileSize;
        public static float viewRange;
        public static bool ditherUsesWorldPos;
        public static int gridSnap;
        public static int allowVisionUnconsciousSingleplayer;
        public static int allowVisionUnconsciousMultiplayer;
        public int allowVisionWhileUnconscious;

        //Hide sprites in fancy mode
        private static HashSet<Type> blacklistedTypes = new HashSet<Type>();
        private static HashSet<Type> whitelistedTypes = new HashSet<Type>();
        private static HashSet<Type> generatedTypeBlacklist;
        public static bool showShortcutEntrance;

        //shaders
        public static FShader fovShader;
        public static RenderTexture renderTexture;
        public static FAtlasElement renderTextureElement;

        public static void AddBlacklistedTypes(params Type[] drawableTypes)
        {
            foreach (Type drawableType in drawableTypes)
                if (blacklistedTypes.Add(drawableType))
                    generatedTypeBlacklist = null;
        }

        public static void RemoveBlacklistedTypes(params Type[] drawableTypes)
        {
            foreach (Type drawableType in drawableTypes)
                if (blacklistedTypes.Remove(drawableType))
                    generatedTypeBlacklist = null;
        }

        public static void AddWhitelistedTypes(params Type[] drawableTypes)
        {
            foreach (Type drawableType in drawableTypes)
                if (whitelistedTypes.Add(drawableType))
                    generatedTypeBlacklist = null;
        }

        public static void RemoveWhitelistedTypes(params Type[] drawableTypes)
        {
            foreach (Type drawableType in drawableTypes)
                if (whitelistedTypes.Remove(drawableType))
                    generatedTypeBlacklist = null;
        }

        private static void GenerateTypeBlacklist()
        {
            generatedTypeBlacklist = new HashSet<Type>();
            List<Type> allWhitelistedTypes = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                foreach (Type type in types)
                {
                    if (type == null) continue; // I guess we have null types now

                    foreach (Type blacklistedType in blacklistedTypes)
                        if (type.IsSubclassOf(blacklistedType) || type == blacklistedType)
                            generatedTypeBlacklist.Add(type);
                    foreach (Type whitelistedType in whitelistedTypes)
                        if (type.IsSubclassOf(whitelistedType) || type == whitelistedType)
                            allWhitelistedTypes.Add(type);
                }
            }
            foreach (Type drawableType in allWhitelistedTypes)
                generatedTypeBlacklist.Remove(drawableType);
        }

        //Sprites
        public bool hideAllSprites = false;
        public IntVector2 resolution;

        //FOV calculation
        private int playerCount;
        private class PlayerFovInfo
        {
            public Vector2? eyePos;
            public Vector2? lastEyePos;
            public Vector2? bodyPos;
            public Vector2? lastBodyPos;
            public float nearBlockAlpha;
            public float lastNearBlockAlpha;
            public float screenBlockAlpha;
            public float lastScreenBlockAlpha;
            public PlayerFovInfo()
            {
                Clear();
            }
            public void Clear()
            {
                eyePos = null; lastEyePos = null;
                bodyPos = null; lastBodyPos = null;
                screenBlockAlpha = 1f; lastScreenBlockAlpha = 1f;
                nearBlockAlpha = 0f; lastNearBlockAlpha = 0f;
            }
        }
        private PlayerFovInfo[] playerInfo;
        
        public enum MappingState
        {
            FindingEdges,
            DuplicatingPoints,
            Done
        }

        //Mapping tiles
        public MappingState state;
        private Room.Tile[,] tiles;
        private int _x;
        private int _y;
        public List<Vector2> corners = new List<Vector2>();
        public List<int> edges = new List<int>();

        public LOSController(Room room)
        {
            _x = 0;
            _y = 0;
            tiles = room.Tiles;

            resolution = new IntVector2(Futile.screen.renderTexture.width, Futile.screen.renderTexture.height);

            //Initialize fovShader
            if (fovShader == null)
                switch (renderMode)
                {
                    case RenderMode.Classic:
                        fovShader = FShader.CreateShader("RenderOutOfFOV", Assets.RenderOutOfFOV);
                        Shader.SetGlobalFloat("_los_visibility", 0f);
                        break;
                    case RenderMode.Fast: 
                        fovShader = FShader.CreateShader("LevelOutOfFOV", Assets.LevelOutOfFOV); 
                        break;
                    case RenderMode.Fancy:
                        fovShader = FShader.CreateShader("RenderOutOfFOV", Assets.RenderOutOfFOV);
                        Shader.SetGlobalFloat("_los_visibility", visibility);
                        break;
                }

            //Initialize Fancy mode
            if(renderMode == RenderMode.Fancy)
            {
                if (renderTexture != null
                    && (renderTexture.width !=  resolution.x || renderTexture.height != resolution.y))
                {
                    renderTexture = null;
                    Futile.atlasManager.ActuallyUnloadAtlasOrImage("LOS_RenderTexture");
                    renderTextureElement = null;
                }
                if (renderTexture == null)
                {
                    renderTexture = new RenderTexture(Futile.screen.renderTexture.descriptor);
                    renderTexture.filterMode = FilterMode.Point;
                    Futile.atlasManager.LoadAtlasFromTexture("LOS_RenderTexture", renderTexture, false);
                    renderTextureElement = Futile.atlasManager.GetElementWithName("LOS_RenderTexture");
                }
            }

            if (room.game.IsStorySession)
                playerCount = room.game.StoryPlayerCount;
            else if (room.game.IsArenaSession)
                playerCount = room.game.GetArenaGameSession.arenaSitting.players.Count;

            playerInfo = new PlayerFovInfo[playerCount];
            for (int i = 0; i < playerCount; i++)
                playerInfo[i] = new PlayerFovInfo();

            allowVisionWhileUnconscious = (playerCount > 1) ? allowVisionUnconsciousMultiplayer : allowVisionUnconsciousSingleplayer;
            if (allowVisionWhileUnconscious == 1 && renderMode != RenderMode.Fancy)
                allowVisionWhileUnconscious = 2;
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            base.InitiateSprites(sLeaser, rCam);

            while (state != MappingState.Done)
                UpdateMapper(int.MaxValue);

            sLeaser.sprites = new FSprite[playerCount * 4 + 2];

            FSprite preBlocker = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            preBlocker.shader = Assets.PreBlockerStencil;
            preBlocker.width = resolution.x;
            preBlocker.height = resolution.y;
            preBlocker.SetPosition(0.5f, -0.5f);
            sLeaser.sprites[0] = preBlocker;

            // Generate blocker meshes
            for (int p =  0; p < playerCount; p++)
            {
                FSprite[] blockers = CreatePlayerBlockers();
                for (int s = 0; s < blockers.Length; s++)
                    sLeaser.sprites[p * 4 + s + 1] = blockers[s];
            }

            // The actual sprite which will block the screen based on the stencil buffer
            FSprite finalBlocker = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            finalBlocker.shader = fovShader;
            switch (renderMode)
            {
                case RenderMode.Classic:
                    finalBlocker.width = resolution.x;
                    finalBlocker.height = resolution.y;
                    break;
                case RenderMode.Fancy:
                    finalBlocker.element = renderTextureElement;
                    break;
            }
            finalBlocker.SetPosition(0.5f, -0.5f);
            sLeaser.sprites[sLeaser.sprites.Length - 1] = finalBlocker;

            AddToContainer(sLeaser, rCam, null);
        }

        private FSprite[] CreatePlayerBlockers()
        {
            FSprite[] sprites = new FSprite[4];

            // Block outside of FoV
            TriangleMesh.Triangle[] fovTris = new TriangleMesh.Triangle[edges.Count];
            for (int i = 0, len = edges.Count / 2; i < len; i++)
            {
                int o = i * 2;
                fovTris[o] = new TriangleMesh.Triangle(edges[o], edges[o + 1], edges[o] + corners.Count / 2);
                fovTris[o + 1] = new TriangleMesh.Triangle(edges[o + 1], edges[o + 1] + corners.Count / 2, edges[o] + corners.Count / 2);
            }

            TriangleMesh fovBlocker = new TriangleMesh("Futile_White", fovTris, false, true);
            fovBlocker.shader = Assets.ViewBlockerStencil;
            corners.CopyTo(fovBlocker.vertices);
            fovBlocker.Refresh();
            sprites[0] = fovBlocker;

            // Near unblocker
            TriangleMesh nearBlocker = GenerateCircularBlocker(Assets.UnblockerStencil, true, 30, 20f, 40f);
            for (int v = 30; v < 60; v++)
                nearBlocker.verticeColors[v] = Color.clear;
            sprites[1] = nearBlocker;

            // Far blocker
            TriangleMesh farBlocker = GenerateCircularBlocker(Assets.ViewBlockerStencil, true, 120, viewRange * 0.6f, viewRange, 50000f);
            for (int v = 0; v < 120; v++)
                farBlocker.verticeColors[v] = Color.clear;
            farBlocker.verticeColors[360] = Color.clear;
            sprites[2] = farBlocker;

            // Full screen blocker
            FSprite screenBlocker = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            screenBlocker.shader = Assets.ScreenBlockerStencil;
            screenBlocker.width = resolution.x;
            screenBlocker.height = resolution.y;
            screenBlocker.SetPosition(0.5f, -0.5f);
            sprites[3] = screenBlocker;

            return sprites;
        }

        private TriangleMesh GenerateCircularBlocker(FShader shader, bool filled, int points, params float[] ranges)
        {
            Array.Sort(ranges);
            int rings = ranges.Length;
            int triCount = points * (rings - 1) * 2;
            triCount = filled ? triCount + points : triCount;

            TriangleMesh.Triangle[] tris = new TriangleMesh.Triangle[triCount];
            for (int r = 0; r < rings - 1; r++)
                for (int p = 0; p < points; p++)
                {
                    int v1 = r * points + p;
                    int v2 = r * points + (p + 1) % points;
                    int v3 = v1 + points;
                    int v4 = v2 + points;
                    tris[(r * points + p) * 2] = new TriangleMesh.Triangle(v1, v2, v3);
                    tris[(r * points + p) * 2 + 1] = new TriangleMesh.Triangle(v2, v3, v4);
                }

            if (filled)
                for (int p = 0; p < points; p++)
                {
                    int v1 = p;
                    int v2 = (p + 1) % points;
                    int v3 = rings * points;
                    tris[points * (rings - 1) * 2 + p] = new TriangleMesh.Triangle(v1, v2, v3);
                }

            TriangleMesh circleBlocker = new TriangleMesh("Futile_White", tris, true, true);
            for (int r = 0; r < rings; r++)
                for (int p = 0; p < points; p++)
                    circleBlocker.vertices[r * points + p] = Custom.DegToVec(p * 360f / points) * ranges[r];
            if (filled)
                circleBlocker.vertices[rings * points] = new Vector2(0f, 0f);

            circleBlocker.shader = shader;
            circleBlocker.Refresh();
            //Debug.Log("Generated circle mesh with " + circleBlocker.vertices.Length + " vertices and " + tris.Length + " triangles.");
            return circleBlocker;
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            FSprite finalBlocker = sLeaser.sprites[sLeaser.sprites.Length - 1];
            if (renderMode == RenderMode.Fast)
                finalBlocker.color = new Color(1f - visibility, 0, 0, 1f);
            else
                finalBlocker.color = Color.Lerp(Color.black, palette.blackColor, brightness);
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
        {
            FContainer container = (renderMode == RenderMode.Fast) ? rCam.ReturnFContainer("ForegroundLights") : rCam.ReturnFContainer("Bloom");

            foreach (FSprite sprite in sLeaser.sprites)
                container.AddChild(sprite);
        }

        public override void Update(bool eu)
        {
            base.Update(eu);
        }

        public void LateUpdate()
        {
            if (room.game.IsArenaSession && !room.game.GetArenaGameSession.playersSpawned
                || room.game.wasAnArtificerDream
                || room.abstractRoom.name.Equals("SB_L01"))
            {
                hideAllSprites = true;
                return;
            }
            hideAllSprites = false;

            // Do not try to access shortcuts when the room is not ready for AI
            if (!room.readyForAI)
                return;

            // Map edges to display quads
            if (state != MappingState.Done)
                UpdateMapper(300);

            for (int p = 0; p < playerCount; p++)
            {
                Player ply = room.game.Players.Count > p ? room.game.Players[p].realizedCreature as Player : null;
                ShortcutHandler.ShortCutVessel plyVessel = ply == null ? null : room.game.shortcuts.transportVessels.Find(x => x.creature == ply);
                PlayerFovInfo fovInfo = playerInfo[p];

                fovInfo.lastEyePos = fovInfo.eyePos.HasValue ? fovInfo.eyePos.Value : null;
                fovInfo.lastBodyPos = fovInfo.bodyPos.HasValue ? fovInfo.bodyPos.Value : null;
                fovInfo.lastScreenBlockAlpha = fovInfo.screenBlockAlpha;
                fovInfo.lastNearBlockAlpha = fovInfo.nearBlockAlpha;

                // Player null or not in room
                if (ply == null || (ply.room != room && plyVessel?.room.realizedRoom != room))
                {
                    if (fovInfo.screenBlockAlpha == 1f && fovInfo.nearBlockAlpha == 0f || !fovInfo.eyePos.HasValue)
                        fovInfo.Clear();
                    else
                    {
                        fovInfo.screenBlockAlpha = Mathf.Clamp01(fovInfo.screenBlockAlpha + 0.05f);
                        fovInfo.nearBlockAlpha = Mathf.Clamp01(fovInfo.nearBlockAlpha - 0.05f);
                    }
                    continue;
                }

                // Update eye position and screenblock alpha
                BodyChunk headChunk = ply.bodyChunks[0];
                if (headChunk != null)
                    fovInfo.eyePos = headChunk.pos;

                BodyChunk TorsoChunk = ply.bodyChunks[1];
                if (headChunk != null)
                    fovInfo.bodyPos = TorsoChunk.pos;

                if (plyVessel != null)
                {
                    fovInfo.eyePos = Vector2.Lerp(plyVessel.lastPos.ToVector2(), plyVessel.pos.ToVector2(), (room.game.updateShortCut + 1) / 3f) * 20f + new Vector2(10f, 10f);
                    fovInfo.bodyPos = fovInfo.eyePos.Value;
                }

                if (!fovInfo.eyePos.HasValue)
                    continue;

                bool justEntered = (!fovInfo.lastEyePos.HasValue) ? true : false;

                // Test for blindness
                if (ply.Consious)
                {
                    if ((ply.Sleeping || ply.sleepCurlUp == 1f)) // sleeping
                    {
                        if (allowVisionWhileUnconscious != 3)
                            fovInfo.screenBlockAlpha = justEntered ? 1f : Mathf.Clamp01(fovInfo.screenBlockAlpha + 0.05f);
                        else
                            fovInfo.screenBlockAlpha = justEntered ? 0f : Mathf.Clamp01(fovInfo.screenBlockAlpha - 0.1f);

                        if (allowVisionWhileUnconscious == 1)
                            fovInfo.nearBlockAlpha = justEntered ? 0f : Mathf.Clamp01(fovInfo.nearBlockAlpha - 0.05f);
                        else
                            fovInfo.nearBlockAlpha = justEntered ? 1f : Mathf.Clamp01(fovInfo.nearBlockAlpha + 0.1f);
                    }
                    else // conscious and awake
                    {
                        fovInfo.screenBlockAlpha = justEntered ? 0f : Mathf.Clamp01(fovInfo.screenBlockAlpha - 0.1f);
                        fovInfo.nearBlockAlpha = justEntered ? 1f : Mathf.Clamp01(fovInfo.nearBlockAlpha + 0.1f);
                    }
                }
                else // unconscious
                {
                    if (allowVisionWhileUnconscious == 3) //far vision allowed when unconscious
                        fovInfo.screenBlockAlpha = justEntered ? 0f : Mathf.Clamp01(fovInfo.screenBlockAlpha - 0.1f);
                    else
                        fovInfo.screenBlockAlpha = justEntered ? 1f : Mathf.Clamp01(fovInfo.screenBlockAlpha + 0.1f);
                    if (allowVisionWhileUnconscious >= 2 || allowVisionWhileUnconscious == 1 && plyVessel != null) //near vision allowed when unconscious
                        fovInfo.nearBlockAlpha = justEntered ? 1f : Mathf.Clamp01(fovInfo.nearBlockAlpha + 0.1f);
                    else
                        fovInfo.nearBlockAlpha = justEntered ? 0f : Mathf.Clamp01(fovInfo.nearBlockAlpha - 0.1f);
                }

                fovInfo.lastEyePos = fovInfo.lastEyePos.HasValue ? fovInfo.lastEyePos.Value : (fovInfo.eyePos.HasValue ? fovInfo.eyePos.Value : null);
                fovInfo.lastBodyPos = fovInfo.lastBodyPos.HasValue ? fovInfo.lastBodyPos.Value : (fovInfo.bodyPos.HasValue ? fovInfo.bodyPos.Value : null);
            }
        }

        private Vector2 _lastCamPos;
        private List<FSprite> duplicateShelterSprites = new List<FSprite>();
        private bool shelterSpritesCreated = false;

        private void CreateDuplicateShelterSprites(RoomCamera rCam, RoomCamera.SpriteLeaser sLeaser)
        {
            if (shelterSpritesCreated) return;
            
            FContainer container = (renderMode == RenderMode.Fast) ? rCam.ReturnFContainer("ForegroundLights") : rCam.ReturnFContainer("Bloom");
            
            for (int i = 0; i < rCam.shortcutGraphics.entranceSprites.GetLength(0); i++)
            {
                if (rCam.shortcutGraphics.entranceSprites[i, 0] != null)
                {
                    bool isShelter =
                        rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutAShelter"
                        || rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutShelter";
                    
                    if (isShelter)
                    {
                        FSprite original = rCam.shortcutGraphics.entranceSprites[i, 0];
                        FSprite duplicate = new FSprite(original.element.name);
                        
                        // Copy all properties from original
                        duplicate.SetPosition(original.GetPosition());
                        duplicate.rotation = original.rotation;
                        duplicate.scaleX = original.scaleX;
                        duplicate.scaleY = original.scaleY;
                        duplicate.color = original.color;
                        duplicate.alpha = original.alpha;
                        duplicate.anchorX = original.anchorX;
                        duplicate.anchorY = original.anchorY;
                        
                        // Add to container
                        container.AddChild(duplicate);
                        duplicateShelterSprites.Add(duplicate);
                    }
                }
            }
            
            shelterSpritesCreated = true;
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (!hackToDelayDrawingUntilAfterTheLevelMoves)
            {
                _lastCamPos = camPos;
                return;
            }

            if (sLeaser == null || rCam == null) return;
            if (room == null || room.game == null || sLeaser.sprites == null) return;

            foreach (FSprite sprite in sLeaser.sprites)
                sprite.isVisible = false;
            if (hideAllSprites)
            {
                // Hide duplicates too
                foreach (FSprite sprite in duplicateShelterSprites)
                    sprite.isVisible = false;
                return;
            }

            //Fancy out of fov render
            if (renderMode == RenderMode.Fancy)
            {
                DisableAllSprites(rCam);

                Futile.instance.camera.targetTexture = renderTexture;
                Futile.stage.Redraw(false, false);
                Futile.stage.LateUpdate();
                Futile.instance.camera.Render();
                Futile.instance.camera.targetTexture = Futile.screen.renderTexture;

                EnableAllSprites(rCam);
            }

            IntVector2 intCamPos = SnapToGrid(_lastCamPos);
            sLeaser.sprites[0].isVisible = true;

            //update blockers for each player
            for (int p = 0; p < playerCount; p++)
            {
                TriangleMesh fovBlocker = sLeaser.sprites[p * 4 + 1] as TriangleMesh;
                TriangleMesh nearBlocker = sLeaser.sprites[p * 4 + 2] as TriangleMesh;
                TriangleMesh farBlocker = (TriangleMesh)sLeaser.sprites[p * 4 + 3];
                FSprite screenBlocker = sLeaser.sprites[p * 4 + 4];
                PlayerFovInfo fovInfo = playerInfo[p];

                // Eye position
                Vector2 eye = Vector2.positiveInfinity;
                if (fovInfo.eyePos.HasValue)
                    eye = Vector2.Lerp(fovInfo.lastEyePos.Value, fovInfo.eyePos.Value, timeStacker);
                if (Vector2.Distance(eye, _lastCamPos + resolution.ToVector2() / 2f) > 10000f) // skip render if too far away or eye position has no value
                    continue;

                // Update FOV blocker mesh
                Vector2 pos = Vector2.zero;
                for (int i = 0, len = corners.Count / 2; i < len; i++)
                {
                    pos.Set(corners[i].x - eye.x, corners[i].y - eye.y);
                    pos.Normalize();
                    fovBlocker.vertices[i].Set(corners[i].x, corners[i].y);
                    fovBlocker.vertices[i + len].Set(pos.x * viewRange * 10f + eye.x, pos.y * viewRange * 10f + eye.y);
                }
                fovBlocker.Refresh();
                fovBlocker.x = -_lastCamPos.x;
                fovBlocker.y = -_lastCamPos.y;
                fovBlocker.isVisible = true;

                // Block the screen when inside a wall
                if (room.GetTile(room.GetTilePosition(eye)).Solid)
                {
                    fovInfo.lastScreenBlockAlpha = 1f;
                    fovInfo.screenBlockAlpha = 1f;
                    fovBlocker.isVisible = false;
                }

                float screenBlockAplha = Mathf.Lerp(fovInfo.lastScreenBlockAlpha, fovInfo.screenBlockAlpha, timeStacker);
                float nearBlockAlpha = Mathf.Lerp(fovInfo.lastNearBlockAlpha, fovInfo.nearBlockAlpha, timeStacker);
                float nearBlockFade = 1 - screenBlockAplha;

                // Update Near and Far Blocker
                if (nearBlockAlpha > 0f)
                {
                    Vector2 center = Vector2.Lerp(Vector2.Lerp(fovInfo.lastBodyPos.Value, fovInfo.bodyPos.Value, timeStacker), eye, 0.48f);
                    IntVector2 nearBlockerPos = SnapToGrid(center) - intCamPos;
                    for (int v = 0; v < 30; v++)
                        nearBlocker.verticeColors[v].a = nearBlockAlpha;
                    nearBlocker.verticeColors[60].a = nearBlockAlpha;
                    for (int v = 30; v < 60; v++)
                        nearBlocker.verticeColors[v].a = nearBlockFade;
                    nearBlocker.SetPosition(nearBlockerPos.ToVector2());
                    nearBlocker.isVisible = true;
                }

                IntVector2 farBlockerPos = SnapToGrid(eye) - intCamPos;
                for (int v = 0; v < 120; v++)
                    farBlocker.verticeColors[v].a = screenBlockAplha;
                farBlocker.verticeColors[360].a = screenBlockAplha;
                farBlocker.SetPosition(farBlockerPos.ToVector2());
                farBlocker.isVisible = true;

                // Move the screenblock
                screenBlocker.alpha = Mathf.Lerp(fovInfo.lastScreenBlockAlpha, fovInfo.screenBlockAlpha, timeStacker);
                screenBlocker.isVisible = true;
            }

            FSprite finalBlocker = sLeaser.sprites[sLeaser.sprites.Length - 1];
            if (renderMode == RenderMode.Fast)
            {
                finalBlocker.scaleX = rCam.levelGraphic.scaleX;
                finalBlocker.scaleY = rCam.levelGraphic.scaleY;
                finalBlocker.SetPosition(rCam.levelGraphic.GetPosition());
                finalBlocker.element = rCam.levelGraphic.element;
            }
            finalBlocker.isVisible = true;

            // Keep on top
            if (finalBlocker.container.GetChildAt(finalBlocker.container.GetChildCount() - 1) != finalBlocker)
                foreach (FSprite sprite in sLeaser.sprites)
                    sprite.MoveToFront();

            // CREATE AND UPDATE DUPLICATE SHELTER SPRITES (for Classic mode)
            if (renderMode == RenderMode.Classic)
            {
                if (!shelterSpritesCreated)
                    CreateDuplicateShelterSprites(rCam, sLeaser);
                
                // Update duplicate positions to match originals and make them visible
                int duplicateIndex = 0;
                for (int i = 0; i < rCam.shortcutGraphics.entranceSprites.GetLength(0); i++)
                {
                    if (rCam.shortcutGraphics.entranceSprites[i, 0] != null)
                    {
                        bool isShelter =
                            rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutAShelter"
                            || rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutShelter";
                        
                        if (isShelter && duplicateIndex < duplicateShelterSprites.Count)
                        {
                            FSprite original = rCam.shortcutGraphics.entranceSprites[i, 0];
                            FSprite duplicate = duplicateShelterSprites[duplicateIndex];
                            
                            // Update position and properties from original
                            duplicate.SetPosition(original.GetPosition());
                            duplicate.rotation = original.rotation;
                            duplicate.color = original.color;
                            duplicate.alpha = original.alpha;
                            duplicate.isVisible = true;
                            
                            // Make sure it's on top
                            duplicate.MoveToFront();
                            
                            duplicateIndex++;
                        }
                    }
                }
            }
            else
            {
                // Hide duplicates in other modes
                foreach (FSprite sprite in duplicateShelterSprites)
                    sprite.isVisible = false;
            }

            if (ditherUsesWorldPos)
                Shader.SetGlobalVector("_los_camPos", intCamPos.ToVector2());
            else
                Shader.SetGlobalVector("_los_camPos", Vector2.zero);

            base.DrawSprites(sLeaser, rCam, timeStacker, _lastCamPos);
        }

        private IntVector2 SnapToGrid(Vector2 vec)
        {
            IntVector2 result = new IntVector2(Mathf.RoundToInt(vec.x), Mathf.RoundToInt(vec.y));
            result.x = result.x - result.x % gridSnap; result.y = result.y - result.y % gridSnap;
            return result;
        }
        
        private List<FNode> nodesHidden = new List<FNode>();
        private Dictionary<FSprite,Color> shortcutColors = new Dictionary<FSprite, Color>();

        private void DisableAllSprites(RoomCamera rCam)
        {
            //generate type set blacklist
            if (generatedTypeBlacklist == null)
                GenerateTypeBlacklist();

            //hide blacklisted Idrawable types
            foreach (RoomCamera.SpriteLeaser sLeaser in rCam.spriteLeasers)
                if (ShouldHideDrawable(sLeaser.drawableObject))
                    foreach (FSprite sprite in sLeaser.sprites)
                        DisableNode(sprite);

            //logs each object type and whether or not it is affected by LOS
            if (DEBUG_LOG)
                foreach (RoomCamera.SpriteLeaser sLeaser in rCam.spriteLeasers)
                    if (!logTypes.Contains(sLeaser.drawableObject.GetType()))
                    {
                        logTypes.Add(sLeaser.drawableObject.GetType());
                        if (generatedTypeBlacklist.Contains(sLeaser.drawableObject.GetType()))
                            Debug.Log("[Line Of Sight] blacklisted type " + sLeaser.drawableObject.GetType());
                        else
                            Debug.Log("[Line Of Sight] whitelisted type " + sLeaser.drawableObject.GetType());
                    }

            
            //temporary code to hide shortcut sprites
            shortcutColors.Clear();
            foreach (FSprite sprite in rCam.shortcutGraphics.sprites.Values)
            {
                shortcutColors.Add(sprite, sprite.color);
                sprite.color = rCam.shortcutGraphics.ColorFromLightness(0f);
            }
            
            //hide shortcut entrances
            if (!showShortcutEntrance)
                for (int i = 0; i < rCam.shortcutGraphics.entranceSprites.GetLength(0); i++)
                    if (rCam.shortcutGraphics.entranceSprites[i, 0] != null)
                    {
                        bool isShelter =
                            rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutAShelter"
                            || rCam.shortcutGraphics.entranceSprites[i, 0].element.name == "ShortcutShelter";
                        if (!isShelter)
                        {
                            DisableNode(rCam.shortcutGraphics.entranceSprites[i, 0]);
                            DisableNode(rCam.shortcutGraphics.entranceSprites[i, 1]);
                        }
                    }

            //Hide HUD
            foreach (FNode node in rCam.ReturnFContainer("HUD")._childNodes)
                DisableNode(node);
            foreach (FNode node in rCam.ReturnFContainer("HUD2")._childNodes)
                DisableNode(node);
        }

        private static HashSet<Type> logTypes = new HashSet<Type>();
        private bool ShouldHideDrawable(object drawable)
        {
            if (typeof(LightSource).IsInstanceOfType(drawable) && (drawable as LightSource).tiedToObject != null) // is a light source attached to something
            {
                drawable = (drawable as LightSource).tiedToObject;
                if (typeof(PhysicalObject).IsInstanceOfType(drawable) && (drawable as PhysicalObject).graphicsModule != null) // the attatched object has a graphics module
                    drawable = (drawable as PhysicalObject).graphicsModule;
            }
            
            if (typeof(Lantern).IsInstanceOfType(drawable) && (drawable as Lantern).stick != null) //ignore lanterns that are part of the level
                return false;
            if (typeof(DataPearl).IsInstanceOfType(drawable) && DataPearl.PearlIsNotMisc((drawable as DataPearl).AbstractPearl.dataPearlType)) //unique pearls
                return false;

            if (generatedTypeBlacklist.Contains(drawable.GetType())) //if drawable is a type we want to hide
                return true;
            if ((typeof(PlayerGraphics).IsInstanceOfType(drawable) && ((drawable as PlayerGraphics).player.isSlugpup ? false : allowVisionWhileUnconscious == 0))) //if player and a slugpup or vision while unconscious is none)
                return true;
            
            return false;
        }

        private void DisableNode(FNode node)
        {
            if (node != null && node.isVisible)
            {
                nodesHidden.Add(node);
                node.isVisible = false;
            }
        }

        private void EnableAllSprites(RoomCamera rCam)
        {
            foreach (FNode node in nodesHidden)
                node.isVisible = true;
            nodesHidden.Clear();

            foreach (KeyValuePair<FSprite, Color> pair in shortcutColors)
                pair.Key.color = pair.Value;
        }

        private static Matrix ROTATE_0 = new Matrix(1f, 0f, 0f, 1f);
        private static Matrix ROTATE_90 = new Matrix(0f, 1f, -1f, 0f);
        private static Matrix ROTATE_180 = new Matrix(-1f, 0f, 0f, -1f);
        private static Matrix ROTATE_270 = new Matrix(0f, -1f, 1f, 0f);
        
        private enum Direction
        {
            Up,
            UpRight,
            Right,
            DownRight,
            Down,
            DownLeft,
            Left,
            UpLeft
        }

        public void UpdateMapper(int iterations)
        {
            for (int i = 0; i < iterations; i++)
            {
                switch (state)
                {
                    case MappingState.FindingEdges:
                        {
                            Room.Tile tile = tiles[_x, _y];
                            Room.Tile.TerrainType terrain = tile.Terrain;
                            Room.SlopeDirection slope = (terrain == Room.Tile.TerrainType.Slope) ? room.IdentifySlope(_x, _y) : Room.SlopeDirection.Broken;

                            if (slope != Room.SlopeDirection.Broken) AddSlopeEdge(_x, _y, slope);
                            if (terrain == Room.Tile.TerrainType.Solid)
                            {
                                if(tileSize == 10f) //old edge detection
                                {
                                    if (HasEdge(_x, _y, Direction.Left) && !HasEdge(_x - 1, _y, Direction.Right)) AddEdge(_x, _y, Direction.Left);
                                    if (HasEdge(_x, _y, Direction.Down) && !HasEdge(_x, _y - 1, Direction.Up)) AddEdge(_x, _y, Direction.Down);
                                    if (HasEdge(_x, _y, Direction.Right) && !HasEdge(_x + 1, _y, Direction.Left)) AddEdge(_x, _y, Direction.Right);
                                    if (HasEdge(_x, _y, Direction.Up) && !HasEdge(_x, _y + 1, Direction.Down)) AddEdge(_x, _y, Direction.Up);
                                }
                                else if (tileSize < 10f) //new edge detection
                                {
                                    CalculateEdge(new Vector2Int(_x, _y), 0, ROTATE_0);
                                    CalculateEdge(new Vector2Int(_x, _y), 1, ROTATE_90);
                                    CalculateEdge(new Vector2Int(_x, _y), 2, ROTATE_180);
                                    CalculateEdge(new Vector2Int(_x, _y), 3, ROTATE_270);
                                }
                            }
                            _x++;
                            if (_x >= room.TileWidth)
                            {
                                _x = 0;
                                _y++;
                                if (_y >= room.TileHeight)
                                {
                                    _y = corners.Count;
                                    state = MappingState.DuplicatingPoints;
                                }
                            }
                        }
                        break;
                    case MappingState.DuplicatingPoints:
                        {
                            corners.Add(corners[_x]);
                            _x++;
                            if (_x >= _y)
                            {
                                state = MappingState.Done;
                                _x = 0;
                                _y = 0;
                            }
                        }
                        break;
                    case MappingState.Done:
                        return;
                }
            }
        }

        private bool HasEdge(int x, int y, Direction dir)
        {
            Room.Tile tile = room.GetTile(x, y);
            Room.Tile.TerrainType terrain = tile.Terrain;
            Room.SlopeDirection slope = (terrain == Room.Tile.TerrainType.Slope) ? room.IdentifySlope(x, y) : Room.SlopeDirection.Broken;

            if (terrain == Room.Tile.TerrainType.Solid) return true;
            if (terrain == Room.Tile.TerrainType.Air ||
                terrain == Room.Tile.TerrainType.ShortcutEntrance ||
                terrain == Room.Tile.TerrainType.Floor) return false;
            switch (dir)
            {
                case Direction.Up:
                    return slope == Room.SlopeDirection.DownRight || slope == Room.SlopeDirection.DownLeft;
                case Direction.Right:
                    return slope == Room.SlopeDirection.UpLeft || slope == Room.SlopeDirection.DownLeft;
                case Direction.Down:
                    return slope == Room.SlopeDirection.UpRight || slope == Room.SlopeDirection.UpLeft;
                case Direction.Left:
                    return slope == Room.SlopeDirection.DownRight || slope == Room.SlopeDirection.UpRight;
            }
            return false;
        }

        private int AddCorner(Vector2 pos)
        {
            int ind = corners.IndexOf(pos);
            if (ind == -1)
            {
                corners.Add(pos);
                ind = corners.Count - 1;
            }
            return ind;
        }

        private void AddEdge(int x, int y, Direction dir)
        {
            Vector2 mid = room.MiddleOfTile(x, y);
            int ind1 = -1;
            int ind2 = -1;
            switch (dir)
            {
                case Direction.Up:
                    ind1 = AddCorner(new Vector2(mid.x - 10f, mid.y + 10f));
                    ind2 = AddCorner(new Vector2(mid.x + 10f, mid.y + 10f));
                    break;
                case Direction.Right:
                    ind1 = AddCorner(new Vector2(mid.x + 10f, mid.y + 10f));
                    ind2 = AddCorner(new Vector2(mid.x + 10f, mid.y - 10f));
                    break;
                case Direction.Down:
                    ind1 = AddCorner(new Vector2(mid.x + 10f, mid.y - 10f));
                    ind2 = AddCorner(new Vector2(mid.x - 10f, mid.y - 10f));
                    break;
                case Direction.Left:
                    ind1 = AddCorner(new Vector2(mid.x - 10f, mid.y - 10f));
                    ind2 = AddCorner(new Vector2(mid.x - 10f, mid.y + 10f));
                    break;
            }
            edges.Add(ind1);
            edges.Add(ind2);
        }

        private void AddSlopeEdge(int x, int y, Room.SlopeDirection dir)
        {
            //Room.SlopeDirection dir = room.IdentifySlope(x, y);
            Vector2 vector = room.MiddleOfTile(x, y);
            int item1 = -1;
            int item2 = -1;
            switch ((int)dir)
            {
                case 0: //upleft
                    item1 = AddCorner(new Vector2(vector.x - tileSize, vector.y - 10f));
                    item2 = AddCorner(new Vector2(vector.x + 10f, vector.y + tileSize));
                    break;
                case 1: //upright
                    item2 = AddCorner(new Vector2(vector.x + tileSize, vector.y - 10f));
                    item1 = AddCorner(new Vector2(vector.x - 10f, vector.y + tileSize));
                    break;
                case 2: //downleft
                    item1 = AddCorner(new Vector2(vector.x + 10f, vector.y - tileSize));
                    item2 = AddCorner(new Vector2(vector.x - tileSize, vector.y + 10f));
                    break;
                case 3: //downright
                    item2 = AddCorner(new Vector2(vector.x - 10f, vector.y - tileSize));
                    item1 = AddCorner(new Vector2(vector.x + tileSize, vector.y + 10f));
                    break;
            }
            edges.Add(item1);
            edges.Add(item2);
        }

        private void CalculateEdge(Vector2Int tile, int phase, Matrix rotationMatrix)
        {
            // get the necessary tiles for the calculation
            Vector2Int leftTile = tile + rotationMatrix.Transform(new Vector2Int(-1, 0));
            Vector2Int topLeftTile = tile + rotationMatrix.Transform(new Vector2Int(-1, 1));
            Vector2Int topTile = tile + rotationMatrix.Transform(new Vector2Int(0, 1));
            Vector2Int topRightTile = tile + rotationMatrix.Transform(new Vector2Int(1, 1));
            Vector2Int rightTile = tile + rotationMatrix.Transform(new Vector2Int(1, 0));

            Vector2 mid = room.MiddleOfTile(tile.x, tile.y);
            List<Vector2> vertices = new List<Vector2>();
            
            if (IsSolid(leftTile, Direction.Right, phase) && IsSolid(topTile, Direction.Down, phase)) // L shaped
            {
                if (IsSlope(leftTile, Direction.UpLeft, phase) || IsSlope(topTile, Direction.UpLeft, phase))
                {
                    vertices.Add(new Vector2(-10f, tileSize));
                    vertices.Add(new Vector2(-tileSize, 10f));
                }
                else if (!IsSolid(topLeftTile, Direction.DownRight, phase))
                {
                    vertices.Add(new Vector2(-10f, tileSize));
                    vertices.Add(new Vector2(-tileSize, tileSize));
                    vertices.Add(new Vector2(-tileSize, 10f));
                }
            }
            else if (!IsSolid(topTile, Direction.Down, phase))  // open top
            {
                if (IsSolid(leftTile, Direction.Right, phase))
                    vertices.Add(new Vector2(-10f, tileSize));
                else
                {
                    if (IsSolid(topLeftTile, Direction.DownRight, phase)) //prevent see-through corners
                        vertices.Add(new Vector2(-10f, 10f));
                    vertices.Add(new Vector2(-tileSize, tileSize));
                }
                if (IsSolid(rightTile, Direction.Left, phase))
                    vertices.Add(new Vector2(10f, tileSize));
                else
                {
                    vertices.Add(new Vector2(tileSize, tileSize));
                    //if (IsSolid(topRightTile, Direction.DownLeft, phase)) //prevent see-through corners
                    //    vertices.Add(new Vector2(10f, 10f));
                }
            }

            // rotate vertices, translate them to their block, and add them to the list
            Vector2[] vertices2 = vertices.ToArray();
            for (int i = 1; i < vertices2.Length; i++)
            {
                edges.Add(AddCorner(mid + rotationMatrix.Transform(vertices2[i-1])));
                edges.Add(AddCorner(mid + rotationMatrix.Transform(vertices2[i])));
            }
        }

        readonly Direction[] slopeTable = { Direction.UpLeft, Direction.UpRight, Direction.DownLeft, Direction.DownRight };

        private bool IsSolid(Vector2Int tile, Direction face, int phase)
        {
            Room.Tile.TerrainType terrain = room.GetTile(tile.x, tile.y).Terrain;

            if (terrain == Room.Tile.TerrainType.Solid)
                return true;

            if (terrain == Room.Tile.TerrainType.Slope)
            {
                Room.SlopeDirection slopeDir = room.IdentifySlope(tile.x, tile.y);
                if (slopeDir == Room.SlopeDirection.Broken)
                    return false;

                //adjust face based on slope direction and phase
                face = (Direction)(((int)face + (9 - (int)slopeTable[(int)slopeDir]) + (2 * phase)) % 8);
                
                //assume slope direction is UpRight
                return face <= Direction.Left && face >= Direction.Down;
            }

            return false;
        }

        private bool IsSlope(Vector2Int tile, Direction dir, int phase)
        {
            Room.SlopeDirection slopeDir = room.IdentifySlope(tile.x, tile.y);
            if (slopeDir != Room.SlopeDirection.Broken)
            {
                dir = (Direction)(((int)dir + (phase * 2)) % 8);
                return dir == slopeTable[(int)slopeDir];
            }
            return false; 
        }
    }

    //used for los mesh building
    class Matrix
    {
        float m11, m12, m21, m22;
        public Matrix(float m11, float m12, float m21, float m22)
        {
            this.m11 = m11;
            this.m12 = m12;
            this.m21 = m21;
            this.m22 = m22;
        }

        public Vector2 Transform(Vector2 v)
        {
            Vector2 result = new Vector2();
            result.x = m11 * v.x + m12 * v.y;
            result.y = m21 * v.x + m22 * v.y;
            return result;
        }

        public Vector2Int Transform(Vector2Int v)
        {
            Vector2Int result = new Vector2Int();
            result.x = (int)Math.Round(m11 * v.x + m12 * v.y);
            result.y = (int)Math.Round(m21 * v.x + m22 * v.y);
            return result;
        }
    }
}
