using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.UI {
    public class OuiJournalCoverWithStickers : OuiJournalCover {
        private static Dictionary<string, MTexture> textures = new Dictionary<string, MTexture>();

        public static void Load() {
            On.Celeste.LevelLoader.ctor += onLevelLoad;
        }

        public static void Unload() {
            On.Celeste.LevelLoader.ctor -= onLevelLoad;
        }

        private static void onLevelLoad(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
            // unload stickers from the previous map, if any.
            foreach (KeyValuePair<string, MTexture> texture in textures) {
                Logger.Log("CollabUtils2/OuiJournalCoverWithStickers", "Unloading sticker " + texture.Key);
                texture.Value.Unload();
            }
            textures.Clear();

            // load stickers that will be shown on the next map.
            if (Everest.Content.Map.TryGetValue("Maps/" + session.Area.GetSID(), out ModAsset asset) && asset.TryGetMeta(out StickerMetadata meta) && meta != null) {
                foreach (Sticker sticker in meta.Stickers) {
                    if (!textures.ContainsKey(sticker.Path) && sticker.FinishedMaps.All(map => AreaData.Get(map) != null && SaveData.Instance.GetAreaStatsFor(AreaData.Get(map).ToKey()).Modes[0].Completed)) {
                        Logger.Log("CollabUtils2/OuiJournalCoverWithStickers", "Loading sticker " + sticker.Path);
                        textures[sticker.Path] = new MTexture(VirtualContent.CreateTexture("Graphics/Atlases/Stickers/" + sticker.Path));
                    }
                }
            }

            orig(self, session, startPosition);
        }

        private class StickerMetadata {
            public List<Sticker> Stickers { get; set; } = new List<Sticker>();
        }

        private class Sticker {
            public string Path { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Rotation { get; set; } = 0;
            public float Scale { get; set; } = 1;
            public List<string> FinishedMaps { get; set; } = new List<string>();
        }

        private List<Sticker> stickersToRender = new List<Sticker>();

        public OuiJournalCoverWithStickers(OuiJournal journal) : base(journal) {
            // determine which stickers we are going to render.
            if (Everest.Content.Map.TryGetValue("Maps/" + SaveData.Instance.CurrentSession_Safe.Area.GetSID(), out ModAsset asset) && asset.TryGetMeta(out StickerMetadata meta)) {
                foreach (Sticker sticker in meta.Stickers) {
                    if (textures.ContainsKey(sticker.Path) && sticker.FinishedMaps.All(map => AreaData.Get(map) != null && SaveData.Instance.GetAreaStatsFor(AreaData.Get(map).ToKey()).Modes[0].Completed)) {
                        stickersToRender.Add(sticker);
                    }
                }
            }
        }

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);

            // render the stickers!
            Draw.SpriteBatch.Begin();
            foreach (Sticker sticker in stickersToRender) {
                textures[sticker.Path].DrawCentered(new Vector2(sticker.X, sticker.Y), Color.White, sticker.Scale, (float) (sticker.Rotation * Math.PI / 180));
            }
            Draw.SpriteBatch.End();
        }
    }
}
