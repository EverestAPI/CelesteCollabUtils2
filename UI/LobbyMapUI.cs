using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.UI {
    /// <summary>
    /// Displays a map allowing the player to warp between certain locations in lobbies.
    /// </summary>
    [Tracked]
    public class LobbyMapUI : Entity {
        #region Fields

        // input handling
        private static VirtualJoystick lobbyMapJoystick;
        private static VirtualButton lobbyMapUpButton;
        private static VirtualButton lobbyMapDownButton;
        private static VirtualButton lobbyMapLeftButton;
        private static VirtualButton lobbyMapRightButton;

        // cached button render info
        private ButtonHelper.ButtonRenderInfo changeDestinationButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo changeLobbyButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo closeButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo confirmButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo zoomButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo holdToPanButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo panButtonRenderInfo;
        private ButtonHelper.ButtonRenderInfo aimButtonRenderInfo;

        // all lobbies in the collab
        private readonly List<LobbySelection> lobbySelections = new List<LobbySelection>();

        // all warps for the selected lobby
        private readonly List<LobbyMapController.MarkerInfo> activeWarps = new List<LobbyMapController.MarkerInfo>();

        // current lobby setup
        private int selectedLobbyIndex;
        private int[] selectedWarpIndexes;
        private LobbyMapController.ControllerInfo lobbyMapInfo;
        private ByteArray2D visitedTiles;
        private LobbyVisitManager visitManager;
        private int heartCount;
        private int initialLobbyIndex;
        private int initialWarpIndex;

        // resources
        private Texture2D mapTexture;
        private Texture2D overlayTexture;
        private VirtualRenderTarget renderTarget;
        private readonly List<Component> markerComponents = new List<Component>();
        private readonly MTexture arrowTexture = GFX.Gui["towerarrow"];
        private Sprite heartSprite;
        private Sprite maddyRunSprite;
        private readonly Wiggler selectWarpWiggler;
        private readonly Wiggler selectLobbyWiggler;
        private readonly Wiggler closeWiggler;
        private readonly Wiggler confirmWiggler;
        private readonly Wiggler zoomWiggler;

        // current view
        private readonly float[] zoomLevels = { 1f, 2f, 3f };
        private const int defaultZoomLevel = 1;
        private int zoomLevel = -1;
        private float actualScale;
        private Vector2 actualOrigin;
        private float targetScale = 1f;
        private Vector2 targetOrigin = Vector2.Zero;
        private Vector2 selectedOrigin = Vector2.Zero;
        private bool shouldShowMaddy;
        private bool shouldCentreOrigin;
        private float scaleTimeRemaining;
        private float translateTimeRemaining;
        private const float scale_time_seconds = 0.3f;
        private const float translate_time_seconds = 0.3f;
        private int lastSelectedWarpIndex = -1;
        private float scaleMultiplier = 1f;
        private float finalScale => actualScale * scaleMultiplier;

        private Rectangle windowBounds;
        private Rectangle mapBounds;

        private bool focused;
        private bool closing;

        private bool openedWithRevealMap;
        private readonly bool viewOnly;
        private Vector2 initialPlayerCenter;

        #endregion

        public LobbyMapUI(bool viewOnly = false) {
            Tag = Tags.PauseUpdate | Tags.HUD;
            Depth = Depths.FGTerrain - 2;
            Visible = false;

            if (lobbyMapJoystick == null) {
                lobbyMapJoystick = new VirtualJoystick(
                    CollabModule.Instance.Settings.PanLobbyMapUp.Binding,
                    CollabModule.Instance.Settings.PanLobbyMapDown.Binding,
                    CollabModule.Instance.Settings.PanLobbyMapLeft.Binding,
                    CollabModule.Instance.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0.1f);
                lobbyMapUpButton = new VirtualButton(CollabModule.Instance.Settings.PanLobbyMapUp.Binding, Input.Gamepad, 0f, 0.4f);
                lobbyMapDownButton = new VirtualButton(CollabModule.Instance.Settings.PanLobbyMapDown.Binding, Input.Gamepad, 0f, 0.4f);
                lobbyMapLeftButton = new VirtualButton(CollabModule.Instance.Settings.PanLobbyMapLeft.Binding, Input.Gamepad, 0f, 0.4f);
                lobbyMapRightButton = new VirtualButton(CollabModule.Instance.Settings.PanLobbyMapRight.Binding, Input.Gamepad, 0f, 0.4f);
            }

            this.viewOnly = viewOnly;

            const int top = 140, bottom = 80, left = 100, right = 100;
            const int topPadding = 10, bottomPadding = 40, leftPadding = 10, rightPadding = 10;
            windowBounds = new Rectangle(left, top, Engine.Width - left - right, Engine.Height - top - bottom);
            mapBounds = new Rectangle(windowBounds.Left + leftPadding, windowBounds.Top + topPadding, windowBounds.Width - leftPadding - rightPadding, windowBounds.Height - topPadding - bottomPadding);

            renderTarget = VirtualContent.CreateRenderTarget("CU2_LobbyMapUI", windowBounds.Width, windowBounds.Height);
            Add(selectWarpWiggler = Wiggler.Create(0.4f, 4f));
            Add(selectLobbyWiggler = Wiggler.Create(0.4f, 4f));
            Add(closeWiggler = Wiggler.Create(0.4f, 4f));
            Add(confirmWiggler = Wiggler.Create(0.4f, 4f));
            Add(zoomWiggler = Wiggler.Create(0.4f, 4f));

            Add(new BeforeRenderHook(beforeRender));
            Add(new Coroutine(mapFocusRoutine()));

            // cache render info
            changeDestinationButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_change_destination"), Input.MenuUp, Input.MenuDown, wiggler: selectWarpWiggler);
            changeLobbyButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_change_lobby"), Input.MenuLeft, Input.MenuRight, wiggler: selectLobbyWiggler);
            closeButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_close"), Input.MenuCancel, wiggler: closeWiggler);
            confirmButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_confirm"), Input.MenuConfirm, wiggler: confirmWiggler);
            zoomButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_zoom"), Input.MenuJournal, wiggler: zoomWiggler);
            holdToPanButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_hold_to_pan"), Input.Grab);

            // pan can be custom or aim
            panButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_pan"), lobbyMapUpButton, lobbyMapDownButton, lobbyMapLeftButton, lobbyMapRightButton);
            aimButtonRenderInfo = new ButtonHelper.ButtonRenderInfo(Dialog.Clean("collabutils2_lobbymap_pan"),
                new VirtualButton { Binding = Settings.Instance.Up },
                new VirtualButton { Binding = Settings.Instance.Down },
                new VirtualButton { Binding = Settings.Instance.Left },
                new VirtualButton { Binding = Settings.Instance.Right });
        }

        #region Entity Overrides

        public override void Added(Scene scene) {
            base.Added(scene);

            openedWithRevealMap = CollabModule.Instance.SaveData.RevealMap;

            if (scene is Level level && level.Tracker.GetEntity<Player>() is Player player) {
                initialPlayerCenter = player.Center;

                SetLocked(true);

                var path = player.Inventory.Backpack ? "marker/runBackpack" : "marker/runNoBackpack";
                Add(maddyRunSprite = new Sprite(MTN.Mountain, path));
                maddyRunSprite.Justify = new Vector2(0.5f, 1f);
                maddyRunSprite.Scale = new Vector2(0.3f);
                maddyRunSprite.Visible = false;
                maddyRunSprite.AddLoop("idle", "", 1 / 8f);
                maddyRunSprite.Play("idle");

                // find all the lobby controllers for this collab
                getLobbyControllers(level);

                // if we successfully selected a lobby to view, open the screen
                if (updateSelectedLobby(true)) {
                    openScreen();
                }
            }
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            renderTarget?.Dispose();
            overlayTexture?.Dispose();

            renderTarget = null;
            overlayTexture = null;
            mapTexture = null;

            SetLocked(false);
        }

        public override void Update() {
            base.Update();

            if (!CheckLocked()) {
                // something exploity happened, so bail
                closeScreen();
                return;
            }

            // handle input
            if (focused) {
                var holdToPan = Input.Grab.Check;
                var mainAim = Input.Aim.Value;
                var lobbyAim = lobbyMapJoystick.Value;
                var mainAiming = mainAim.LengthSquared() > float.Epsilon;

                // if holding pan and using the main aim method, use main aim, otherwise fall back to our custom aim
                var aim = holdToPan && mainAiming ? mainAim : lobbyAim;
                var aiming = aim.LengthSquared() > float.Epsilon;

                if (!viewOnly && activeWarps.Count > 0 && !holdToPan) {
                    int moveDir = 0;
                    if (Input.MenuUp.Pressed) {
                        if (!Input.MenuUp.Repeating && selectedWarpIndexes[selectedLobbyIndex] == 0) {
                            selectedWarpIndexes[selectedLobbyIndex] = activeWarps.Count - 1;
                            moveDir = -1;
                        } else if (selectedWarpIndexes[selectedLobbyIndex] > 0) {
                            selectedWarpIndexes[selectedLobbyIndex]--;
                            moveDir = -1;
                        }
                    } else if (Input.MenuDown.Pressed) {
                        if (!Input.MenuDown.Repeating && selectedWarpIndexes[selectedLobbyIndex] == activeWarps.Count - 1) {
                            selectedWarpIndexes[selectedLobbyIndex] = 0;
                            moveDir = 1;
                        } else if (selectedWarpIndexes[selectedLobbyIndex] < activeWarps.Count - 1) {
                            selectedWarpIndexes[selectedLobbyIndex]++;
                            moveDir = 1;
                        }
                    }

                    if (moveDir != 0) {
                        Audio.Play(moveDir < 0 ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
                        selectWarpWiggler.Start();
                    }
                }

                if (!holdToPan && Input.MenuLeft.Pressed) {
                    if (selectedLobbyIndex > 0) {
                        Audio.Play("event:/ui/main/rollover_up");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex--;
                        updateSelectedLobby();
                    }
                } else if (!holdToPan && Input.MenuRight.Pressed) {
                    if (selectedLobbyIndex < lobbySelections.Count - 1) {
                        Audio.Play("event:/ui/main/rollover_down");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex++;
                        updateSelectedLobby();
                    }
                } else if (Input.MenuJournal.Pressed) {
                    zoomWiggler.Start();
                    zoomLevel--;
                    if (zoomLevel < 0) {
                        zoomLevel = zoomLevels.Length - 1;
                        Audio.Play("event:/ui/main/rollover_up");
                    } else {
                        Audio.Play("event:/ui/main/rollover_down");
                    }

                    targetScale = zoomLevels[zoomLevel];
                    scaleTimeRemaining = scale_time_seconds;
                    shouldCentreOrigin = zoomLevel == 0;

                    if (shouldCentreOrigin || zoomLevel == zoomLevels.Length - 1) {
                        targetOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                        translateTimeRemaining = translate_time_seconds;
                    }
                } else if (!shouldCentreOrigin && translateTimeRemaining <= 0 && scaleTimeRemaining <= 0 && aiming && mapTexture != null) {
                    var aspectRatio = (float)mapTexture.Width / mapTexture.Height;
                    var offset = aim.SafeNormalize() * 2f / actualScale;
                    if (aspectRatio > 0) {
                        offset.X /= aspectRatio;
                    } else {
                        offset.Y *= aspectRatio;
                    }
                    var newOrigin = actualOrigin + offset * Engine.DeltaTime;
                    actualOrigin = newOrigin.Clamp(0, 0, 1, 1);

                    // update the selected warp if we should
                    if (!viewOnly && !shouldCentreOrigin) {
                        var nearestWarpIndex = nearestWarpIndexToActualOrigin();
                        if (nearestWarpIndex != selectedWarpIndexes[selectedLobbyIndex]) {
                            Audio.Play(nearestWarpIndex < selectedWarpIndexes[selectedLobbyIndex] ? "event:/ui/main/rollover_up" : "event:/ui/main/rollover_down");
                            selectedWarpIndexes[selectedLobbyIndex] = lastSelectedWarpIndex = nearestWarpIndex;
                            selectedOrigin = originForPosition(activeWarps[nearestWarpIndex].Position);
                        }
                    }

                    // make sure the markers are in the right place
                    updateMarkers();
                }

                var close = false;
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                if (!viewOnly && Input.MenuConfirm.Pressed && warpIndex >= 0 && warpIndex < activeWarps.Count) {
                    if (selectedLobbyIndex == initialLobbyIndex && warpIndex == initialWarpIndex) {
                        // confirming on the initial warp just closes the screen
                        close = true;
                    } else {
                        var warp = activeWarps[warpIndex];
                        confirmWiggler.Start();
                        teleportToWarp(warp);
                    }
                } else if (Input.MenuCancel.Pressed) {
                    close = true;
                } else if (Input.ESC.Pressed) {
                    close = true;
                    Input.ESC.ConsumeBuffer();
                } else if (Input.Pause.Pressed) {
                    close = true;
                    Input.Pause.ConsumeBuffer();
                }

                if (close) {
                    closeWiggler.Start();
                    closeScreen();
                    return;
                }
            }

            // update map position for warp selection if not view only
            if (!viewOnly && activeWarps.Count > 0 && lastSelectedWarpIndex != selectedWarpIndexes[selectedLobbyIndex]) {
                var warp = activeWarps[selectedWarpIndexes[selectedLobbyIndex]];
                selectedOrigin = originForPosition(warp.Position);

                if (lastSelectedWarpIndex < 0) {
                    actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                } else if (!shouldCentreOrigin) {
                    targetOrigin = selectedOrigin;
                    translateTimeRemaining = translate_time_seconds;
                }

                lastSelectedWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
            }
        }

        /// <summary>
        /// Finds the index into <see cref="activeWarps"/> that has the nearest warp to <see cref="actualOrigin"/>.
        /// </summary>
        private int nearestWarpIndexToActualOrigin() {
            int nearestIndex = -1;
            var nearestLengthSquared = float.MaxValue;
            for (int i = 0; i < activeWarps.Count; i++) {
                var warpOrigin = originForPosition(activeWarps[i].Position);
                var warpLengthSquared = (actualOrigin - warpOrigin).LengthSquared();
                if (warpLengthSquared < nearestLengthSquared) {
                    nearestLengthSquared = warpLengthSquared;
                    nearestIndex = i;
                }
            }
            return nearestIndex;
        }

        #endregion

        #region Lobby Configuration

        /// <summary>
        /// Returns true if the given position has been revealed in the current lobby.
        /// </summary>
        private bool isVisited(Vector2 position, byte threshold = 0x7F) =>
            visitedTiles.TryGet((int)(position.X / 8), (int)(position.Y / 8), out var value) && value > threshold;

        /// <summary>
        /// Calculates the correct origin within the overlay texture for a given level coordinate.
        /// </summary>
        /// <param name="point"></param>
        private Vector2 originForPosition(Vector2 point) {
            var tileX = point.X / 8f;
            var tileY = point.Y / 8f;
            return new Vector2(tileX / (lobbyMapInfo?.RoomWidth ?? 1), tileY / (lobbyMapInfo?.RoomHeight ?? 1));
        }

        /// <summary>
        /// Loads all LobbyMapController data from maps in this levelset, if at least one warp has been unlocked.
        /// </summary>
        private void getLobbyControllers(Level level) {
            // we can only return lobbies that have at least one visited position, or this one
            var collabName = LobbyHelper.GetCollabNameForSID(level.Session.Area.SID);
            var visitedLobbySIDs = CollabModule.Instance.SaveData.VisitedLobbyPositions.Keys.Where(k => k.StartsWith(collabName)).ToList();
            var thisLobbyKey = $"{level.Session.Area.SID}.{level.Session.Level}";
            if (!visitedLobbySIDs.Contains(thisLobbyKey)) {
                visitedLobbySIDs.Add(thisLobbyKey);
            }

            // parse all the markers in all the lobbies that have at least one warp activated
            lobbySelections.Clear();
            foreach (var key in visitedLobbySIDs) {
                // get the room and sid
                var room = string.Empty;
                var sid = key;
                if (key.LastIndexOf('.') > key.LastIndexOf('/')) {
                    room = key.Substring(key.LastIndexOf('.') + 1);
                    sid = key.Substring(0, key.LastIndexOf('.'));
                }

                // get the visit manager and skip if no warps found, unless it's this lobby
                if (key != thisLobbyKey) {
                    var lobbyVisitManager = getLobbyVisitManager(level, sid, room);
                    if (!lobbyVisitManager.ActivatedWarps.Any()) continue;
                }

                // get the map data from the lobby sid
                var mapData = AreaData.Get(sid)?.Mode.FirstOrDefault()?.MapData;
                if (mapData == null) continue;

                // find the map controller in the specified room, or the first in the map if no room specified
                const string mapControllerName = "CollabUtils2/LobbyMapController";
                var levelData = string.IsNullOrWhiteSpace(room) ? null : mapData.Get(room);
                var entityData = levelData == null
                    ? mapData.Levels.Select(l => findEntityData(l, mapControllerName)).FirstOrDefault()
                    : findEntityData(levelData, mapControllerName);

                // parse the markers in the room if a controller was found
                if (entityData != null) {
                    var selection = new LobbySelection(entityData, mapData);
                    var markers = new List<LobbyMapController.MarkerInfo>();
                    foreach (var data in selection.Data.Level.Entities.Concat(selection.Data.Level.Triggers)) {
                        if (LobbyMapController.MarkerInfo.TryParse(data, selection.Info, out var value)) {
                            value.SID = selection.SID;
                            value.Room = selection.Room;
                            markers.Add(value);
                        }
                    }
                    selection.Markers = markers.ToArray();
                    lobbySelections.Add(selection);
                }
            }

            // sort lobbies by SID
            lobbySelections.Sort((lhs, rhs) => string.Compare(lhs.SID, rhs.SID, StringComparison.Ordinal));
            selectedWarpIndexes = new int[lobbySelections.Count];

            // select the current lobby
            selectedLobbyIndex = lobbySelections.FindIndex(s => s.SID == level.Session.Area.SID);

            // verify selection
            if (selectedLobbyIndex < 0) {
                Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyMapUI", $"getLobbyControllers: Couldn't find map for {level.Session.Area.SID}, defaulting to first");
                selectedLobbyIndex = 0;
            }
        }

        /// <summary>
        /// Finds or creates a <see cref="LobbyVisitManager"/> for the specified lobby.
        /// </summary>
        private static LobbyVisitManager getLobbyVisitManager(Scene scene, string sid, string room) {
            if (scene.Tracker.GetEntity<LobbyMapController>() is LobbyMapController lmc &&
                (lmc.VisitManager?.MatchesKey(sid, room) ?? false)) {
                return lmc.VisitManager;
            }

            return new LobbyVisitManager(sid, room);
        }

        /// <summary>
        /// Configures the UI for the currently selected lobby.
        /// </summary>
        public bool updateSelectedLobby(bool first = false) {
            // validate lobby index
            if (selectedLobbyIndex < 0 || selectedLobbyIndex >= lobbySelections.Count) {
                Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyMapUI", $"updateSelectedLobby: Invalid lobby selection {selectedLobbyIndex}");
                return false;
            }

            if (!(Engine.Scene is Level level) || !(level.Tracker.GetEntity<Player>() is Player player)) {
                Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyMapUI", $"updateSelectedLobby: Couldn't find level or player");
                return false;
            }

            var selection = lobbySelections[selectedLobbyIndex];
            var markers = lobbySelections[selectedLobbyIndex].Markers;
            lobbyMapInfo = selection.Info;

            // get or create a visit manager
            visitManager = getLobbyVisitManager(Scene, selection.SID, selection.Room);

            // generate the 2d array of visited tiles
            if (openedWithRevealMap || visitManager.VisitedAll) {
                visitedTiles = new ByteArray2D(0, 0);
            } else {
                visitedTiles = generateVisitedTiles(lobbyMapInfo, visitManager);
                var visibleMarkers = markers.Where(m => isVisited(m.Position)).ToArray();
                if (visibleMarkers.Length == markers.Length && lobbyMapInfo.RevealWhenAllMarkersFound) {
                    visitManager.VisitAll();
                }
                markers = visibleMarkers;
            }

            // find warps
            activeWarps.Clear();
            activeWarps.AddRange(markers
                .Where(f => f.Type == LobbyMapController.MarkerType.Warp && (!f.WarpRequiresActivation || visitManager.ActivatedWarps.Contains(f.MarkerId))));

            // sort by marker id, comparing ints if possible
            activeWarps.Sort((lhs, rhs) =>
                int.TryParse(lhs.MarkerId.Trim(), out var lhsInt) && int.TryParse(rhs.MarkerId.Trim(), out var rhsInt)
                    ? Math.Sign(lhsInt - rhsInt)
                    : string.CompareOrdinal(lhs.MarkerId, rhs.MarkerId));

            // regenerate marker components
            var rainbowBerryUnlocked = isRainbowBerryUnlocked(lobbyMapInfo.LevelSet);
            markerComponents.ForEach(c => c.RemoveSelf());
            markerComponents.Clear();
            markerComponents.AddRange(markers
                .Where(f => {
                    if (!lobbyMapInfo.ShouldShowMarker(f)) return false;
                    if (f.Type == LobbyMapController.MarkerType.Warp && f.WarpRequiresActivation && !visitManager.ActivatedWarps.Contains(f.MarkerId)) return false;
                    if (f.Type == LobbyMapController.MarkerType.RainbowBerry && !rainbowBerryUnlocked) return false;
                    return true;
                })
                .OrderByDescending(f => f.Type)
                .Select(createMarkerComponent));
            markerComponents.ForEach(Add);

            // if this is the first time we've selected a lobby, select the nearest warp if not view only
            if (!viewOnly && first) {
                selectedWarpIndexes[selectedLobbyIndex] = 0;
                var nearestWarpLengthSquared = float.MaxValue;
                for (int i = 0; i < activeWarps.Count; i++) {
                    var warpPosition = activeWarps[i].Position + level.LevelOffset;
                    var lengthSquared = (warpPosition - player.Position).LengthSquared();
                    if (lengthSquared < nearestWarpLengthSquared) {
                        nearestWarpLengthSquared = lengthSquared;
                        selectedWarpIndexes[selectedLobbyIndex] = i;
                    }
                }

                // keep track of where we started
                initialLobbyIndex = selectedLobbyIndex;
                initialWarpIndex = selectedWarpIndexes[selectedLobbyIndex];
            }

            // get the map texture
            mapTexture = GFX.Gui[lobbyMapInfo.MapTexture].Texture.Texture;

            // generate the overlay texture if we should
            overlayTexture?.Dispose();
            overlayTexture = null;
            if (!openedWithRevealMap && !visitManager.VisitedAll) {
                overlayTexture = new Texture2D(Engine.Instance.GraphicsDevice, lobbyMapInfo.RoomWidth, lobbyMapInfo.RoomHeight, false, SurfaceFormat.Alpha8);
                overlayTexture.SetData(visitedTiles.Data);
            }

            // set view
            if (zoomLevel < 0) {
                zoomLevel = defaultZoomLevel;
            }
            zoomLevel = Calc.Clamp(zoomLevel, 0, zoomLevels.Length);
            actualScale = zoomLevels[zoomLevel];
            shouldCentreOrigin = zoomLevel == 0;

            var isCurrentLobby = selection.SID == level.Session.Area.SID;
            shouldShowMaddy = !viewOnly || isCurrentLobby;

            if (!viewOnly) {
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                selectedOrigin = warpIndex >= 0 && warpIndex < activeWarps.Count ? originForPosition(activeWarps[warpIndex].Position) : new Vector2(0.5f);
                actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
            } else if (isCurrentLobby) {
                selectedOrigin = originForPosition(player.Position - level.Bounds.Location.ToVector2());
                actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
            } else {
                selectedOrigin = actualOrigin = new Vector2(0.5f);
            }

            translateTimeRemaining = 0f;
            scaleTimeRemaining = 0f;

            // calculate multiplier by aspect ratios
            var padded = mapBounds;
            var mapAspectRatio = (float) mapTexture.Width / mapTexture.Height;
            var boundsAspectRatio = (float) padded.Width / padded.Height;
            scaleMultiplier = mapAspectRatio > boundsAspectRatio ? (float) padded.Width / mapTexture.Width : (float) padded.Height / mapTexture.Height;

            // update marker positions
            updateMarkers();

            // add heart component
            var heartFrame = heartSprite?.CurrentAnimationFrame ?? 0;
            heartSprite?.RemoveSelf();
            heartSprite = null;

            if (lobbyMapInfo.ShowHeartCount) {
                // try to get a custom id
                var id = InGameOverworldHelper.GetGuiHeartSpriteId(selection.SID, AreaMode.Normal);

                if (id == null) {
                    heartSprite = GFX.GuiSpriteBank.Create("heartgem0");
                } else {
                    heartSprite = InGameOverworldHelper.HeartSpriteBank.Create(id);
                }

                heartSprite.Scale = Vector2.One / 2f;
                heartSprite.Position = new Vector2(windowBounds.Left + 10, windowBounds.Top + 10);
                heartSprite.Justify = Vector2.Zero;
                heartSprite.Play("spin");
                heartSprite.SetAnimationFrame(heartFrame);
                heartSprite.JustifyOrigin(Vector2.Zero);
                Add(heartSprite);

                var levelSetStats = SaveData.Instance.GetLevelSets().FirstOrDefault(ls => ls.Name == lobbyMapInfo.LevelSet);
                heartCount = levelSetStats?.TotalHeartGems ?? 0;
            }

            return true;
        }

        /// <summary>
        /// Creates a component that represents the passed marker. Currently only supports an Image subclass.
        /// </summary>
        private Component createMarkerComponent(LobbyMapController.MarkerInfo markerInfo) {
            return new MarkerImage(markerInfo);
        }

        #endregion

        #region Rendering

        /// <summary>
        /// BeforeRenderHook that draws the map onto a RenderTarget.
        /// This allows unexplored areas to be transparent rather than using a fake black overlay.
        /// </summary>
        private void beforeRender() {
            // bail if there's nothing to draw
            if (renderTarget?.IsDisposed != false || mapTexture?.IsDisposed != false) {
                return;
            }

            var position = new Vector2(mapBounds.Center.X - windowBounds.Left, mapBounds.Center.Y - windowBounds.Top);
            var scale = finalScale;
            var destWidth = mapTexture.Width * scale;
            var destHeight = mapTexture.Height * scale;
            var destRect = new Rectangle(
                (int) (position.X - actualOrigin.X * destWidth), (int) (position.Y - actualOrigin.Y * destHeight),
                (int) destWidth, (int) destHeight);

            // set the render target and clear it
            Engine.Graphics.GraphicsDevice.SetRenderTarget(renderTarget);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

            // only draw the overlay if reveal map is not enabled
            if (!openedWithRevealMap && !visitManager.VisitedAll && overlayTexture?.IsDisposed == false) {
                // draw the exploration as a direct alpha channel
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState {
                    AlphaSourceBlend = Blend.One,
                    AlphaDestinationBlend = Blend.Zero,
                    ColorSourceBlend = Blend.Zero,
                    ColorDestinationBlend = Blend.Zero,
                });
                Draw.SpriteBatch.Draw(overlayTexture, destRect, Color.White);
                Draw.SpriteBatch.End();

                // begin a sprite batch that maintains the target alpha
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState {
                    AlphaSourceBlend = Blend.Zero,
                    AlphaDestinationBlend = Blend.One,
                    ColorSourceBlend = Blend.DestinationAlpha,
                    ColorDestinationBlend = Blend.Zero,
                });
            } else {
                // begin a sprite batch that overwrites the target alpha
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            }

            // draw the map
            Draw.SpriteBatch.Draw(mapTexture, destRect, Color.White);
            Draw.SpriteBatch.End();

            // draw a rectangle around the map if the console is open
            if (Engine.Commands.Open) {
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                Draw.HollowRect(destRect, Color.Red);
                Draw.SpriteBatch.End();
            }
        }

        public override void Render() {
            drawBackground();
            drawMap();

            base.Render();

            if (shouldShowMaddy) {
                maddyRunSprite?.Render();
            }

            if (CollabModule.Instance.SaveData.ShowVisitedPoints) {
                drawVisitedPoints();
            }

            drawForeground();
        }

        /// <summary>
        /// Draws a tinted background.
        /// </summary>
        private void drawBackground() {
            Draw.Rect(0, 0, Engine.Width, Engine.Height, Color.Black * 0.9f);
        }

        /// <summary>
        /// Draws all foreground components such as black borders, titles, heart count, etc.
        /// </summary>
        private void drawForeground() {
            const int margin = 8;
            const int thickness = 8;
            const int safety = 10;
            var safeBounds = new Rectangle(-safety, -safety, Engine.Width + 2 * safety, Engine.Height + 2 * safety);
            var border = windowBounds;
            border.Inflate(margin + thickness, margin + thickness);

            // draw borders
            Draw.Rect(safeBounds.Left, safeBounds.Top, safeBounds.Width, windowBounds.Top - safeBounds.Top, Color.Black);
            Draw.Rect(safeBounds.Left, windowBounds.Bottom, safeBounds.Width, safeBounds.Bottom - windowBounds.Bottom, Color.Black);
            Draw.Rect(safeBounds.Left, windowBounds.Top - safety, windowBounds.Left - safeBounds.Left, windowBounds.Height + 2 * safety, Color.Black);
            Draw.Rect(windowBounds.Right, windowBounds.Top - safety, safeBounds.Right - windowBounds.Right, windowBounds.Height + 2 * safety, Color.Black);
            Draw.Rect(border.Left, border.Top, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Bottom - thickness, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Top, thickness, border.Height, Color.White);
            Draw.Rect(border.Right - thickness, border.Top, thickness, border.Height, Color.White);

            var lobby = lobbySelections[selectedLobbyIndex];
            var title = Dialog.Clean(lobby.SID);
            var colorAlpha = 1f;

            // draw lobby title and arrows
            const float titleScale = 1.75f;
            const float titleArrowOffset = 100f;
            const float titleArrowScale = 0.9f;
            var titleWidth = ActiveFont.Measure(title).X * titleScale;
            var titleY = border.Top * 0.5f;
            ActiveFont.DrawEdgeOutline(title, new Vector2(Celeste.TargetWidth / 2f, titleY), new Vector2(0.5f, 0.5f), Vector2.One * titleScale, Color.Gray * colorAlpha, 4f, Color.DarkSlateBlue * colorAlpha, 2f, Color.Black * colorAlpha);
            if (selectedLobbyIndex > 0) {
                arrowTexture.DrawCentered(new Vector2(Celeste.TargetWidth / 2f - titleWidth / 2f - titleArrowOffset, titleY), Color.White * colorAlpha, titleArrowScale);
            }
            if (selectedLobbyIndex < lobbySelections.Count - 1) {
                arrowTexture.DrawCentered(new Vector2(Celeste.TargetWidth / 2f + titleWidth / 2f + titleArrowOffset, titleY), Color.White * colorAlpha, titleArrowScale, (float) Math.PI);
            }

            // draw heart count
            if (lobbyMapInfo.ShowHeartCount && heartSprite != null) {
                var heartCountColor = heartCount >= lobbyMapInfo.TotalMaps ? Color.Gold : Color.White;
                var heartText = $"{heartCount} / {lobbyMapInfo.TotalMaps}";
                var measured = ActiveFont.Measure(heartText);
                var position = new Vector2(heartSprite.Position.X + heartSprite.Width / 2f + 10f + measured.X / 2f, heartSprite.Position.Y + heartSprite.Height / 4f);
                ActiveFont.DrawOutline(heartText, position, new Vector2(0.5f), Vector2.One, heartCountColor, 2f, Color.Black);
            }

            // draw selected warp title
            if (!viewOnly) {
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                if (warpIndex >= 0 && warpIndex < activeWarps.Count && !string.IsNullOrWhiteSpace(activeWarps[warpIndex].DialogKey)) {
                    const float warpTitleOffset = -18f;
                    const float warpTitleAlpha = 1f;
                    var clean = Dialog.Clean(activeWarps[warpIndex].DialogKey);
                    ActiveFont.DrawOutline(clean, new Vector2(windowBounds.Center.X, windowBounds.Bottom + warpTitleOffset), new Vector2(0.5f), new Vector2(0.8f), Color.White * warpTitleAlpha, 2f, Color.Black);
                }
            }

            // draw controls
            const float buttonScale = 0.5f;
            const float disabledButtonAlpha = 0.4f;
            const float xOffset = 32f, yOffset = 45f;
            const float wiggleAmount = 0.05f;

            var buttonPosition = new Vector2(windowBounds.Left, windowBounds.Bottom + yOffset);
            var holdToPan = Input.Grab.Check;

            // draw change destination button
            if (!viewOnly && activeWarps.Count > 1 && !holdToPan) {
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, changeDestinationButtonRenderInfo, buttonScale, justifyX: 0f, wiggle: wiggleAmount);
            }

            // draw change lobby button
            if (lobbySelections.Count > 1 && !holdToPan) {
                changeLobbyButtonRenderInfo.Button1Alpha = selectedLobbyIndex > 0 ? 1f : disabledButtonAlpha;
                changeLobbyButtonRenderInfo.Button2Alpha = selectedLobbyIndex < lobbySelections.Count - 1 ? 1f : disabledButtonAlpha;
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, changeLobbyButtonRenderInfo, buttonScale, justifyX: 0f, wiggle: wiggleAmount);
            }

            buttonPosition.X = windowBounds.Right;

            // draw close button
            ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, closeButtonRenderInfo, buttonScale, justifyX: 1f, wiggle: wiggleAmount);

            // draw confirm button if not view only
            if (!viewOnly) {
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, confirmButtonRenderInfo, buttonScale, justifyX: 1f, wiggle: wiggleAmount);
            }

            // draw zoom button
            ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, zoomButtonRenderInfo, buttonScale, justifyX: 1f, wiggle: wiggleAmount);

            var panAlpha = shouldCentreOrigin ? disabledButtonAlpha : 1f;

            if (holdToPan) {
                // if we're holding pan, render the aim buttons as controls
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, aimButtonRenderInfo, buttonScale, panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            } else if (hasLatestBinding(CollabModule.Instance.Settings.PanLobbyMapUp.Binding,
                CollabModule.Instance.Settings.PanLobbyMapDown.Binding,
                CollabModule.Instance.Settings.PanLobbyMapLeft.Binding,
                CollabModule.Instance.Settings.PanLobbyMapRight.Binding)) {
                // draw custom pan inputs if at least one is bound
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, panButtonRenderInfo, buttonScale, panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            } else {
                // otherwise we draw the "hold to pan" input
                ButtonHelper.RenderMultiButton(ref buttonPosition, xOffset, holdToPanButtonRenderInfo, buttonScale, panAlpha, justifyX: 1f, wiggle: wiggleAmount);
            }
        }

        /// <summary>
        /// Draws the render target to the screen.
        /// </summary>
        private void drawMap() {
            if (renderTarget?.IsDisposed != false) return;
            Draw.SpriteBatch.Draw(renderTarget, new Vector2(windowBounds.Left, windowBounds.Top), Color.White);
        }

        /// <summary>
        /// Draw every visited point.
        /// </summary>
        private void drawVisitedPoints() {
            if (visitManager == null) return;

            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;

            foreach (var visitedPoint in visitManager.VisitedPoints) {
                var origin = originForPosition(visitedPoint.Point * Vector2.One * 8);
                var originOffset = origin - actualOrigin;
                var pointX = mapBounds.Center.X + originOffset.X * actualWidth;
                var pointY = mapBounds.Center.Y + originOffset.Y * actualHeight;
                Draw.Rect(pointX - 1, pointY - 1, 3, 3, Color.Red);
            }
        }

        /// <summary>
        /// Ensures marker representations have the right position and scale.
        /// </summary>
        private void updateMarkers() {
            // for now we assume markers are all Images
            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;
            var scaleOffset = zoomLevels[1] - actualScale;
            var imageScale = scaleOffset <= 0 ? 1f : Calc.LerpClamp(1f, 0.75f, scaleOffset / (zoomLevels[1] - zoomLevels[0]));

            // move and scale markers
            foreach (MarkerImage image in markerComponents) {
                var origin = originForPosition(image.Info.Position);
                var originOffset = origin - actualOrigin;
                image.Position = new Vector2(mapBounds.Center.X + originOffset.X * actualWidth, mapBounds.Center.Y + originOffset.Y * actualHeight);
                image.Scale = new Vector2(imageScale);
            }

            // move the player icon
            if (maddyRunSprite != null) {
                var selectedOriginOffset = selectedOrigin - actualOrigin;
                maddyRunSprite.Position = new Vector2(mapBounds.Center.X + selectedOriginOffset.X * actualWidth, mapBounds.Center.Y + selectedOriginOffset.Y * actualHeight);
            }
        }

        #endregion

        #region Lifetime

        /// <summary>
        /// Opens the screen.
        /// </summary>
        private void openScreen() {
            SetLocked(true, Scene);

            Audio.Play(SFX.ui_game_pause);

            Add(new Coroutine(transitionRoutine(onFadeOut: () => {
                Visible = true;
            })));
        }

        /// <summary>
        /// Closes the screen.
        /// </summary>
        private void closeScreen(bool force = false) {
            // don't try to close twice
            if (closing) return;
            closing = true;

            void DoClose() {
                SetLocked(false, Scene);
                RemoveSelf();
            }

            if (!force) {
                Audio.Play(SFX.ui_game_unpause);
                Add(new Coroutine(transitionRoutine(onFadeOut: () => Visible = false, onFadeIn: DoClose)));
            } else {
                Visible = false;
                DoClose();
            }
        }

        /// <summary>
        /// Teleports to the selected warp within the current map.
        /// </summary>
        private void teleportToWarp(LobbyMapController.MarkerInfo warp) {
            if (!(Scene is Level level)) return;

            focused = false;
            const float wipeDuration = 0.5f;

            Audio.Play("event:/game/04_cliffside/snowball_spawn");

            void onComplete() {
                closeScreen(true);
                if (warp.SID != level.Session.Area.SID) {
                    level.OnEndOfFrame += () => {
                        var areaId = AreaData.Areas.FirstOrDefault(a => a.SID == warp.SID)?.ID ?? level.Session.Area.ID;
                        var levelData = AreaData.Get(new AreaKey(areaId)).Mode[0].MapData.Get(warp.Room);
                        var session = new Session(new AreaKey(areaId)) {
                            Level = warp.Room,
                            FirstLevel = false,
                            RespawnPoint = levelData.Spawns.ClosestTo(levelData.Position + warp.Position),
                        };
                        LevelEnter.Go(session, fromSaveData: false);
                    };
                } else {
                    level.OnEndOfFrame += () => {
                        if (level.Tracker.GetEntity<Player>() is Player oldPlayer) {
                            Leader.StoreStrawberries(oldPlayer.Leader);
                            level.Remove(oldPlayer);
                        }

                        level.UnloadLevel();
                        level.Session.Level = warp.Room;
                        level.Session.FirstLevel = false;
                        level.Session.RespawnPoint = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top) + warp.Position);
                        level.LoadLevel(Player.IntroTypes.Respawn);
                        level.Wipe?.Cancel();

                        if (level.Tracker.GetEntity<Player>() is Player newPlayer) {
                            level.Camera.Position = newPlayer.CameraTarget;
                            Leader.RestoreStrawberries(newPlayer.Leader);
                        }

                        createWipe(warp.WipeType, wipeDuration, level, true);
                    };
                }
            }

            createWipe(warp.WipeType, wipeDuration, level, false, onComplete);
        }

        #endregion

        #region Routines

        /// <summary>
        /// Tweens the current scale and origin to expected values.
        /// </summary>
        private IEnumerator mapFocusRoutine() {
            float scaleFrom = actualScale;
            Vector2 translateFrom = actualOrigin;

            while (true) {
                if (scaleTimeRemaining == scale_time_seconds) scaleFrom = actualScale;
                if (translateTimeRemaining == translate_time_seconds) translateFrom = actualOrigin;

                if (scaleTimeRemaining > 0) {
                    actualScale = Calc.LerpClamp(scaleFrom, targetScale, Ease.QuintOut(1 - scaleTimeRemaining / scale_time_seconds));
                    scaleTimeRemaining -= Engine.DeltaTime;
                    if (scaleTimeRemaining <= 0) actualScale = targetScale;
                }

                if (translateTimeRemaining > 0) {
                    actualOrigin = Vector2.Lerp(translateFrom, targetOrigin, Ease.QuintOut(1 - translateTimeRemaining / translate_time_seconds));
                    translateTimeRemaining -= Engine.DeltaTime;
                    if (translateTimeRemaining <= 0) actualOrigin = targetOrigin;
                }

                updateMarkers();

                yield return null;
            }
        }

        /// <summary>
        /// Transitions the screen in and out with a fade wipe.
        /// </summary>
        private IEnumerator transitionRoutine(float duration = 0.5f, Action onFadeOut = null, Action onFadeIn = null) {
            duration = Math.Max(0f, duration);

            focused = false;

            yield return new FadeWipe(Scene, false) {
                Duration = duration / 2f,
                OnComplete = onFadeOut,
            }.Wait();

            yield return new FadeWipe(Scene, true) {
                Duration = duration / 2f,
                OnComplete = onFadeIn,
            }.Wait();

            focused = true;
        }

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// Generates a 2d array of bytes representing all visited tiles in the map.
        /// </summary>
        private static ByteArray2D generateVisitedTiles(LobbyMapController.ControllerInfo config, LobbyVisitManager visitManager) {
            var circle = createCircleData(LobbyVisitManager.EXPLORATION_RADIUS - 1, LobbyVisitManager.EXPLORATION_RADIUS + 1);

            var visitedTiles = new ByteArray2D(config.RoomWidth, config.RoomHeight);

            // apply a circle for each visited point
            foreach (var pos in visitManager.VisitedPoints) {
                visitedTiles.Max(circle, (int) pos.Point.X - circle.Width / 2, (int) pos.Point.Y - circle.Height / 2);
            }

            // fuzzy edges to remove the hard map boundary
            const byte quarter = byte.MaxValue / 4;
            for (int x = 0; x < config.RoomWidth; x++) {
                visitedTiles[x, 0] = 0;
                visitedTiles[x, 1] = Math.Min(visitedTiles[x, 1], quarter);
                visitedTiles[x, config.RoomHeight - 1] = 0;
                visitedTiles[x, config.RoomHeight - 2] = Math.Min(visitedTiles[x, config.RoomHeight - 2], quarter);
            }

            for (int y = 1; y < config.RoomHeight - 1; y++) {
                visitedTiles[0, y] = 0;
                visitedTiles[1, y] = Math.Min(visitedTiles[1, y], quarter);
                visitedTiles[config.RoomWidth - 1, y] = 0;
                visitedTiles[config.RoomWidth - 2, y] = Math.Min(visitedTiles[config.RoomWidth - 2, y], quarter);
            }

            return visitedTiles;
        }

        /// <summary>
        /// Generates a 2D array of bytes containing an antialiased circle.
        /// </summary>
        private static ByteArray2D createCircleData(int hardRadius, int softRadius) {
            var diameter = 2 * softRadius;
            var array = new ByteArray2D(diameter, diameter);

            for (int y = 0; y < diameter; y++) {
                for (int x = 0; x < diameter; x++) {
                    var lenSq = (softRadius - x) * (softRadius - x) + (softRadius - y) * (softRadius - y);
                    float alpha =
                        lenSq < hardRadius * hardRadius ? 0f :
                        lenSq > softRadius * softRadius ? 1f :
                        ((float) lenSq - hardRadius * hardRadius) / (softRadius * softRadius - hardRadius * hardRadius);
                    array[x, y] = (byte) ((1 - alpha) * byte.MaxValue);
                }
            }

            return array;
        }

        /// <summary>
        /// Creates a screen wipe animation.
        /// </summary>
        private static ScreenWipe createWipe(string wipeTypeName, float wipeDuration, Level level, bool wipeIn, Action onComplete = null) {
            Type wipeType = FakeAssembly.GetFakeEntryAssembly().GetType(wipeTypeName);
            if (wipeType == null) {
                Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyMapUI", $"Couldn't find wipe \"{wipeTypeName}\", falling back to Celeste.Mountain");
                wipeType = typeof(MountainWipe);
            }
            var screenWipe = (ScreenWipe) Activator.CreateInstance(wipeType, level, wipeIn, onComplete);
            if (screenWipe != null) screenWipe.Duration = wipeDuration;
            return screenWipe;
        }

        /// <summary>
        /// Checks whether any of the passed bindings have a valid button for the latest control scheme.
        /// </summary>
        private static bool hasLatestBinding(Binding binding1, Binding binding2 = null, Binding binding3 = null, Binding binding4 = null) {
            if (Input.GuiInputPrefix() == "keyboard") {
                return binding1?.Keyboard.Any() == true || binding2?.Keyboard.Any() == true || binding3?.Keyboard.Any() == true || binding4?.Keyboard.Any() == true;
            } else {
                return binding1?.Controller.Any() == true || binding2?.Controller.Any() == true || binding3?.Controller.Any() == true || binding4?.Controller.Any() == true;
            }
        }

        /// <summary>
        /// Find the first entity in the level data with the specified name.
        /// </summary>
        private static EntityData findEntityData(LevelData levelData, string entityName) =>
            levelData.Entities.FirstOrDefault(e => e.Name == entityName);

        /// <summary>
        /// Returns true if at least one silver berry has been collected for this lobby.
        /// </summary>
        private static bool isRainbowBerryUnlocked(string levelSet) {
            if (!CollabMapDataProcessor.SilverBerries.ContainsKey(levelSet)) return false;

            foreach (KeyValuePair<string, EntityID> requiredSilver in CollabMapDataProcessor.SilverBerries[levelSet]) {
                // check if the silver was collected.
                AreaStats stats = SaveData.Instance.GetAreaStatsFor(AreaData.Get(requiredSilver.Key).ToKey());
                if (stats.Modes[0].Strawberries.Contains(requiredSilver.Value)) return true;
            }

            return false;
        }

        /// <summary>
        /// Enforces removal of player control as much as possible.
        /// </summary>
        public static void SetLocked(bool locked, Scene scene = null, Player player = null) {
            // if we didn't pass in a scene and/or player, grab them ourselves
            Level level = (scene ?? Engine.Scene) as Level;
            player = player ?? level?.Tracker.GetEntity<Player>();
            if (level == null || player == null) return;

            level.CanRetry = !locked;
            level.PauseLock = locked;
            player.Speed = Vector2.Zero;
            player.DummyGravity = !locked;
            player.StateMachine.State = locked ? Player.StDummy : Player.StNormal;

            // disable auto animate if we're locking while not on the ground (happens while swimming)
            player.DummyAutoAnimate = !locked || player.OnGround();
        }

        /// <summary>
        /// Verifies that player control is still removed and there's no interaction storage we know of.
        /// </summary>
        public bool CheckLocked() {
            if (Scene is Level level && level.Tracker.GetEntity<Player>() is Player player) {
                // we really want to stop interaction storage, so if any of these checks fail, it's no longer valid to show the map
                return (initialPlayerCenter - player.Center).LengthSquared() < 16 * 16 &&
                    !player.Dead && !level.CanRetry && level.PauseLock && !player.DummyGravity &&
                    player.Speed == Vector2.Zero && player.StateMachine.State == Player.StDummy;
            }

            return false;
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Represents a map marker as an image.
        /// </summary>
        private class MarkerImage : Image {
            public readonly LobbyMapController.MarkerInfo Info;

            public MarkerImage(LobbyMapController.MarkerInfo info) : base(null) {
                Info = info;

                var icon = info.Icon;
                if (info.Type == LobbyMapController.MarkerType.Map || info.Type == LobbyMapController.MarkerType.HeartSide) {
                    if (info.Type != LobbyMapController.MarkerType.HeartSide && info.MapInfo.Difficulty >= 0 && GFX.Gui.Has(icon + info.MapInfo.Difficulty)) {
                        icon += info.MapInfo.Difficulty;
                    }
                    if (info.MapInfo.Completed && GFX.Gui.Has(icon + "Completed")) {
                        icon += "Completed";
                    }
                }

                Texture = GFX.Gui[icon];
                CenterOrigin();
            }
        }

        /// <summary>
        /// Aggregates information about the selected lobby.
        /// </summary>
        private class LobbySelection {
            public readonly LobbyMapController.ControllerInfo Info;
            public readonly EntityData Data;
            public readonly string SID;
            public readonly string Room;
            public LobbyMapController.MarkerInfo[] Markers;

            public LobbySelection(EntityData data, MapData map) {
                Info = new LobbyMapController.ControllerInfo(data, map);
                Data = data;
                SID = map.Area.SID;
                Room = data.Level.Name;
            }
        }

        #endregion
    }
}
