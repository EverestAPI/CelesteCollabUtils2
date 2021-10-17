using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CollabUtils2 {
    class CollabMapDataProcessor : EverestMapDataProcessor {
        public struct SpeedBerryInfo {
            public EntityID ID;
            public int Gold;
            public int Silver;
            public int Bronze;
        }

        // the structure here is: SilverBerries[LevelSet][SID] = ID of the silver berry in that map.
        // so, to check if all silvers in a levelset have been unlocked, go through all entries in SilverBerries[levelset].
        public static Dictionary<string, Dictionary<string, EntityID>> SilverBerries = new Dictionary<string, Dictionary<string, EntityID>>();
        public static Dictionary<string, SpeedBerryInfo> SpeedBerries = new Dictionary<string, SpeedBerryInfo>();
        private string levelName;

        public static HashSet<string> MapsWithSilverBerries = new HashSet<string>();
        public static HashSet<string> MapsWithRainbowBerries = new HashSet<string>();

        public override Dictionary<string, Action<BinaryPacker.Element>> Init() {
            return new Dictionary<string, Action<BinaryPacker.Element>> {
                {
                    "level", level => {
                        // be sure to write the level name down.
                        levelName = level.Attr("name").Split(':')[0];
                        if (levelName.StartsWith("lvl_")) {
                            levelName = levelName.Substring(4);
                        }
                    }
                },
                {
                    "entity:CollabUtils2/SilverBerry", silverBerry => {
                        if (!SilverBerries.TryGetValue(AreaKey.GetLevelSet(), out Dictionary<string, EntityID> allSilversInLevelSet)) {
                            allSilversInLevelSet = new Dictionary<string, EntityID>();
                            SilverBerries.Add(AreaKey.GetLevelSet(), allSilversInLevelSet);
                        }
                        allSilversInLevelSet[AreaKey.GetSID()] = new EntityID(levelName, silverBerry.AttrInt("id"));

                        MapsWithSilverBerries.Add(AreaKey.GetSID());
                    }
                },
                {
                    "entity:CollabUtils2/RainbowBerry", berry => MapsWithRainbowBerries.Add(AreaKey.GetSID())
                },
                {
                    "entity:CollabUtils2/SpeedBerry", speedBerry => {
                        SpeedBerries[AreaKey.GetSID()] = new SpeedBerryInfo() {
                            ID = new EntityID(levelName, speedBerry.AttrInt("id")),
                            Gold = speedBerry.AttrInt("goldTime"),
                            Silver = speedBerry.AttrInt("silverTime"),
                            Bronze = speedBerry.AttrInt("bronzeTime")
                        };
                    }
                }
            };
        }

        public override void Reset() {
            if (SilverBerries.ContainsKey(AreaKey.GetLevelSet())) {
                SilverBerries[AreaKey.GetLevelSet()].Remove(AreaKey.GetSID());
            }
            SpeedBerries.Remove(AreaKey.GetSID());
            MapsWithSilverBerries.Remove(AreaKey.GetSID());
            MapsWithRainbowBerries.Remove(AreaKey.GetSID());
        }

        public override void End() {
            // nothing to do here
        }
    }
}
