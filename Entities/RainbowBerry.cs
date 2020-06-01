using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// A rainbow berry behaves just like a red berry, but appears when you got all silver berries in a defined level set.
    /// </summary>
    [CustomEntity("CollabUtils2/RainbowBerry")]
    [RegisterStrawberry(tracked: false, blocksCollection: false)]
    class RainbowBerry : Strawberry {
        public static SpriteBank SpriteBank;

        private string levelSet;

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/RainbowBerry.xml");
        }

        public RainbowBerry(EntityData data, Vector2 offset, EntityID gid) : base(data, offset, gid) {
            levelSet = data.Attr("levelSet");
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            // fix the bloom to match golden alpha
            new DynData<Strawberry>(this).Get<BloomPoint>("bloom").Alpha = 0.5f;

            if (CollabMapDataProcessor.SilverBerries.ContainsKey(levelSet)) {
                int missingBerries = 0;
                int totalBerries = CollabMapDataProcessor.SilverBerries[levelSet].Count;
                foreach (KeyValuePair<string, EntityID> requiredSilver in CollabMapDataProcessor.SilverBerries[levelSet]) {
                    // check if the silver was collected.
                    AreaStats stats = SaveData.Instance.GetAreaStatsFor(AreaData.Get(requiredSilver.Key).ToKey());
                    if (!stats.Modes[0].Strawberries.Contains(requiredSilver.Value)) {
                        // this berry wasn't collected!
                        missingBerries++;
                    }
                }

                if (missingBerries != 0) {
                    // some berries are missing, spawn the hologram instead of the berry.
                    HoloRainbowBerry hologram = new HoloRainbowBerry(Position, totalBerries - missingBerries, totalBerries);
                    scene.Add(hologram);

                    RemoveSelf();
                }
            }
        }
    }
}
