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
        private string levelSet;

        internal HoloRainbowBerry HologramForCutscene;
        internal int CutsceneTotalBerries;

        public RainbowBerry(EntityData data, Vector2 offset, EntityID gid) : base(data, offset, gid) {
            levelSet = data.Attr("levelSet");
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            // fix the bloom to match golden alpha
            DynData<Strawberry> self = new DynData<Strawberry>(this);
            self.Get<BloomPoint>("bloom").Alpha = 0.5f;

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
                } else {
                    // all berries are here! check if we should play the unlock cutscene.
                    if (!CollabModule.Instance.SaveData.CombinedRainbowBerries.Contains((scene as Level).Session.Area.GetSID())) {
                        // spawn the hologram for the animation...
                        HoloRainbowBerry hologram = new HoloRainbowBerry(Position, totalBerries, totalBerries);
                        hologram.Tag = Tags.FrozenUpdate;
                        scene.Add(hologram);

                        // make rainbow berry invisible for now...
                        Visible = false;
                        Collidable = false;
                        self.Get<BloomPoint>("bloom").Visible = (self.Get<VertexLight>("light").Visible = false);

                        // now we wait for the player to enter the trigger. filling the HologramForCutscene field will tell the trigger to create the cutscene.
                        HologramForCutscene = hologram;
                        CutsceneTotalBerries = totalBerries;
                    }
                }
            }
        }
    }
}
