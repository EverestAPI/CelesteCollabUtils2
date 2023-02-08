using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
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
                VisitManager = new LobbyVisitManager(level.Session.Area.SID, level.Session.Level);
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

                if (!level.Paused && !level.Transitioning && VisitManager?.VisitedAll == false) {
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
            private static readonly char[] commaSeparator = {','};

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
            /// An array of custom entity names that should be considered <see cref="MarkerType.Map"/> markers.
            /// These entities must have a "map" attribute containing the SID of the target map.
            /// </summary>
            public string[] CustomMarkers;

            public string WarpIcon;
            public string RainbowBerryIcon;
            public string HeartGateIcon;
            public string GymIcon;
            public string MapIcon;
            public string JournalIcon;
            public string HeartSideIcon;

            public bool ShowWarps;
            public bool ShowRainbowBerry;
            public bool ShowHeartGate;
            public bool ShowGyms;
            public bool ShowMaps;
            public bool ShowJournals;
            public bool ShowHeartSide;
            public bool ShowHeartCount;

            #endregion

            public int RoomWidth;
            public int RoomHeight;
            public string LevelSet;

            public ControllerInfo(EntityData data, MapData mapData = null) {
                MapTexture = data.Attr("mapTexture");
                TotalMaps = data.Int("totalMaps");

                WarpIcon = data.Attr("warpIcon", "CollabUtils2/lobbies/warp");
                RainbowBerryIcon = data.Attr("rainbowBerryIcon", "CollabUtils2/lobbies/rainbowBerry");
                HeartGateIcon = data.Attr("heartGateIcon", "CollabUtils2/lobbies/heartgate");
                GymIcon = data.Attr("gymIcon", "CollabUtils2/lobbies/gym");
                MapIcon = data.Attr("mapIcon", "CollabUtils2/lobbies/map");
                JournalIcon = data.Attr("journalIcon", "CollabUtils2/lobbies/journal");
                HeartSideIcon = data.Attr("heartSideIcon", "CollabUtils2/lobbies/heartside");

                ShowWarps = data.Bool("showWarps", true);
                ShowRainbowBerry = data.Bool("showRainbowBerry", true);
                ShowHeartGate = data.Bool("showHeartGate", true);
                ShowGyms = data.Bool("showGyms", true);
                ShowMaps = data.Bool("showMaps", true);
                ShowJournals = data.Bool("showJournals", true);
                ShowHeartSide = data.Bool("showHeartSide", true);
                ShowHeartCount = data.Bool("showHeartCount", true);

                var customMarkers = data.Attr("customMarkers");
                CustomMarkers = !string.IsNullOrWhiteSpace(customMarkers) ? customMarkers.Split(commaSeparator, StringSplitOptions.RemoveEmptyEntries) : new string[0];

                if (RoomWidth <= 0) RoomWidth = data.Level.TileBounds.Width;
                if (RoomHeight <= 0) RoomHeight = data.Level.TileBounds.Height;
                if (mapData != null) {
                    LevelSet = LobbyHelper.GetLobbyLevelSet(mapData.Area.SID);
                }
            }

            public bool ShouldShowMarker(MarkerInfo marker) {
                switch (marker.Type) {
                    case MarkerType.Custom: return true;
                    case MarkerType.Warp: return ShowWarps;
                    case MarkerType.RainbowBerry: return ShowRainbowBerry;
                    case MarkerType.HeartGate: return ShowHeartGate;
                    case MarkerType.Gym: return ShowGyms;
                    case MarkerType.Map: return ShowMaps;
                    case MarkerType.Journal: return ShowJournals;
                    case MarkerType.HeartSide: return ShowHeartSide;
                    default: return true;
                }
            }
        }

        public struct MarkerInfo {
            /// <summary>
            /// The icon in the Gui atlas.
            /// </summary>
            public string Icon;

            /// <summary>
            /// A key into Dialog.Clean for markers that include a title.
            /// </summary>
            public string DialogKey;

            /// <summary>
            /// A unique id for this marker. Also used to identify and sort warps.
            /// </summary>
            public string MarkerId;

            /// <summary>
            /// The type of marker. This allows for filtering in the controller.
            /// </summary>
            public MarkerType Type;

            /// <summary>
            /// The position of the marker on the map.
            /// </summary>
            public Vector2 Position;

            /// <summary>
            /// The SID for the lobby map.
            /// </summary>
            public string SID;

            /// <summary>
            /// The room within the map that this marker belongs to.
            /// </summary>
            public string Room;

            /// <summary>
            /// The name of the map to load when using a marker type of <see cref="MarkerType.Map"/>.
            /// </summary>
            public string Map;

            /// <summary>
            /// Extracted data about the referenced map if it exists.
            /// </summary>
            public MapInfo MapInfo;

            /// <summary>
            /// The wipe to use for warp markers.
            /// </summary>
            public string WipeType;

            public static bool TryParse(EntityData data, ControllerInfo controllerInfo, out MarkerInfo value) {
                value = default;

                // CU2 simple marker entity
                if (data.Name == "CollabUtils2/LobbyMapMarker") {
                    value.Type = MarkerType.Custom;
                    value.DialogKey = data.Attr("dialogKey");
                    value.Icon = data.Attr("icon");
                }
                // CU2 warp entity
                else if (data.Name == "CollabUtils2/LobbyMapWarp") {
                    value.Type = MarkerType.Warp;
                    value.DialogKey = data.Attr("dialogKey");
                    value.MarkerId = data.Attr("warpId");
                    value.Icon = data.Attr("icon");
                    value.WipeType = data.Attr("wipeType", "Celeste.Mountain");
                }
                // CU2 rainbow berry
                else if (data.Name == "CollabUtils2/RainbowBerry") {
                    value.Type = MarkerType.RainbowBerry;
                }
                // CU2 heart door
                else if (data.Name == "CollabUtils2/MiniHeartDoor") {
                    value.Type = MarkerType.HeartGate;
                }
                // CU2 journal trigger
                else if (data.Name == "CollabUtils2/JournalTrigger") {
                    value.Type = MarkerType.Journal;
                }
                // CU2 map trigger or something from the CustomMarkers property
                else if (data.Name == "CollabUtils2/ChapterPanelTrigger" || controllerInfo != null && controllerInfo.CustomMarkers.Contains(data.Name)) {
                    value.Map = data.Attr("map");
                    value.Type = LobbyHelper.IsCollabGym(value.Map) ? MarkerType.Gym : LobbyHelper.IsHeartSide(value.Map) ? MarkerType.HeartSide : MarkerType.Map;
                }
                // not a valid map marker, skip
                else {
                    return false;
                }

                // grab the entity's centre
                value.Position = new Vector2(data.Position.X + data.Width / 2f, data.Position.Y + data.Height / 2f);

                // if we haven't explictly set a marker id, default to the type and entity id
                if (string.IsNullOrWhiteSpace(value.MarkerId)) {
                    value.MarkerId = $"{value.Type}_{data.ID}";
                }

                // if we haven't explicitly set an icon, use the one defined in the controller for the given type
                // or fall back to some defaults
                if (string.IsNullOrWhiteSpace(value.Icon)) {
                    switch (value.Type) {
                        case MarkerType.Warp:
                            value.Icon = controllerInfo?.WarpIcon ?? "CollabUtils2/lobbies/warp";
                            break;
                        case MarkerType.RainbowBerry:
                            value.Icon = controllerInfo?.RainbowBerryIcon ?? "CollabUtils2/lobbies/rainbowBerry";
                            break;
                        case MarkerType.HeartGate:
                            value.Icon = controllerInfo?.HeartGateIcon ?? "CollabUtils2/lobbies/heartgate";
                            break;
                        case MarkerType.Gym:
                            value.Icon = controllerInfo?.GymIcon ?? "CollabUtils2/lobbies/gym";
                            break;
                        case MarkerType.Map:
                            value.Icon = controllerInfo?.MapIcon ?? "CollabUtils2/lobbies/map";
                            break;
                        case MarkerType.HeartSide:
                            value.Icon = controllerInfo?.HeartSideIcon ?? "CollabUtils2/lobbies/heartside";
                            break;
                        case MarkerType.Journal:
                            value.Icon = controllerInfo?.JournalIcon ?? "CollabUtils2/lobbies/journal";
                            break;
                    }
                }

                // if this marker represents an enterable map, try to read the data for that map
                if (value.Type == MarkerType.Map && !string.IsNullOrWhiteSpace(value.Map)) {
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

        public enum MarkerType {
            Warp,
            RainbowBerry,
            HeartGate,
            Gym,
            Map,
            Journal,
            HeartSide,
            Custom,
        }
    }
}
