using RWCustom;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;

namespace LineOfSight
{
    class LOSController : CosmeticSprite
    {
        internal static bool hackToDelayDrawingUntilAfterTheLevelMoves;

        private static Matrix ROTATE_0 = new Matrix(1f, 0f, 0f, 1f);
        private static Matrix ROTATE_90 = new Matrix(0f, 1f, -1f, 0f);
        private static Matrix ROTATE_180 = new Matrix(-1f, 0f, 0f, -1f);
        private static Matrix ROTATE_270 = new Matrix(0f, -1f, 1f, 0f);

        private int _x;
        private int _y;
        public float lastScreenblockAlpha = 1f;
        public float screenblockAlpha = 1f;
        public bool hideAllSprites = false;
        private float _peekAlpha;
        private float _lastPeekAlpha;
        private Vector2 _peekPos;
        private float _peekAngle;
        private Vector2? _overrideEyePos;
        private Vector2 _lastOverrideEyePos;

        //config variables
        private float tileSize;
        private float brightness;
        private float visibility;

        private Room.Tile[,] _tiles;

        private static FShader _fovShader;
        private FShader _shader;

        public enum MappingState
        {
            FindingEdges,
            DuplicatingPoints,
            Done
        }
        public MappingState state;

        private FieldInfo _Room_Tiles = typeof(Room).GetField("Tiles", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        public LOSController(Room room, OptionsMenu config)
        {
            tileSize = config.tileSize.Value;
            brightness = config.brightness.Value / 100f;
            visibility = config.visibility.Value / 100f;

            _x = 0;
            _y = 0;

            if (_fovShader == null)
            {
                _fovShader = FShader.CreateShader("LOSShader", Assets.LOSShader);
            }

            _shader = LineOfSightMod.classic ? room.game.rainWorld.Shaders["Basic"] : _fovShader;

            // Create a copy of the room's tiles
            Room.Tile[,] fromTiles = (Room.Tile[,])_Room_Tiles.GetValue(room);
            _tiles = new Room.Tile[fromTiles.GetLength(0), fromTiles.GetLength(1)];
            Array.Copy(fromTiles, _tiles, fromTiles.Length);
        }

        public List<Vector2> corners = new List<Vector2>();
        public List<int> edges = new List<int>();

        private enum Direction
        {
            Up,
            Right,
            Down,
            Left
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


        private static IntVector2[] _peekSearchOffsets = new IntVector2[]
        {
            new IntVector2( 0,  0), // Middle
            new IntVector2( 1,  0), // 1 tile
            new IntVector2( 0,  1),
            new IntVector2(-1,  0),
            new IntVector2( 0, -1),
            new IntVector2( 2,  0), // 2 tiles
            new IntVector2( 1,  1),
            new IntVector2( 0,  2),
            new IntVector2(-1,  1),
            new IntVector2(-2,  0),
            new IntVector2(-1, -1),
            new IntVector2( 0, -2)
        };
        private FieldInfo _RainWorldGame_updateShortCut = typeof(RainWorldGame).GetField("updateShortCut", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        public override void Update(bool eu)
        {
            base.Update(eu);

            lastScreenblockAlpha = screenblockAlpha;

            hideAllSprites = false;
            if (room.game.IsArenaSession)
            {
                if (!room.game.GetArenaGameSession.playersSpawned)
                    hideAllSprites = true;
            }

            Player ply = null;
            if (room.game.Players.Count > 0)
                ply = room.game.Players[0].realizedCreature as Player;

            // Map edges to display quads
            if (state != MappingState.Done)
                UpdateMapper(300);

            // Do not try to access shortcuts when the room is not ready for AI
            if (!room.readyForAI)
            {
                screenblockAlpha = 1f;
                return;
            }

            // Find the player's shortcut vessel
            ShortcutHandler.ShortCutVessel plyVessel = null;
            foreach (ShortcutHandler.ShortCutVessel vessel in room.game.shortcuts.transportVessels)
            {
                if (vessel.creature == ply)
                {
                    plyVessel = vessel;
                    break;
                }
            }

            if (ply == null || ply.room != room || (plyVessel != null && plyVessel.entranceNode != -1))
                screenblockAlpha = Mathf.Clamp01(screenblockAlpha + 0.1f);
            else
                screenblockAlpha = Mathf.Clamp01(screenblockAlpha - 0.1f);

            if (ply != null)
            {
                // Search for the closest shortcut entrance and display a sprite at the end location
                // Disabled in classic mode
                if (!LineOfSightMod.classic)
                {
                    IntVector2 scPos = new IntVector2();
                    bool found = false;
                    if (ply.room != null && !ply.inShortcut)
                    {
                        for (int chunk = 0; chunk < ply.bodyChunks.Length; chunk++)
                        {
                            IntVector2 chunkPos = room.GetTilePosition(ply.bodyChunks[chunk].pos);
                            for (int i = 0; i < _peekSearchOffsets.Length; i++)
                            {
                                IntVector2 testPos = chunkPos + _peekSearchOffsets[i];
                                if (testPos.x < 0 || testPos.y < 0 || testPos.x >= room.TileWidth || testPos.y >= room.TileHeight) continue;
                                if (room.GetTile(testPos).Terrain == Room.Tile.TerrainType.ShortcutEntrance)
                                {
                                    int ind = Array.IndexOf(room.shortcutsIndex, testPos);
                                    if (ind > -1 && ind < (room.shortcuts?.Length ?? 0))
                                    {
                                        if (room.shortcuts[ind].shortCutType == ShortcutData.Type.Normal)
                                        {
                                            found = true;
                                            scPos = testPos;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    ShortcutData sc = default(ShortcutData);
                    int scInd = Array.IndexOf(room.shortcutsIndex, scPos);
                    if (scInd > -1 && scInd < (room.shortcuts?.Length ?? 0))
                        sc = room.shortcuts[scInd];
                    else
                        found = false;
                    if (found)
                    {
                        IntVector2 dest = sc.DestTile;
                        Vector2 newPeekPos = ply.room.MiddleOfTile(dest);
                        if (_peekPos != newPeekPos)
                        {
                            _peekAlpha = 0f;
                            _peekPos = newPeekPos;
                            _peekAngle = 0f;
                            for (int i = 0; i < 4; i++)
                            {
                                if (!ply.room.GetTile(dest + Custom.fourDirections[i]).Solid)
                                {
                                    _peekAngle = 180f - 90f * i;
                                    break;
                                }
                            }
                        }
                    }

                    _lastPeekAlpha = _peekAlpha;
                    _peekAlpha = Custom.LerpAndTick(_peekAlpha, found ? Mathf.Sin(room.game.clock / 40f * Mathf.PI * 4f) * 0.25f + 0.75f : 0f, 0.1f, 0.075f);
                }

                // Allow vision when going through shortcuts
                if (plyVessel != null)
                {
                    int updateShortCut = (int)_RainWorldGame_updateShortCut.GetValue(room.game);
                    bool first = !_overrideEyePos.HasValue;
                    if (!first) _lastOverrideEyePos = _overrideEyePos.Value;
                    _overrideEyePos = Vector2.Lerp(plyVessel.lastPos.ToVector2(), plyVessel.pos.ToVector2(), (updateShortCut + 1) / 3f) * 20f + new Vector2(10f, 10f);
                    if (first) _lastOverrideEyePos = _overrideEyePos.Value;
                    if (plyVessel.room.realizedRoom != null)
                        screenblockAlpha = plyVessel.room.realizedRoom.GetTile(_overrideEyePos.Value).Solid ? 1f : 0f;
                }
                else
                    _overrideEyePos = null;
            }
            else
            {
                _peekAlpha = 0f;
            }

            // Don't display in arena while multiple players are present
            // This doesn't happen in story so that Monkland still works
            if (room.game.IsArenaSession && room.game.Players.Count > 1)
                hideAllSprites = true;
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            base.InitiateSprites(sLeaser, rCam);

            _peekAlpha = 0f;
            _peekPos.Set(-1f, -1f);

            while (state != MappingState.Done)
                UpdateMapper(int.MaxValue);

            sLeaser.sprites = new FSprite[3];

            // Generate tris
            TriangleMesh.Triangle[] tris = new TriangleMesh.Triangle[edges.Count];
            for (int i = 0, len = edges.Count / 2; i < len; i++)
            {
                int o = i * 2;
                tris[o] = new TriangleMesh.Triangle(edges[o], edges[o + 1], edges[o] + corners.Count / 2);
                tris[o + 1] = new TriangleMesh.Triangle(edges[o + 1], edges[o + 1] + corners.Count / 2, edges[o] + corners.Count / 2);
            }

            // Block outside of FoV with level color
            TriangleMesh colorBlocker = new TriangleMesh("Futile_White", tris, false, true);
            colorBlocker.shader = _shader;
            sLeaser.sprites[0] = colorBlocker;
            corners.CopyTo(colorBlocker.vertices);
            colorBlocker.Refresh();

            // Full screen overlay
            sLeaser.sprites[1] = new FSprite("pixel")
            {
                anchorX = 0f,
                anchorY = 0f
            };
            sLeaser.sprites[1].shader = _shader;

            // Shortcut peek
            tris = new TriangleMesh.Triangle[]
            {
                // Small square
                new TriangleMesh.Triangle(0, 1, 2), new TriangleMesh.Triangle(1, 2, 3),
                // Large trapezoid
                new TriangleMesh.Triangle(2, 3, 4), new TriangleMesh.Triangle(3, 4, 5),
                new TriangleMesh.Triangle(4, 5, 6), new TriangleMesh.Triangle(5, 6, 7)
            };
            TriangleMesh scPeek = new TriangleMesh("Futile_White", tris, true, true);
            scPeek.vertices[0].Set(-10f, -10f);
            scPeek.vertices[1].Set(-10f, 10f);
            scPeek.vertices[2].Set(10f, -10f);
            scPeek.vertices[3].Set(10f, 10f);
            scPeek.vertices[4].Set(30f, -30f);
            scPeek.vertices[5].Set(30f, 30f);
            scPeek.vertices[6].Set(60f, -60f);
            scPeek.vertices[7].Set(60f, 60f);
            sLeaser.sprites[2] = scPeek;

            AddToContainer(sLeaser, rCam, null);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            /*if (LineOfSightMod.classic)
            {
                sLeaser.sprites[0].color = palette.blackColor;
                sLeaser.sprites[1].color = palette.blackColor;
            }*/
            Color unseenColor = Color.Lerp(Color.black, palette.blackColor, brightness);
            unseenColor.a = 1 - visibility;

            sLeaser.sprites[0].color = unseenColor;
            sLeaser.sprites[1].color = unseenColor;
            sLeaser.sprites[2].color = new Color(1f, 1f, 1f);
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContainer)
        {
            if (newContainer == null)
                newContainer = rCam.ReturnFContainer("ForegroundLights");
            for (int i = 0; i < sLeaser.sprites.Length; i++)
                newContainer.AddChild(sLeaser.sprites[i]);
        }

        private Vector2 _lastEyePos;
        private Vector2 _eyePos;
        private Vector2 _lastCamPos;
        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            if (!hackToDelayDrawingUntilAfterTheLevelMoves)
            {
                _lastCamPos = camPos;
                return;
            }

            _lastEyePos = _eyePos;

            if (sLeaser == null || rCam == null) return;
            if (room == null || room.game == null || sLeaser.sprites == null) return;

            foreach (FSprite sprite in sLeaser.sprites)
                sprite.isVisible = !hideAllSprites;

            if (room.game.Players.Count > 0)
            {
                BodyChunk headChunk = room.game.Players[0].realizedCreature?.bodyChunks[0];
                // Thanks, screams
                if (headChunk != null)
                    _eyePos = Vector2.Lerp(headChunk.lastPos, headChunk.pos, timeStacker);
            }

            if (_overrideEyePos.HasValue)
                _eyePos = Vector2.Lerp(_lastOverrideEyePos, _overrideEyePos.Value, timeStacker);

            // Update FOV blocker mesh
            TriangleMesh fovBlocker = (TriangleMesh)sLeaser.sprites[0];

            if (_eyePos != _lastEyePos)
            {
                Vector2 pos;
                pos.x = 0f;
                pos.y = 0f;
                for (int i = 0, len = corners.Count / 2; i < len; i++)
                {
                    pos.Set(corners[i].x - _eyePos.x, corners[i].y - _eyePos.y);
                    pos.Normalize();
                    fovBlocker.vertices[i].Set(corners[i].x, corners[i].y);
                    fovBlocker.vertices[i + len].Set(pos.x * 2000f + _eyePos.x, pos.y * 2000f + _eyePos.y);
                }

                // Calculate FoV blocker UVs
                for (int i = fovBlocker.UVvertices.Length - 1; i >= 0; i--)
                {
                    Vector2 wPos = fovBlocker.vertices[i] - _lastCamPos;
                    fovBlocker.UVvertices[i].x = InverseLerpUnclamped(rCam.levelGraphic.x, rCam.levelGraphic.x + rCam.levelGraphic.width, wPos.x);
                    fovBlocker.UVvertices[i].y = InverseLerpUnclamped(rCam.levelGraphic.y, rCam.levelGraphic.y + rCam.levelGraphic.height, wPos.y);
                }
                fovBlocker.Refresh();
            }

            fovBlocker.x = -_lastCamPos.x;
            fovBlocker.y = -_lastCamPos.y;

            if (!LineOfSightMod.classic && fovBlocker.element != rCam.levelGraphic.element)
                fovBlocker.element = rCam.levelGraphic.element;

            // Block the screen when inside a wall
            {
                IntVector2 tPos = room.GetTilePosition(_eyePos);
                if (tPos.x < 0) tPos.x = 0;
                if (tPos.x >= room.TileWidth) tPos.x = room.TileWidth - 1;
                if (tPos.y < 0) tPos.y = 0;
                if (tPos.y >= room.TileHeight) tPos.y = room.TileHeight - 1;
                if (_tiles[tPos.x, tPos.y].Solid)
                {
                    lastScreenblockAlpha = 1f;
                    screenblockAlpha = 1f;
                }
            }

            // Move the screenblock
            float alpha = Mathf.Lerp(lastScreenblockAlpha, screenblockAlpha, timeStacker);
            if (alpha == 0f)
            {
                sLeaser.sprites[1].isVisible = false;
            }
            else
            {
                FSprite screenBlock = sLeaser.sprites[1];
                screenBlock.scaleX = rCam.levelGraphic.scaleX;
                screenBlock.scaleY = rCam.levelGraphic.scaleY;
                screenBlock.x = rCam.levelGraphic.x;
                screenBlock.y = rCam.levelGraphic.y;
                if (LineOfSightMod.classic)
                {
                    // Must be resized to fit the level image
                    screenBlock.width = rCam.levelGraphic.width;
                    screenBlock.height = rCam.levelGraphic.height;
                }
                else if (screenBlock.element != rCam.levelGraphic.element)
                    screenBlock.element = rCam.levelGraphic.element;
                screenBlock.alpha = alpha;
            }

            if (!LineOfSightMod.classic)
            {
                // Update shortcut peek
                float peekAlpha = Mathf.Lerp(_lastPeekAlpha, _peekAlpha, timeStacker);
                if (peekAlpha > 0f)
                {
                    TriangleMesh peek = (TriangleMesh)sLeaser.sprites[2];
                    //if (peek.element != rCam.levelGraphic.element)
                    //    peek.element = rCam.levelGraphic.element;
                    if (_lastPeekAlpha != _peekAlpha)
                    {
                        Color[] cols = peek.verticeColors;
                        for (int i = 0; i < cols.Length; i++)
                        {
                            float vertAlpha = (i < 6) ? peekAlpha : 0f;
                            //cols[i] = new Color(1f, vertAlpha * 0.75f, 0f, vertAlpha);
                            cols[i] = new Color(1f, 1f, 1f, vertAlpha * 0.25f);
                        }
                    }

                    //Rect bounds = rCam.levelGraphic.localRect;
                    //bounds.position += rCam.levelGraphic.GetPosition();
                    //for (int i = peek.UVvertices.Length - 1; i >= 0; i--)
                    //{
                    //    Vector2 wPos = peek.vertices[i];
                    //    float rad = _peekAngle * Mathf.Deg2Rad;
                    //    wPos.Set(wPos.x * Mathf.Cos(rad) + wPos.y * Mathf.Sin(rad), wPos.y * Mathf.Cos(rad) - wPos.x * Mathf.Sin(rad));
                    //    wPos = wPos + _peekPos - camPos;
                    //    peek.UVvertices[i].x = InverseLerpUnclamped(bounds.xMin, bounds.xMax, wPos.x);
                    //    peek.UVvertices[i].y = InverseLerpUnclamped(bounds.yMin, bounds.yMax, wPos.y);
                    //}

                    peek.SetPosition(_peekPos - _lastCamPos);
                    peek.rotation = _peekAngle;
                }
                else
                    sLeaser.sprites[2].isVisible = false;
            }
            else
                sLeaser.sprites[2].isVisible = false;

            // Keep on top
            FContainer container = sLeaser.sprites[2].container;
            if (container.GetChildAt(container.GetChildCount() - 1) != sLeaser.sprites[2])
            {
                for (int i = 0; i < sLeaser.sprites.Length; i++)
                    sLeaser.sprites[i].MoveToFront();
            }
            base.DrawSprites(sLeaser, rCam, timeStacker, _lastCamPos);

        }

        private float InverseLerpUnclamped(float from, float to, float t)
        {
            return (t - from) / (to - from);
        }

        public void UpdateMapper(int iterations)
        {
            Room.Tile[,] tiles = _tiles;
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
                                    CalculateEdge(new Vector2Int(_x, _y), ROTATE_0);
                                    CalculateEdge(new Vector2Int(_x, _y), ROTATE_90);
                                    CalculateEdge(new Vector2Int(_x, _y), ROTATE_180);
                                    CalculateEdge(new Vector2Int(_x, _y), ROTATE_270);
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

        public void CalculateEdge(Vector2Int tile, Matrix rotationMatrix)
        {
            // get the necessary tiles for the calculation
            Vector2Int leftTile = tile + rotationMatrix.Transform(new Vector2Int(-1, 0));
            Vector2Int topLeftTile = tile + rotationMatrix.Transform(new Vector2Int(-1, 1));
            Vector2Int topTile = tile + rotationMatrix.Transform(new Vector2Int(0, 1));
            Vector2Int topRightTile = tile + rotationMatrix.Transform(new Vector2Int(1, 1));
            Vector2Int rightTile = tile + rotationMatrix.Transform(new Vector2Int(1, 0));

            Vector2 mid = room.MiddleOfTile(tile.x, tile.y);
            List<Vector2> vertices = new List<Vector2>();
            
            if (IsSolid(leftTile) && !IsSolid(topLeftTile) && IsSolid(topTile)) // L shaped
            {
                if (IsSlope(leftTile) || IsSlope(topTile))
                {
                    vertices.Add(new Vector2(-10f, tileSize));
                    vertices.Add(new Vector2(-tileSize, 10f));
                }
                else
                {
                    vertices.Add(new Vector2(-10f, tileSize));
                    vertices.Add(new Vector2(-tileSize, tileSize));
                    vertices.Add(new Vector2(-tileSize, 10f));
                }
            }
            else if (!IsSolid(topTile))
            {
                if (IsSolid(leftTile))
                    vertices.Add(new Vector2(-10f, tileSize));
                else
                {
                    if (IsSolid(topLeftTile))
                        vertices.Add(new Vector2(-10f, 10f));
                    vertices.Add(new Vector2(-tileSize, tileSize));
                }
                if (IsSolid(rightTile))
                    vertices.Add(new Vector2(10f, tileSize));
                else
                {
                    vertices.Add(new Vector2(tileSize, tileSize));
                    if (IsSolid(topRightTile))
                        vertices.Add(new Vector2(10f, 10f));
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

        public bool IsSolid(Vector2Int tile)
        {
            Room.Tile.TerrainType terrain = room.GetTile(tile.x, tile.y).Terrain;

            if (terrain == Room.Tile.TerrainType.Solid ||
                (terrain == Room.Tile.TerrainType.Slope &&
                room.IdentifySlope(tile.x, tile.y) != Room.SlopeDirection.Broken)) 
                return true;
            return false;
        }
        public bool IsSlope(Vector2Int tile)
        {

            return room.GetTile(tile.x, tile.y).Terrain == Room.Tile.TerrainType.Slope &&
                room.IdentifySlope(tile.x, tile.y) != Room.SlopeDirection.Broken;
        }
    }

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
