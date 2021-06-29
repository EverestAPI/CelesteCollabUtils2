using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using System.IO;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Xna.Framework.Graphics;

namespace Celeste.Mod.CollabUtils2 {
    public class HubMapHelper {

        public enum ChapterState {
            Hidden,
            NeverSeen,
            Seen,
            Beaten,
        }
        public struct ChapterPanel {
            public Vector2 position;
            public ChapterState state;

            public ChapterPanel(EntityData data) {

                position = data.Position + new Vector2(data.Width / 2, data.Height / 2);

                state = ChapterState.NeverSeen;
                bool allMapsDone = true;

                foreach (var area in SaveData.Instance.Areas_Safe) {
                    if (area.SID == data.Attr("map")) {

                        if (LobbyHelper.IsHeartSide(area.GetSID())) {
                            if (allMapsDone || area.TotalTimePlayed > 0) {
                            } else {
                                state = ChapterState.Hidden;
                                // all maps weren't complete yet, and the heart side was never accessed: hide the heart side for now.
                                continue;
                            }
                        }

                        if (area.TotalTimePlayed <= 0) {
                            // skip the map, because it was not discovered yet.
                            // since it wasn't discovered, we can already say all maps weren't done though.
                            allMapsDone = false;
                            continue;
                        }

                        var mode = area.Modes[0];
                        
                        if (mode.HeartGem || mode.Completed) {
                            state = ChapterState.Beaten;
                        } else if (mode.TimePlayed > 0) {
                            state = ChapterState.Seen;
                        }
                        break;
                    }
                }
            }
        }

        public static void SetColors() {
            colors.Clear();
            colors['0'] = Color.Black;
        }
        private static void SetColor(char c) {
            const int tileSample = 3;
            var map = GFX.FGAutotiler.GenerateBox(c, tileSample, tileSample).TileGrid.Tiles;

            if (map == null)
                return;

            var texture = map[0, 0].Texture.Texture_Safe;

            int r = 0, g = 0, b = 0, idx = 0;

            Color[] colorArray = new Color[texture.Width * texture.Height];
            texture.GetData(colorArray);

            foreach (var color in colorArray) {
                if (color.A == 0) {
                    continue;
                }
                r += color.R;
                g += color.G;
                b += color.B;
                idx++;
            }

            if (idx == 0)
                colors[c] = Color.Magenta;
            else
                colors[c] = new Color(
                    (int) Calc.Map(r / idx, 0, 255, 30, 210),
                    (int) Calc.Map(g / idx, 0, 255, 30, 210),
                    (int) Calc.Map(b / idx, 0, 255, 30, 210));
        }

        static readonly Dictionary<ChapterState, Color> levelColors = new Dictionary<ChapterState, Color>() {
            {ChapterState.NeverSeen, Color.Red },
            {ChapterState.Seen, Color.Goldenrod },
            {ChapterState.Beaten, Color.Green },
        };
        static readonly Dictionary<ChapterState, Color> levelBorders = new Dictionary<ChapterState, Color>() {
            {ChapterState.NeverSeen, Color.Black },
            {ChapterState.Seen, Color.Black },
            {ChapterState.Beaten, Color.Black },
        };

        static Dictionary<char, Color> colors = new Dictionary<char, Color>();

        List<DisplaySolid> solids = new List<DisplaySolid>();

        List<ChapterPanel> levels = new List<ChapterPanel>();


        public IReadOnlyList<ChapterPanel> LevelPoints => levels;

        public int Width, Height;
        internal Vector2 entityOffset;

        public HubMapHelper(LevelData data) {

            Width = (int) Math.Ceiling(data.Bounds.Width / 8d);
            Height = (int) Math.Ceiling(data.Bounds.Height / 8d);

            colors['0'] = Color.Black;

            Calc.PushRandom(100);

            VirtualMap<char> map = new VirtualMap<char>(Width, Height);
            string[] split = data.Solids.Split('\n');
            for (int y = 0; y < Height; ++y) {
                int x;
                char lastChar = '0';
                for (x = 0; x < Width && x < split[y].Length; ++x) {
                    lastChar = split[y][x];
                    map[x, y] = lastChar;

                    if (!colors.ContainsKey(lastChar)) {
                        SetColor(lastChar);
                    }
                }
                for(; x < Width; ++x) {
                    map[x, y] = lastChar;
                }
            }

            Calc.PopRandom();

            foreach (EntityData ed in data.Entities) {
                switch (ed.Name) {
                    case "fakeWall":
                        int xPos = (int) (ed.Position.X / 8),
                            yPos = (int) (ed.Position.Y / 8);

                        char val = ed.Char("tiletype");

                        for (int x = 0; x < ed.Width / 8; ++x) {
                            for (int y = 0; y < ed.Height / 8; ++y) {
                                map[x + xPos, y + yPos] = val;
                            }
                        }
                        break;
                }

            }

            entityOffset = data.Position;

            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {

                    int num2 = 0;
                    char c = map[x, y];
                    while (x + num2 < Width && map[x + num2, y] == c) {
                        num2++;
                    }
                    if (num2 > 0) {
                        solids.Add(new DisplaySolid(new Rectangle(x, y, num2, 1), c));
                        x += num2 - 1;
                    }
                }
            }

            foreach (EntityData ed in data.Triggers) {
                switch (ed.Name) {
                    case "CollabUtils2/ChapterPanelTrigger":
                        levels.Add(new ChapterPanel(ed));
                        break;
                }
            }
        }

        private static int PixelSize;
        private static Rectangle Bounds;
        private static Vector2 RenderOffset;

        private static void DrawRect(Rectangle rect, Color color) {
            rect.X -= (int) RenderOffset.X;
            rect.Y -= (int) RenderOffset.Y;

            rect.X *= PixelSize;
            rect.Y *= PixelSize;
            rect.Width *= PixelSize;
            rect.Height *= PixelSize;

            if (rect.X >= Bounds.Width || rect.Y >= Bounds.Height)
                return;
            if (rect.Right <= 0 || rect.Bottom <= 0)
                return;

            if (rect.X < 0) {
                rect.Width += rect.X;
                rect.X = 0;
            }
            if (rect.Y < 0) {
                rect.Height += rect.Y;
                rect.Y = 0;
            }
            if (rect.Right > Bounds.Width) {
                rect.Width -= rect.Right - Bounds.Width;
            }
            if (rect.Bottom > Bounds.Height) {
                rect.Height -= rect.Bottom - Bounds.Height;
            }

            rect.X += Bounds.X;
            rect.Y += Bounds.Y;


            Draw.Rect(rect, color);
        }

        public void Render(Rectangle renderBounds, int pixelSize, bool showOffscreenLevels) {

            PixelSize = pixelSize;
            Bounds = renderBounds;

            Vector2? position = null;
            var player = Engine.Scene.Entities.FindFirst<Player>();
            if (player != null)
                position = player.Position - entityOffset;

            RenderOffset = ((position / 8) - new Vector2(renderBounds.Width / (2 * pixelSize), renderBounds.Height / (2 * pixelSize))) ?? RenderOffset;

            bool showColor = CollabModule.Instance.Settings.ColoredMinimap;

            foreach (var rect in solids) {
                var color = showColor ? colors[rect.TileType] : (rect.TileType == '0' ? Color.Black : Color.Gray);

                if (color.R == 0)
                    color *= 0.4f;

                DrawRect(rect.Rectangle, color * 0.85f);
            }

            foreach (var level in levels) {
                if (level.state == ChapterState.Hidden)
                    continue;

                Vector2 scaled = level.position / 8;

                if ((level.state == ChapterState.NeverSeen || level.state == ChapterState.Seen) && showOffscreenLevels) {

                    if (scaled.X < (int) RenderOffset.X)
                        scaled.X = (int) RenderOffset.X;
                    if (scaled.Y < (int) RenderOffset.Y)
                        scaled.Y = (int) RenderOffset.Y;
                    if (scaled.X >= (int) RenderOffset.X + Bounds.Width / pixelSize)
                        scaled.X = (int) RenderOffset.X + Bounds.Width / pixelSize - 1;
                    if (scaled.Y >= (int) RenderOffset.Y + Bounds.Height / pixelSize)
                        scaled.Y = (int) RenderOffset.Y + Bounds.Height / pixelSize - 1;
                }


                var rect = new Rectangle((int) (scaled.X), (int) (scaled.Y), 1, 1);
                rect.Inflate(2, 1);

                DrawRect(rect, levelBorders[level.state]);
                rect.Inflate(-1, 1);
                DrawRect(rect, levelBorders[level.state]);
                rect.Inflate(0, -1);

                DrawRect(rect, levelColors[level.state]);
            }

            if (position != null) {
                var rect = new Rectangle((int) Math.Floor((float) (position?.X / 8)), (int) Math.Floor((float) (position?.Y / 8)), 1, 1);
                rect.Y -= 2;

                rect.Inflate(2, 1);
                DrawRect(rect, Color.Black);
                rect.Inflate(-1, 1);
                DrawRect(rect, Color.Black);
                rect.Inflate(0, -1);

                DrawRect(rect, Color.White);
            }
        } 
    }

    internal struct DisplaySolid {
        public Rectangle Rectangle;
        public char TileType;

        public DisplaySolid(Rectangle rect, char tile) {
            Rectangle = rect;
            TileType = tile;
        }

        public override int GetHashCode() {
            int hashCode = -1030903623;
            hashCode = hashCode * -1521134295 + Rectangle.GetHashCode();
            hashCode = hashCode * -1521134295 + TileType.GetHashCode();
            return hashCode;
        }
    }
}
