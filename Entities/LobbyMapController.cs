using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [Tracked]
    [CustomEntity("CollabUtils2/LobbyMapController")]
    public class LobbyMapController : Entity {
        public readonly ControllerInfo Info;
        public LobbyVisitManager VisitManager;

        public LobbyMapController(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Info = new ControllerInfo(data);
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            if (scene is Level level) {
                VisitManager = new LobbyVisitManager(level.Session.Area.SID, level.Session.LevelData.Name);
            }
        }

        public override void Update() {
            base.Update();
            
            if (Scene is Level level &&
                level.Tracker.GetEntity<LobbyMapUI>() == null &&
                level.Tracker.GetEntity<Player>() is Player player) {
                
                if (!level.OnInterval(0.2f) || player.StateMachine.State == Player.StDummy) {
                    return;
                }

                if (!level.Paused && !level.Transitioning) {
                    var playerPosition = new Vector2(Math.Min((float) Math.Floor((player.Center.X - level.Bounds.X) / 8f), (float) Math.Round(level.Bounds.Width / 8f, MidpointRounding.AwayFromZero) - 1),
                        Math.Min((float) Math.Floor((player.Center.Y - level.Bounds.Y) / 8f), (float) Math.Round(level.Bounds.Height / 8f, MidpointRounding.AwayFromZero) + 1));
                    VisitManager?.VisitPoint(playerPosition);
                }
            }
        }

        public override void SceneEnd(Scene scene) {
            VisitManager?.Save();
            base.SceneEnd(scene);
        }

        /// <summary>
        /// EntityData is parsed into a class so that it can be reused in <see cref="LobbyMapUI"/>
        /// without having to load the entire entity.
        /// </summary>
        public class ControllerInfo {
            private static readonly char[] semicolonSeparator = {';'};
            
            #region EntityData Fields
            
            /// <summary>
            /// The map background texture to read from <see cref="GFX.Gui"/>.
            /// Example: SJ2021/1-Beginner/beginnermap
            /// </summary>
            public string MapTexture;
            
            /// <summary>
            /// The total number of maps in the lobby, used to display the miniheart tally.
            /// </summary>
            public int TotalMaps;
            
            /// <summary>
            /// An array of custom entity names that should be considered map features.
            /// This is not required for CU2 entities.
            /// </summary>
            public CustomFeatureEntityInfo[] CustomFeatures;

            public int MemorialId;
            
            public string WarpIcon;
            public string RainbowBerryIcon;
            public string HeartDoorIcon;
            public string GymIcon;
            public string MapIcon;
            public string JournalIcon;
            public string MemorialIcon;

            public bool ShowWarps;
            public bool ShowRainbowBerry;
            public bool ShowHeartDoor;
            public bool ShowGyms;
            public bool ShowMaps;
            public bool ShowJournals;
            public bool ShowHeartCount;
            
            #endregion
            
            public int RoomWidth;
            public int RoomHeight;
            public string LevelSet;
            public int LobbyIndex;
            
            public ControllerInfo(EntityData data, MapData mapData = null) {
                MapTexture = data.Attr("mapTexture");
                TotalMaps = data.Int("totalMaps");
                MemorialId = data.Int("memorialId");

                WarpIcon = data.Attr("warpIcon", "CollabUtils2/lobbies/warp");
                RainbowBerryIcon = data.Attr("rainbowBerryIcon", "CollabUtils2/lobbies/rainbowBerry");
                HeartDoorIcon = data.Attr("heartDoorIcon", "CollabUtils2/lobbies/heartgate");
                GymIcon = data.Attr("gymIcon", "CollabUtils2/lobbies/gym");
                MapIcon = data.Attr("mapIcon", "CollabUtils2/lobbies/map");
                JournalIcon = data.Attr("journalIcon", "CollabUtils2/lobbies/journal");
                MemorialIcon = data.Attr("memorialIcon", "CollabUtils2/lobbies/memorial");

                ShowWarps = data.Bool("showWarps", true);
                ShowRainbowBerry = data.Bool("showRainbowBerry", true);
                ShowHeartDoor = data.Bool("showHeartDoor", true);
                ShowGyms = data.Bool("showGyms", true);
                ShowMaps = data.Bool("showMaps", true);
                ShowJournals = data.Bool("showJournals", true);
                ShowHeartCount = data.Bool("showHeartCount", true);

                var customFeatures = data.Attr("customFeatures");
                if (!string.IsNullOrWhiteSpace(customFeatures)) {
                    var customFeaturesList = new List<CustomFeatureEntityInfo>();
                    var tokens = customFeatures.Split(semicolonSeparator, StringSplitOptions.None);
                    foreach (var token in tokens) {
                        if (CustomFeatureEntityInfo.TryParse(token, out var value)) {
                            customFeaturesList.Add(value);
                        }
                    }
                    CustomFeatures = customFeaturesList.ToArray();
                } else {
                    CustomFeatures = default;
                }

                if (RoomWidth <= 0) RoomWidth = data.Level.TileBounds.Width;
                if (RoomHeight <= 0) RoomHeight = data.Level.TileBounds.Height;
                if (mapData != null) {
                    LevelSet = LobbyHelper.GetLobbyLevelSet(mapData.Area.SID);
                    LobbyIndex = LobbyHelper.GetLobbyIndex(mapData.Area.SID);
                }
            }

            public bool ShouldShowFeature(FeatureInfo feature) {
                switch (feature.Type) {
                    case FeatureType.Custom: return true;
                    case FeatureType.Warp: return ShowWarps;
                    case FeatureType.RainbowBerry: return ShowRainbowBerry;
                    case FeatureType.HeartDoor: return ShowHeartDoor;
                    case FeatureType.Gym: return ShowGyms;
                    case FeatureType.Map: return ShowMaps;
                    case FeatureType.Journal: return ShowJournals;
                    default: return true;
                }
            }

            public bool TryCreateCustom(EntityData data, out FeatureInfo value) {
                value = default;
                
                if (CustomFeatures != null) {
                    foreach (var custom in CustomFeatures) {
                        if (custom.TryCreate(data, out value)) {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
        
        public class CustomFeatureEntityInfo {
            private static readonly char[] commaSeparator = {','};
            private static readonly char[] equalsSeparator = {'='};
            
            public string Name;
            public FeatureType Type;
            public readonly Dictionary<string, string> AttributeMap = new Dictionary<string, string>();

            public static bool TryParse(string str, out CustomFeatureEntityInfo value) {
                value = null;

                var tokens = str.Split(commaSeparator, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) return false;
                
                var name = tokens[0];
                if (!Enum.TryParse(tokens[1], true, out FeatureType type)) return false;

                value = new CustomFeatureEntityInfo {
                    Name = name, Type = type,
                };
                
                for (int i = 2; i < tokens.Length; i++) {
                    var subtokens = tokens[i].Split(equalsSeparator, StringSplitOptions.RemoveEmptyEntries);
                    if (subtokens.Length != 2 || string.IsNullOrWhiteSpace(subtokens[0]) || string.IsNullOrWhiteSpace(subtokens[1])) return false;
                    value.AttributeMap[subtokens[0]] = subtokens[1];
                }

                return true;
            }

            public bool TryCreate(EntityData data, out FeatureInfo value) {
                value = default;
                
                if (data.Name != Name) return false;
                
                value.Type = Type;

                foreach (var key in AttributeMap.Keys) {
                    var attrValue = AttributeMap[key];
                    if (attrValue.StartsWith("$") && attrValue.Length > 1 && data.Has(attrValue.Substring(1))) {
                        attrValue = data.Attr(attrValue.Substring(1));
                    }

                    if (key.Equals(nameof(FeatureInfo.FeatureId), StringComparison.InvariantCultureIgnoreCase)) {
                        value.FeatureId = attrValue;
                    } else if (key.Equals(nameof(FeatureInfo.Icon), StringComparison.InvariantCultureIgnoreCase)) {
                        value.Icon = attrValue;
                    } else if (key.Equals(nameof(FeatureInfo.Map), StringComparison.InvariantCultureIgnoreCase)) {
                        value.Map = attrValue;
                    } else if (key.Equals(nameof(FeatureInfo.DialogKey), StringComparison.InvariantCultureIgnoreCase)) {
                        value.DialogKey = attrValue;
                    }
                }

                return true;
            }
        }
        
        public struct FeatureInfo {
            /// <summary>
            /// The icon in the Gui atlas.
            /// </summary>
            public string Icon;
            
            /// <summary>
            /// A key into Dialog.Clean for features that include a title.
            /// </summary>
            public string DialogKey;
            
            /// <summary>
            /// A unique id for this feature. Also used to identify and sort warps.
            /// </summary>
            public string FeatureId;
            
            /// <summary>
            /// The type of feature. This allows for filtering in the controller.
            /// </summary>
            public FeatureType Type;
            
            /// <summary>
            /// The position of the feature on the map.
            /// </summary>
            public Vector2 Position;
            
            /// <summary>
            /// The SID for the lobby map.
            /// </summary>
            public string SID;
            
            /// <summary>
            /// The room within the map that this feature belongs to.
            /// </summary>
            public string Room;
            
            /// <summary>
            /// The name of the map to load when using a feature type of <see cref="FeatureType.Map"/>.
            /// </summary>
            public string Map;
            
            /// <summary>
            /// Extracted data about the referenced map if it exists.
            /// </summary>
            public MapInfo MapInfo;

            public static bool TryParse(EntityData data, ControllerInfo controllerInfo, out FeatureInfo value) {
                value = default;
                
                // CU2 warp entity
                if (data.Name == "CollabUtils2/LobbyMapWarp") {
                    value.Type = FeatureType.Warp;
                    value.DialogKey = data.Attr("dialogKey");
                    value.FeatureId = data.Attr("warpId");
                    value.Icon = data.Attr("icon");
                }
                // CU2 rainbow berry
                else if (data.Name == "CollabUtils2/RainbowBerry") {
                    value.Type = FeatureType.RainbowBerry;
                }
                // CU2 heart door
                else if (data.Name == "CollabUtils2/MiniHeartDoor") {
                    value.Type = FeatureType.HeartDoor;
                }
                // CU2 journal trigger
                else if (data.Name == "CollabUtils2/JournalTrigger") {
                    value.Type = FeatureType.Journal;
                }
                // CU2 map trigger
                else if (data.Name == "CollabUtils2/ChapterPanelTrigger") {
                    value.Map = data.Attr("map");
                    value.Type = value.Map.Contains("0-Gyms") ? FeatureType.Gym : FeatureType.Map;
                }
                // XaphanHelper warp station (can only be warped to, not from)
                else if (data.Name == "XaphanHelper/WarpStation" && controllerInfo != null) {
                    value.Type = FeatureType.Warp;
                    value.FeatureId = data.Int("index").ToString();
                    value.DialogKey = $"{LobbyHelper.GetCollabNameForLevelSet(controllerInfo.LevelSet)}_0_Lobbies_Warp_Ch{controllerInfo.LobbyIndex}_{data.Level.Name}_{value.FeatureId}";
                }
                // something from the CustomFeatures property
                else if (controllerInfo != null && controllerInfo.TryCreateCustom(data, out value)) {
                    // do nothing
                }
                // not a valid map feature, skip
                else {
                    return false;
                }
                
                // grab the entity's position
                value.Position = data.Position;

                // if we haven't explictly set a feature id, default to the type and entity id
                if (string.IsNullOrWhiteSpace(value.FeatureId)) {
                    value.FeatureId = $"{value.Type}_{data.ID}";
                }

                // if we haven't explicitly set an icon, use the one defined in the controller for the given type
                // or fall back to some defaults
                if (string.IsNullOrWhiteSpace(value.Icon)) {
                    switch (value.Type) {
                        case FeatureType.Warp:
                            value.Icon = controllerInfo?.WarpIcon ?? "CollabUtils2/lobbies/warp";
                            break;
                        case FeatureType.RainbowBerry:
                            value.Icon = controllerInfo?.RainbowBerryIcon ?? "CollabUtils2/lobbies/rainbowBerry";
                            break;
                        case FeatureType.HeartDoor:
                            value.Icon = controllerInfo?.HeartDoorIcon ?? "CollabUtils2/lobbies/heartgate";
                            break;
                        case FeatureType.Gym:
                            value.Icon = controllerInfo?.GymIcon ?? "CollabUtils2/lobbies/gym";
                            break;
                        case FeatureType.Map:
                            value.Icon = controllerInfo?.MapIcon ?? "CollabUtils2/lobbies/map";
                            break;
                        case FeatureType.Journal:
                            value.Icon = controllerInfo?.JournalIcon ?? "CollabUtils2/lobbies/journal";
                            break;
                    }
                }

                // if this feature represents an enterable map, try to read the data for that map
                if (value.Type == FeatureType.Map && !string.IsNullOrWhiteSpace(value.Map)) {
                    value.MapInfo = new MapInfo(value.Map);
                }

                // successfully parsed the entity
                return true;
            }
        }

        public struct MapInfo {
            public readonly string SID;
            public readonly bool Completed;
            public readonly int Difficulty;

            public MapInfo(string mapName) {
                SID = string.Empty;
                Completed = false;
                Difficulty = -1;
                
                if (AreaData.Get(mapName) is AreaData areaData) {
                    SID = areaData.SID;
                    AreaStats areaStatsFor = SaveData.Instance.GetAreaStatsFor(areaData.ToKey());
                    Completed = areaStatsFor != null && areaStatsFor.Modes[0].Completed;

                    string mapDifficultyIconPath = areaData.Icon;
                    if (!string.IsNullOrWhiteSpace(mapDifficultyIconPath)) {
                        var iconFilename = mapDifficultyIconPath.Split('/').LastOrDefault() ?? string.Empty;
                        var firstToken = iconFilename.Split('-').FirstOrDefault() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(firstToken)) {
                            int.TryParse(firstToken, out Difficulty);
                        }
                    }
                }
            }
        }
        
        public enum FeatureType {
            Custom = 0,
            Warp,
            RainbowBerry,
            HeartDoor,
            Gym,
            Map,
            Journal,
        }
    }
}
