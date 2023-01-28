using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [Tracked]
    [CustomEntity(ENTITY_NAME)]
    public class LobbyMapController : Entity {
        public const string ENTITY_NAME = "CollabUtils2/LobbyMapController";
        
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
                    VisitManager?.VisitPoint(playerPosition, Info.ExplorationRadius);
                }
            }
        }

        public override void SceneEnd(Scene scene) {
            VisitManager?.Save();
            base.SceneEnd(scene);
        }

        /// <summary>
        /// EntityData is parsed into a struct so that it can be reused in <see cref="LobbyMapUI"/>
        /// without having to load the entire entity.
        /// </summary>
        public struct ControllerInfo {
            /// <summary>
            /// An array of map scales to be used as zoom levels. Should be in increasing order.
            /// </summary>
            public float[] ZoomLevels;
            
            /// <summary>
            /// Zero-based index into <see cref="ZoomLevels"/>, defaults to half the number of zoom levels, rounded down.
            /// </summary>
            public int DefaultZoomLevel;
            
            /// <summary>
            /// The map background texture to read from <see cref="GFX.Gui"/>.
            /// Example: SJ2021/1-Beginner/beginnermap
            /// </summary>
            public string MapTexture;
            
            /// <summary>
            /// The level set that this lobby belongs to.
            /// Example: StrawberryJam2021/1-Beginner
            /// </summary>
            public string LevelSet;
            
            /// <summary>
            /// The index of the lobby within its level set.
            /// </summary>
            public int LobbyIndex;
            
            /// <summary>
            /// The total number of maps in the lobby, used to display the miniheart tally.
            /// </summary>
            public int TotalMaps;
            
            /// <summary>
            /// The radius in tiles that should be revealed per visited point.
            /// </summary>
            public int ExplorationRadius;
            
            /// <summary>
            /// A comma-separated list of custom entity names that should be considered map features.
            /// This is not required for CU2 entities.
            /// </summary>
            public string CustomFeatures;
            
            /// <summary>
            /// The width of the room in tiles.
            /// </summary>
            public int RoomWidth;
            
            /// <summary>
            /// The height of the room in tiles.
            /// </summary>
            public int RoomHeight;

            public string WarpIcon;
            public string RainbowBerryIcon;
            public string HeartDoorIcon;
            public string GymIcon;
            public string MapIcon;
            public string JournalIcon;

            public bool ShowWarps;
            public bool ShowRainbowBerry;
            public bool ShowHeartDoor;
            public bool ShowGyms;
            public bool ShowMaps;
            public bool ShowJournals;

            public bool ShowHeartCount;
            
            public ControllerInfo(EntityData data, MapData mapData = null) {
                MapTexture = data.Attr("mapTexture");
                LevelSet = data.Attr("levelSet");
                LobbyIndex = data.Int("lobbyIndex");
                TotalMaps = data.Int("totalMaps");
                ExplorationRadius = data.Int("explorationRadius", 20);
                CustomFeatures = data.Attr("customFeatures");
                RoomWidth = data.Int("roomWidth");
                RoomHeight = data.Int("roomHeight");
                ShowHeartCount = data.Bool("showHeartCount", true);

                WarpIcon = data.Attr("warpIcon", "CollabUtils2/lobbies/warp");
                RainbowBerryIcon = data.Attr("rainbowBerryIcon", "CollabUtils2/lobbies/rainbowBerry");
                HeartDoorIcon = data.Attr("heartDoorIcon", "CollabUtils2/lobbies/heartDoorIcon");
                GymIcon = data.Attr("gymIcon", "CollabUtils2/lobbies/gym");
                MapIcon = data.Attr("mapIcon", "CollabUtils2/lobbies/map");
                JournalIcon = data.Attr("journalIcon", "CollabUtils2/lobbies/journal");

                ShowWarps = data.Bool("showWarps", true);
                ShowRainbowBerry = data.Bool("showRainbowBerry", true);
                ShowHeartDoor = data.Bool("showHeartDoor", true);
                ShowGyms = data.Bool("showGyms", true);
                ShowMaps = data.Bool("showMaps", true);
                ShowJournals = data.Bool("showJournals", true);

                var zoomLevels = data.Attr("zoomLevels", string.Empty)
                    .Split(',')
                    .Select(s => float.TryParse(s, out var value) ? value : -1)
                    .Where(f => f > 0)
                    .ToArray();

                if (zoomLevels.Any()) {
                    ZoomLevels = zoomLevels;
                } else {
                    ZoomLevels = new[] {
                        1f, 2f, 3f,
                    };
                }

                DefaultZoomLevel = data.Int("defaultZoomLevel", ZoomLevels.Length / 2);

                if (string.IsNullOrWhiteSpace(LevelSet)) {
                    var sid = mapData?.Area.SID ?? AreaData.Get(Engine.Scene)?.SID;
                    LevelSet = string.IsNullOrWhiteSpace(sid) ? string.Empty : LobbyHelper.GetLobbyLevelSet(sid);
                }
                
                if (RoomWidth <= 0) RoomWidth = data.Level.TileBounds.Width;
                if (RoomHeight <= 0) RoomHeight = data.Level.TileBounds.Height;
            }
        }
        
        public struct FeatureInfo {
            // public readonly bool ShowOnMap;
            public string Icon;
            public string DialogKey;
            public bool CanWarpFrom;
            public bool CanWarpTo;
            public string FeatureId;
            public FeatureType Type;
            public Vector2 Position;
            public string SID;
            public string Room;
            public string Map;
            public MapInfo MapInfo;

            public FeatureInfo(EntityData data, ControllerInfo controllerInfo) {
                TryParse(data, controllerInfo, out this);
            }
            
            public static bool TryParse(EntityData data, ControllerInfo controllerInfo, out FeatureInfo value) {
                value = default;
                
                // special case for CU2 entities
                if (data.Name == LobbyMapWarp.ENTITY_NAME) {
                    value.Type = FeatureType.Warp;
                } else if (data.Name == RainbowBerry.ENTITY_NAME) {
                    value.Type = FeatureType.RainbowBerry;
                } else if (data.Name == MiniHeartDoor.ENTITY_NAME) {
                    value.Type = FeatureType.HeartDoor;
                } else if (data.Name == JournalTrigger.ENTITY_NAME) {
                    value.Type = FeatureType.Journal;
                } else if (data.Name == ChapterPanelTrigger.CHAPTER_PANEL_TRIGGER_NAME) {
                    value.Map = data.Attr("map");
                    value.Type = value.Map.Contains("0-Gyms") ? FeatureType.Gym : FeatureType.Map;
                } else if (data.Has("cu2map_type")) {
                    value.Type = data.Enum("cu2map_type", FeatureType.Custom);
                } else {
                    return false;
                }
                
                // ShowOnMap = data.Bool("cu2map_showOnMap");

                value.Icon = data.Attr("cu2map_icon");
                value.DialogKey = data.Attr("cu2map_dialogKey");
                value.FeatureId = data.Attr("cu2map_id");
                value.Map = data.Attr("cu2map_map", value.Map);
                value.Position = data.Position;

                var fallbackIcon = string.Empty;
                var fallbackWarpFrom = false;
                var fallbackWarpTo = false;
                
                switch (value.Type) {
                    case FeatureType.Warp:
                        // value.Icon = "CollabUtils2/lobbyMap/warp";
                        fallbackIcon = controllerInfo.WarpIcon ?? "CollabUtils2/lobbies/warp";
                        fallbackWarpFrom = true;
                        fallbackWarpTo = true;
                        break;
                    case FeatureType.RainbowBerry:
                        fallbackIcon = controllerInfo.RainbowBerryIcon ?? "CollabUtils2/lobbies/rainbowBerry";
                        break;
                    case FeatureType.HeartDoor:
                        fallbackIcon = controllerInfo.HeartDoorIcon ?? "CollabUtils2/lobbies/heartgate";
                        break;
                    case FeatureType.Gym:
                        fallbackIcon = controllerInfo.GymIcon ?? "CollabUtils2/lobbies/gym";
                        break;
                    case FeatureType.Map:
                        fallbackIcon = controllerInfo.MapIcon ?? "CollabUtils2/lobbies/map";
                        break;
                    case FeatureType.Journal:
                        fallbackIcon = controllerInfo.JournalIcon ?? "CollabUtils2/lobbies/journal";
                        break;
                }

                if (string.IsNullOrWhiteSpace(value.Icon)) {
                    value.Icon = fallbackIcon;
                }

                if (value.Type == FeatureType.Map && !string.IsNullOrWhiteSpace(value.Map)) {
                    value.MapInfo = new MapInfo(value.Map);
                }

                value.CanWarpFrom = data.Bool("cu2map_canWarpFrom", fallbackWarpFrom);
                value.CanWarpTo = data.Bool("cu2map_canWarpTo", fallbackWarpTo);

                return true;
            }
        }

        public struct MapInfo {
            public string SID;
            public bool Completed;
            public int Difficulty;

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
            HeartSide,
        }
    }
}
