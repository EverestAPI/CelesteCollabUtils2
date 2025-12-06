using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// A rainbow berry behaves just like a red berry, but appears when you got all silver berries in a defined level set.
    /// </summary>
    [CustomEntity("CollabUtils2/RainbowBerry")]
    [RegisterStrawberry(tracked: false, blocksCollection: false)]
    public class RainbowBerry : Strawberry {
        private readonly string levelSet;
        private readonly string mapsRaw;
        private readonly string[] maps;
        private readonly int? requiredBerries;

        internal HoloRainbowBerry HologramForCutscene;
        internal int CutsceneTotalBerries;

        public RainbowBerry(EntityData data, Vector2 offset, EntityID gid) : base(data, offset, gid) {
            levelSet = data.Attr("levelSet");

            if (string.IsNullOrEmpty(data.Attr("maps"))) {
                maps = null;
                mapsRaw = null;
            } else {
                maps = data.Attr("maps").Split(',');
                mapsRaw = data.Attr("maps");

                for (int i = 0; i < maps.Length; i++) {
                    maps[i] = levelSet + "/" + maps[i];
                }
            }

            if (!string.IsNullOrEmpty(data.Attr("requires"))) {
                requiredBerries = int.Parse(data.Attr("requires"));
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            // fix the bloom to match golden alpha
            bloom.Alpha = 0.5f;

            if (CollabMapDataProcessor.SilverBerries.ContainsKey(levelSet)) {
                int missingBerries = 0;
                int totalBerries = 0;

                foreach (KeyValuePair<string, EntityID> requiredSilver in CollabMapDataProcessor.SilverBerries[levelSet]) {
                    if (maps == null || maps.Contains(requiredSilver.Key)) {
                        totalBerries++;

                        // check if the silver was collected.
                        AreaStats stats = SaveData.Instance.GetAreaStatsFor(AreaData.Get(requiredSilver.Key).ToKey());
                        if (!stats.Modes[0].Strawberries.Contains(requiredSilver.Value)) {
                            // this berry wasn't collected!
                            missingBerries++;
                        }
                    }
                }

                if (requiredBerries.HasValue) {
                    // adjust the total and missing berry count to account for the forced berry count.
                    int collectedBerries = totalBerries - missingBerries;

                    missingBerries = Math.Max(0, requiredBerries.Value - collectedBerries);
                    totalBerries = requiredBerries.Value;
                }

                if (missingBerries != 0) {
                    // some berries are missing, spawn the hologram instead of the berry.
                    HoloRainbowBerry hologram = new HoloRainbowBerry(Position, totalBerries - missingBerries, totalBerries);
                    scene.Add(hologram);

                    RemoveSelf();
                } else {
                    // all berries are here! check if we should play the unlock cutscene.
                    if (!CollabModule.Instance.SaveData.CombinedRainbowBerries.Contains(GetCombinedRainbowId(scene as Level))) {
                        // spawn the hologram for the animation...
                        HoloRainbowBerry hologram = new HoloRainbowBerry(Position, totalBerries, totalBerries);
                        hologram.Tag = Tags.FrozenUpdate;
                        scene.Add(hologram);

                        // make rainbow berry invisible for now...
                        Visible = false;
                        Collidable = false;
                        bloom.Visible = (light.Visible = false);

                        // now we wait for the player to enter the trigger. filling the HologramForCutscene field will tell the trigger to create the cutscene.
                        HologramForCutscene = hologram;
                        CutsceneTotalBerries = totalBerries;
                    }
                }
            }
        }

        public string GetCombinedRainbowId(Level level) {
            if (maps != null) {
                return string.Join(",", maps);
            } else {
                return level.Session.Area.GetSID();
            }
        }

        public bool MatchesRainbowBerryTriggerWithSettings(string levelSet, string maps) {
            if (!string.IsNullOrEmpty(levelSet) && this.levelSet != levelSet) {
                return false;
            }

            if (!string.IsNullOrEmpty(maps) && this.mapsRaw != maps) {
                return false;
            }

            return true;
        }
    }
}
