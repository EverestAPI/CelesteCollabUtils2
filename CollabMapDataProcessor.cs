using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CollabUtils2 {
    class CollabMapDataProcessor : EverestMapDataProcessor {
        // the structure here is: SilverBerries[LevelSet][SID] = ID of the silver berry in that map.
        // so, to check if all silvers in a levelset have been unlocked, go through all entries in SilverBerries[levelset].
        public static Dictionary<string, Dictionary<string, EntityID>> SilverBerries = new Dictionary<string, Dictionary<string, EntityID>>();
        private string levelName;

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
                    }
                }
            };
        }

        public override void Reset() {
            if (SilverBerries.ContainsKey(AreaKey.GetLevelSet())) {
                SilverBerries[AreaKey.GetLevelSet()].Remove(AreaKey.GetSID());
            }
        }

        public override void End() {
            // nothing to do here
        }
    }
}
