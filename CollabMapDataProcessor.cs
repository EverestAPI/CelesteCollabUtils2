using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabMapDataProcessor : EverestMapDataProcessor {
        public struct SpeedBerryInfo {
            public EntityID ID;
            public float Gold;
            public float Silver;
            public float Bronze;
        }

        public struct GymLevelInfo {
            public string[] Tech;
            public string Flag;
        }

        public struct GymTechInfo {
            public string Difficulty;
            public Color? DifficultyColor;
            public Color? LearntColor;
            public string AreaSID;
            public string Level;
        }

        // GymLevels: maps map SIDs to their gym level info
        // GymTech: maps collab IDs and tech names to gym tech info
        public static Dictionary<string, GymLevelInfo> GymLevels = new Dictionary<string, GymLevelInfo>();
        public static Dictionary<string, Dictionary<string, GymTechInfo>> GymTech = new Dictionary<string, Dictionary<string, GymTechInfo>>();

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
                            Gold = speedBerry.AttrFloat("goldTime"),
                            Silver = speedBerry.AttrFloat("silverTime"),
                            Bronze = speedBerry.AttrFloat("bronzeTime")
                        };
                    }
                },
                {
                    "triggers", triggerList => {
                        foreach (BinaryPacker.Element trigger in triggerList.Children) {
                            if(trigger.Name == "CollabUtils2/ChapterPanelTrigger") {
                                addGymInfoFromChapterPanelTrigger(trigger);
                            }
                        }
                    }
                },
                {
                    "entity:FlushelineCollab/LevelEntrance", levelEntrance => {
                        addGymInfoFromChapterPanelTrigger(levelEntrance);
                    }
                },
                {
                    "entity:CollabUtils2/GymMarker", gymMarker => {
                        string techName = gymMarker.Attr("name");
                        if (string.IsNullOrEmpty(techName))
                            return;
                        
                        string difficulty = gymMarker.Attr("difficulty", "beginner");
                        string difficultyColor = gymMarker.Attr("difficultyColor");
                        string learntColor = gymMarker.Attr("learntColor");
                        GymTechInfo techInfo = new GymTechInfo {
                            Difficulty = !string.IsNullOrEmpty(difficulty) ? difficulty : null,
                            DifficultyColor = !string.IsNullOrEmpty(difficultyColor) ? Calc.HexToColor(difficultyColor) : null,
                            LearntColor = !string.IsNullOrEmpty(learntColor) ? Calc.HexToColor(learntColor) : null,
                            AreaSID = AreaKey.GetSID(),
                            Level = levelName
                        };

                        string collabID = LobbyHelper.GetCollabNameForSID(AreaKey.GetSID());
                        if (GymTech.TryGetValue(collabID, out Dictionary<string, GymTechInfo> tech))
                            tech[techName] = techInfo;
                        else
                            GymTech.Add(collabID, new Dictionary<string, GymTechInfo> { { techName, techInfo } });
                    }
                }
            };
        }

        private static void addGymInfoFromChapterPanelTrigger(BinaryPacker.Element trigger) {
            string map = trigger.Attr("map");
            string tech = trigger.Attr("tech");
            if (!string.IsNullOrEmpty(map) && !string.IsNullOrEmpty(tech)) {
                GymLevels[map] = new GymLevelInfo {
                    Tech = trigger.Attr("tech").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                    Flag = trigger.Attr("flag")
                };
            }
        }

        public override void Reset() {
            if (SilverBerries.TryGetValue(AreaKey.GetLevelSet(), out Dictionary<string, EntityID> silverBerries))
                silverBerries.Remove(AreaKey.GetSID());
            SpeedBerries.Remove(AreaKey.GetSID());
            
            MapsWithSilverBerries.Remove(AreaKey.GetSID());
            MapsWithRainbowBerries.Remove(AreaKey.GetSID());
            
            GymLevels.Remove(AreaKey.GetSID());
            foreach ((string _, Dictionary<string, GymTechInfo> techForCollab) in GymTech) {
                string[] affectedTech = techForCollab.Where(kvp => kvp.Value.AreaSID == AreaKey.GetSID()).Select(kvp => kvp.Key).ToArray();
                foreach (string tech in affectedTech)
                    techForCollab.Remove(tech);
            }
        }

        public override void End() {
            // nothing to do here
        }
    }
}
