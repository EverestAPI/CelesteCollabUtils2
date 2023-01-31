using Celeste.Mod.CollabUtils2.Cutscenes;
using Celeste.Mod.CollabUtils2.Entities;
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

        // all lobbies in the collab
        private readonly List<LobbySelection> lobbySelections = new List<LobbySelection>();

        // all warps for the selected lobby
        private readonly List<LobbyMapController.FeatureInfo> allWarps = new List<LobbyMapController.FeatureInfo>();
        private readonly List<LobbyMapController.FeatureInfo> activeWarps = new List<LobbyMapController.FeatureInfo>();

        // current lobby setup
        private int selectedLobbyIndex;
        private int[] selectedWarpIndexes;
        private LobbyMapController.ControllerInfo lobbyMapInfo;
        private ByteArray2D visitedTiles;
        private LobbyVisitManager visitManager;
        private int heartCount;

        // resources
        private Texture2D mapTexture;
        private Texture2D overlayTexture;
        private VirtualRenderTarget renderTarget;
        private readonly List<Component> featureComponents = new List<Component>();
        private readonly MTexture arrowTexture = GFX.Gui["towerarrow"];
        private Sprite heartSprite;
        private Image playerIcon;
        private Image playerIconHair;
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
        private bool shouldCentreOrigin;
        private float scaleTimeRemaining;
        private float translateTimeRemaining;
        private const float scale_time_seconds = 0.3f;
        private const float translate_time_seconds = 0.3f;
        private int lastSelectedWarpIndex = -1;
        private float scaleMultiplier = 1f;
        private float finalScale => actualScale * scaleMultiplier;
        private float playerVisibleForceTime;
        private float playerVisibleIntervalOffset;

        private Rectangle bounds;

        private bool focused;

        private bool openedWithRevealMap;

        #endregion

        public LobbyMapUI() {
            Tag = Tags.PauseUpdate | Tags.HUD;
            Depth = Depths.FGTerrain - 2;
            Visible = false;

            const int top = 182, bottom = 80, left = 100, right = 100;
            bounds = new Rectangle(left, top, Engine.Width - left - right, Engine.Height - top - bottom);

            renderTarget = VirtualContent.CreateRenderTarget("CU2_LobbyMapUI", bounds.Width, bounds.Height);
            Add(selectWarpWiggler = Wiggler.Create(0.4f, 4f));
            Add(selectLobbyWiggler = Wiggler.Create(0.4f, 4f));
            Add(closeWiggler = Wiggler.Create(0.4f, 4f));
            Add(confirmWiggler = Wiggler.Create(0.4f, 4f));
            Add(zoomWiggler = Wiggler.Create(0.4f, 4f));

            Add(new BeforeRenderHook(beforeRender));
            Add(new Coroutine(mapFocusRoutine()));
        }

        #region Entity Overrides

        public override void Added(Scene scene) {
            base.Added(scene);

            openedWithRevealMap = CollabModule.Instance.SaveData.RevealMap;
            
            if (!(scene is Level level)) return;

            getLobbyControllers(level);
            updateSelectedLobby(true);

            openScreen();
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);
            renderTarget?.Dispose();
            overlayTexture?.Dispose();

            renderTarget = null;
            overlayTexture = null;
            mapTexture = null;
        }

        public override void Update() {
            base.Update();

            // handle input
            if (focused) {
                if (activeWarps.Count > 0) {
                    if (Input.MenuUp.Pressed) {
                        if (selectedWarpIndexes[selectedLobbyIndex] > 0) {
                            Audio.Play("event:/ui/main/rollover_up");
                            selectWarpWiggler.Start();
                            selectedWarpIndexes[selectedLobbyIndex]--;
                            resetPlayerFlash();
                        }
                    } else if (Input.MenuDown.Pressed) {
                        if (selectedWarpIndexes[selectedLobbyIndex] < activeWarps.Count - 1) {
                            Audio.Play("event:/ui/main/rollover_down");
                            selectWarpWiggler.Start();
                            selectedWarpIndexes[selectedLobbyIndex]++;
                            resetPlayerFlash();
                        }
                    }
                }

                if (Input.MenuLeft.Pressed) {
                    if (selectedLobbyIndex > 0) {
                        Audio.Play("event:/ui/main/rollover_up");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex--;
                        updateSelectedLobby();
                        resetPlayerFlash();
                    }
                } else if (Input.MenuRight.Pressed) {
                    if (selectedLobbyIndex < lobbySelections.Count - 1) {
                        Audio.Play("event:/ui/main/rollover_down");
                        selectLobbyWiggler.Start();
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex++;
                        updateSelectedLobby();
                        resetPlayerFlash();
                    }
                }

                if (Input.MenuJournal.Pressed) {
                    Audio.Play("event:/ui/main/rollover_down");
                    zoomWiggler.Start();
                    zoomLevel--;
                    if (zoomLevel < 0) {
                        zoomLevel = zoomLevels.Length - 1;
                    }

                    targetScale = zoomLevels[zoomLevel];
                    scaleTimeRemaining = scale_time_seconds;
                    shouldCentreOrigin = zoomLevel == 0;

                    if (shouldCentreOrigin || zoomLevel == zoomLevels.Length - 1) {
                        targetOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                        translateTimeRemaining = translate_time_seconds;
                    }
                    resetPlayerFlash();
                }

                var close = false;
                var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
                if (Input.MenuConfirm.Pressed && warpIndex >= 0 && warpIndex < activeWarps.Count) {
                    var warp = activeWarps[warpIndex];
                    confirmWiggler.Start();
                    teleportToWarp(warp, "Fade", 0.5f);
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

            // update flashing player
            if (Scene?.Tracker.GetEntity<Player>() is Player player && playerIcon != null) {
                playerIconHair.Color = player.Hair.Color;
                if (playerVisibleForceTime > 0) {
                    playerVisibleForceTime -= Engine.RawDeltaTime;
                    if (playerVisibleForceTime <= 0) {
                        playerVisibleIntervalOffset = Scene.RawTimeActive;
                    }
                } else if (Scene.OnRawInterval(0.3f, playerVisibleIntervalOffset)) {
                    playerIcon.Visible = playerIconHair.Visible = !playerIcon.Visible;
                }
            }
            
            // update map position for warp selection
            if (activeWarps.Count > 0 && lastSelectedWarpIndex != selectedWarpIndexes[selectedLobbyIndex]) {
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
            return new Vector2(tileX / (overlayTexture?.Width ?? 1), tileY / (overlayTexture?.Height ?? 1));
        }
        
        /// <summary>
        /// Loads all LobbyMapController data from maps in this levelset, if at least one warp has been unlocked. 
        /// </summary>
        private void getLobbyControllers(Level level) {
            // we can only return lobbies that have at least one warp unlocked, or this one
            var collabName = level.Session.Area.SID.Substring(0, level.Session.Area.SID.IndexOf("/", StringComparison.Ordinal) + 1);
            var visitedLobbySIDs = CollabModule.Instance.SaveData.VisitedLobbyPositions.Keys.Where(k => k.StartsWith(collabName)).ToList();
            var thisLobbyKey = $"{level.Session.Area.SID}.{level.Session.Level}";
            if (!visitedLobbySIDs.Contains(thisLobbyKey)) {
                visitedLobbySIDs.Add(thisLobbyKey);
            }
            
            // parse all the features in all the lobbies that have been visited
            lobbySelections.Clear();
            foreach (var key in visitedLobbySIDs) {
                var room = string.Empty;
                var sid = key;
                if (key.LastIndexOf('.') > key.LastIndexOf('/')) {
                    room = key.Substring(key.LastIndexOf('.') + 1);
                    sid = key.Substring(0, key.LastIndexOf('.'));
                }
                
                var mapData = AreaData.Get(sid)?.Mode.FirstOrDefault()?.MapData;
                if (mapData == null) continue;
                
                const string mapControllerName = "CollabUtils2/LobbyMapController";
                var levelData = string.IsNullOrWhiteSpace(room) ? null : mapData.Get(room);
                var entityData = levelData == null
                    ? mapData.Levels.Select(l => findEntityData(l, mapControllerName)).FirstOrDefault()
                    : findEntityData(levelData, mapControllerName);

                if (entityData != null) {
                    var selection = new LobbySelection(entityData, mapData);
                    var features = new List<LobbyMapController.FeatureInfo>();
                    foreach (var data in selection.Data.Level.Entities.Concat(selection.Data.Level.Triggers)) {
                        if (LobbyMapController.FeatureInfo.TryParse(data, selection.Info, out var value)) {
                            value.SID = selection.SID;
                            value.Room = selection.Room;
                            features.Add(value);
                        }
                    }
                    selection.Features = features.ToArray();
                    lobbySelections.Add(selection);
                }
            }

            // sort lobbies by their index
            lobbySelections.Sort((lhs, rhs) => lhs.Info.LobbyIndex - rhs.Info.LobbyIndex);

            // select the current lobby
            selectedLobbyIndex = lobbySelections.FindIndex(s => s.SID == level.Session.Area.SID);
            selectedWarpIndexes = new int[lobbySelections.Count];
        }

        /// <summary>
        /// Configures the UI for the currently selected lobby.
        /// </summary>
        public void updateSelectedLobby(bool first = false) {
            var selection = lobbySelections[selectedLobbyIndex];
            var features = lobbySelections[selectedLobbyIndex].Features;
            lobbyMapInfo = selection.Info;

            // get or create a visit manager
            visitManager = new LobbyVisitManager(selection.SID, selection.Room);

            // generate the 2d array of visited tiles
            visitedTiles = generateVisitedTiles(lobbyMapInfo, visitManager);
            
            // find warps
            allWarps.Clear();
            activeWarps.Clear();
            allWarps.AddRange(features.Where(f => f.Type == LobbyMapController.FeatureType.Warp).OrderBy(f => f.FeatureId));
            activeWarps.AddRange(openedWithRevealMap ? allWarps : allWarps.Where(w => isVisited(w.Position)));

            // regenerate feature components
            featureComponents.ForEach(c => c.RemoveSelf());
            featureComponents.Clear();
            featureComponents.AddRange(features.Where(f => lobbyMapInfo.ShouldShowFeature(f)).Select(createFeatureComponent));
            featureComponents.ForEach(Add);
            
            // if this is the first time we've selected a lobby, select the nearest warp
            if (first && Engine.Scene is Level level && level.Tracker.GetEntity<Player>() is Player player) {
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
            }

            var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
            
            // get the map texture
            mapTexture = GFX.Gui[lobbyMapInfo.MapTexture].Texture.Texture;

            // generate the overlay texture
            overlayTexture?.Dispose();
            overlayTexture = new Texture2D(Engine.Instance.GraphicsDevice, lobbyMapInfo.RoomWidth, lobbyMapInfo.RoomHeight, false, SurfaceFormat.Alpha8);
            overlayTexture.SetData(visitedTiles.Data);

            // set view
            if (zoomLevel < 0) {
                zoomLevel = defaultZoomLevel;
            }
            zoomLevel = Calc.Clamp(zoomLevel, 0, zoomLevels.Length);
            actualScale = zoomLevels[zoomLevel];
            shouldCentreOrigin = zoomLevel == 0;
            selectedOrigin = warpIndex >= 0 && warpIndex < activeWarps.Count ? originForPosition(activeWarps[warpIndex].Position) : new Vector2(0.5f);
            actualOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
            translateTimeRemaining = 0f;
            scaleTimeRemaining = 0f;

            // calculate multiplier by aspect ratios
            var padded = bounds;
            padded.Inflate(-10, -10);
            var mapAspectRatio = (float) mapTexture.Width / mapTexture.Height;
            var boundsAspectRatio = (float) padded.Width / padded.Height;
            scaleMultiplier = mapAspectRatio > boundsAspectRatio ? (float) padded.Width / mapTexture.Width : (float) padded.Height / mapTexture.Height;

            // add player icon
            playerIcon?.RemoveSelf();
            playerIconHair?.RemoveSelf();
            Add(playerIcon = new Image(GFX.Gui["CollabUtils2/lobbyMap/player"]));
            playerIcon.CenterOrigin();
            Add(playerIconHair = new Image(GFX.Gui["CollabUtils2/lobbyMap/playerHair"]));
            playerIconHair.CenterOrigin();
            resetPlayerFlash();
            
            // update feature positions
            updateFeatures();
            
            // add heart component
            if (heartSprite != null && !lobbyMapInfo.ShowHeartCount) {
                heartSprite?.RemoveSelf();
                heartSprite = null;
            }
            else if (lobbyMapInfo.ShowHeartCount) {
                var heartPath = lobbyMapInfo.LobbyIndex <= 3
                    ? $"collectables/heartgem/{lobbyMapInfo.LobbyIndex - 1}/"
                    : $"CollabUtils2/crystalHeart/{(lobbyMapInfo.LobbyIndex == 4 ? "expert" : "grandmaster")}/";
                if (heartSprite == null || heartSprite.Path != heartPath) {
                    var lastFrame = heartSprite?.CurrentAnimationFrame ?? 0;
                    heartSprite?.RemoveSelf();
                    Add(heartSprite = new Sprite(GFX.Gui, heartPath));
                    heartSprite.CenterOrigin();
                    heartSprite.AddLoop("spin", "spin", 0.1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
                    heartSprite.Scale = Vector2.One / 2f;
                    heartSprite.Play("spin");
                    heartSprite.SetAnimationFrame(lastFrame);
                    heartSprite.Position = new Vector2(bounds.Left + 10, bounds.Top + 10);
                }

                var levelSetStats = SaveData.Instance.GetLevelSets().FirstOrDefault(ls => ls.Name == lobbyMapInfo.LevelSet);
                heartCount = levelSetStats?.TotalHeartGems ?? 0;
            }
        }

        /// <summary>
        /// Creates a component that represents the passed feature. Currently only supports an Image subclass.
        /// </summary>
        private Component createFeatureComponent(LobbyMapController.FeatureInfo featureInfo) {
            return new FeatureImage(featureInfo);
        }

        /// <summary>
        /// Forces the player icon to stop flashing for a short time.
        /// </summary>
        private void resetPlayerFlash() {
            playerIcon.Visible = playerIconHair.Visible = true;
            playerVisibleForceTime = 0.8f;
        }

        #endregion

        #region Rendering

        /// <summary>
        /// BeforeRenderHook that draws the map onto a RenderTarget.
        /// This allows unexplored areas to be transparent rather than using a fake black overlay.
        /// </summary>
        private void beforeRender() {
            // bail if there's nothing to draw
            if (renderTarget?.IsDisposed != false || overlayTexture?.IsDisposed != false || mapTexture?.IsDisposed != false) {
                return;
            }

            var position = new Vector2(bounds.Width / 2f, bounds.Height / 2f);
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
            if (!openedWithRevealMap) {
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
            var border = bounds;
            border.Inflate(margin + thickness, margin + thickness);

            // draw borders
            Draw.Rect(safeBounds.Left, safeBounds.Top, safeBounds.Width, bounds.Top - safeBounds.Top, Color.Black);
            Draw.Rect(safeBounds.Left, bounds.Bottom, safeBounds.Width, safeBounds.Bottom - bounds.Bottom, Color.Black);
            Draw.Rect(safeBounds.Left, bounds.Top - safety, bounds.Left - safeBounds.Left, bounds.Height + 2 * safety, Color.Black);
            Draw.Rect(bounds.Right, bounds.Top - safety, safeBounds.Right - bounds.Right, bounds.Height + 2 * safety, Color.Black);
            Draw.Rect(border.Left, border.Top, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Bottom - thickness, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Top, thickness, border.Height, Color.White);
            Draw.Rect(border.Right - thickness, border.Top, thickness, border.Height, Color.White);

            var lobby = lobbySelections[selectedLobbyIndex];
            var warpIndex = selectedWarpIndexes[selectedLobbyIndex];
            var title = Dialog.Clean(lobby.SID);
            var colorAlpha = 1f;

            // draw lobby title and arrows
            ActiveFont.DrawEdgeOutline(title, new Vector2(Celeste.TargetWidth / 2f, 80f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray * colorAlpha, 4f, Color.DarkSlateBlue * colorAlpha, 2f, Color.Black * colorAlpha);
            if (selectedLobbyIndex > 0) {
                arrowTexture.DrawCentered(new Vector2(960f - ActiveFont.Measure(title).X - 100f, 80f), Color.White * colorAlpha);
            }
            if (selectedLobbyIndex < lobbySelections.Count - 1) {
                arrowTexture.DrawCentered(new Vector2(960f + ActiveFont.Measure(title).X + 100f, 80f), Color.White * colorAlpha, 1f, (float) Math.PI);
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
            if (warpIndex >= 0 && warpIndex < activeWarps.Count && !string.IsNullOrWhiteSpace(activeWarps[warpIndex].DialogKey)) {
                var clean = Dialog.Clean(activeWarps[warpIndex].DialogKey);
                ActiveFont.DrawOutline(clean, new Vector2(bounds.Center.X, bounds.Bottom - 30f), new Vector2(0.5f), Vector2.One, Color.White, 2f, Color.Black);
            }

            // draw controls
            var changeDestinationLabel = Dialog.Clean("collabutils2_lobbymap_change_destination");
            var changeLobbyLabel = Dialog.Clean("collabutils2_lobbymap_change_lobby");
            var closeLabel = Dialog.Clean("collabutils2_lobbymap_close");
            var confirmLabel = Dialog.Clean("collabutils2_lobbymap_confirm");
            var zoomLabel = Dialog.Clean("collabutils2_lobbymap_zoom");
            const float buttonScale = 0.5f;
            const float xOffset = 32f, yOffset = 45f;
            const float wiggleAmount = 0.05f;
            
            var buttonPosition = new Vector2(bounds.Left + xOffset, bounds.Bottom + yOffset);
            
            if (activeWarps.Count > 1) {
                renderDoubleButton(buttonPosition, changeDestinationLabel, Input.MenuUp, Input.MenuDown,
                    buttonScale, true, true, 0f, selectWarpWiggler.Value * wiggleAmount);
                buttonPosition.X += measureDoubleButton(changeDestinationLabel, Input.MenuUp, Input.MenuDown) / 2f + xOffset;
            }

            if (lobbySelections.Count > 1) {
                renderDoubleButton(buttonPosition, changeLobbyLabel, Input.MenuLeft, Input.MenuRight,
                    buttonScale, selectedLobbyIndex > 0, selectedLobbyIndex < lobbySelections.Count - 1, 0f, selectLobbyWiggler.Value * wiggleAmount);
            }

            var closeWidth = ButtonUI.Width(closeLabel, Input.MenuCancel);
            var confirmWidth = ButtonUI.Width(confirmLabel, Input.MenuConfirm);
            buttonPosition.X = bounds.Right + xOffset / 2f;
            ButtonUI.Render(buttonPosition, closeLabel, Input.MenuCancel, buttonScale, 1f, closeWiggler.Value * wiggleAmount);
            buttonPosition.X -= closeWidth / 2f + xOffset;
            ButtonUI.Render(buttonPosition, confirmLabel, Input.MenuConfirm, buttonScale, 1f, confirmWiggler.Value * wiggleAmount);
            buttonPosition.X -= confirmWidth / 2f + xOffset;
            ButtonUI.Render(buttonPosition, zoomLabel, Input.MenuJournal, buttonScale, 1f, zoomWiggler.Value * wiggleAmount);
        }

        /// <summary>
        /// Draws the render target to the screen.
        /// </summary>
        private void drawMap() {
            if (renderTarget?.IsDisposed != false) return;
            Draw.SpriteBatch.Draw(renderTarget, new Vector2(bounds.Left, bounds.Top), Color.White);
        }

        /// <summary>
        /// Ensures feature representations have the right position and scale.
        /// </summary>
        private void updateFeatures() {
            // for now we assume features are all Images
            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;
            var scaleOffset = zoomLevels[1] - actualScale;
            var imageScale = scaleOffset <= 0 ? 1f : Calc.LerpClamp(1f, 0.75f, scaleOffset / (zoomLevels[1] - zoomLevels[0]));

            // move and scale features
            foreach (FeatureImage image in featureComponents) {
                var origin = originForPosition(image.Info.Position);
                var originOffset = origin - actualOrigin;
                image.Position = new Vector2(bounds.Center.X + originOffset.X * actualWidth, bounds.Center.Y + originOffset.Y * actualHeight);
                image.Scale = new Vector2(imageScale);
                image.Visible = openedWithRevealMap || isVisited(image.Info.Position);
            }

            // move the player icon to the currently selected warp
            if (playerIcon != null) {
                var selectedOriginOffset = selectedOrigin - actualOrigin;
                playerIcon.Position = playerIconHair.Position = new Vector2(bounds.Center.X + selectedOriginOffset.X * actualWidth, bounds.Center.Y + selectedOriginOffset.Y * actualHeight);
            }
        }

        #endregion

        #region Lifetime

        /// <summary>
        /// Opens the screen with PauseLock.
        /// </summary>
        private void openScreen() {
            if (Scene is Level level) {
                level.PauseLock = true;

                if (level.Tracker.GetEntity<Player>() is Player player) {
                    player.StateMachine.State = Player.StDummy;
                }

                Audio.Play(SFX.ui_game_pause);
                Add(new Coroutine(transitionRoutine(onFadeOut: () => {
                    Visible = true;
                })));
            }
        }

        /// <summary>
        /// Closes the screen and resets PauseLock.
        /// </summary>
        private void closeScreen() {
            Audio.Play(SFX.ui_game_unpause);
            Add(new Coroutine(transitionRoutine(onFadeOut: () => {
                Visible = false;
            }, onFadeIn: () => {
                if (Scene is Level level) {
                    level.PauseLock = false;

                    if (level.Tracker.GetEntity<Player>() is Player player) {
                        player.StateMachine.State = Player.StNormal;
                    }
                }

                RemoveSelf();
            })));
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

                updateFeatures();

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
        /// Teleports to the selected warp within the current map.
        /// </summary>
        private void teleportToWarp(LobbyMapController.FeatureInfo warp, string wipeType, float wipeDuration) {
            if (Scene is Level level && level.Tracker.GetEntity<Player>() is Player player) {
                if (warp.SID == level.Session.Area.SID) {
                    level.Add(new TeleportCutscene(player, warp.Room, warp.Position, 0, 0, true, 0f, wipeType, wipeDuration));
                } else {
                    var targetAreaId = AreaData.Areas.FirstOrDefault(a => a.SID == warp.SID)?.ID ?? level.Session.Area.ID;

                    ScreenWipe wipe = null;
                    if (typeof(Celeste).Assembly.GetType($"Celeste.{wipeType}Wipe") is Type type) {
                        wipe = (ScreenWipe) Activator.CreateInstance(type, level, false, new Action(() => teleportToChapter(targetAreaId, warp.Room, warp.Position)));
                    } else {
                        wipe = new FadeWipe(level, false, () => teleportToChapter(targetAreaId, warp.Room, warp.Position));
                    }

                    wipe.Duration = Math.Min(1.35f, wipeDuration);
                }
            }
        }

        /// <summary>
        /// Teleports to the selected chapter with room and position.
        /// </summary>
        private static void teleportToChapter(int areaId, string room, Vector2 position) {
            var levelData = AreaData.Get(new AreaKey(areaId)).Mode[0].MapData.Get(room);
            var session = new Session(new AreaKey(areaId)) {
                Level = room,
                FirstLevel = false,
                RespawnPoint = levelData.Spawns.ClosestTo(levelData.Position + position),
            };
            LevelEnter.Go(session, fromSaveData: false);
        }

        /// <summary>
        /// Calculates the width of a double button.
        /// </summary>
        public static float measureDoubleButton(string label, VirtualButton button1, VirtualButton button2) {
            MTexture mTexture1 = Input.GuiButton(button1, "controls/keyboard/oemquestion");
            MTexture mTexture2 = Input.GuiButton(button2, "controls/keyboard/oemquestion");
            return ActiveFont.Measure(label).X + 8f + mTexture1.Width + mTexture2.Width;
        }

        /// <summary>
        /// Draws a double button.
        /// </summary>
        public static void renderDoubleButton(Vector2 position, string label, VirtualButton button1, VirtualButton button2, float scale, bool displayButton1, bool displayButton2, float justifyX = 0.5f, float wiggle = 0f, float alpha = 1f) {
            MTexture mTexture1 = Input.GuiButton(button1, "controls/keyboard/oemquestion");
            MTexture mTexture2 = Input.GuiButton(button2, "controls/keyboard/oemquestion");
            float num = ActiveFont.Measure(label).X + 8f + mTexture1.Width;
            position.X -= scale * num * (justifyX - 0.5f) + mTexture2.Width / 2;
            drawText(label, position, num / 2f, scale + wiggle, alpha);
            if (displayButton1 && !displayButton2) {
                mTexture1.Draw(position, new Vector2(mTexture1.Width - num / 2f, mTexture1.Height / 2f), Color.White * alpha, scale + wiggle);
            }

            if (!displayButton1 && displayButton2) {
                mTexture2.Draw(position, new Vector2(mTexture2.Width - num / 2f, mTexture2.Height / 2f), Color.White * alpha, scale + wiggle);
            }

            if (displayButton1 && displayButton2) {
                mTexture1.Draw(position, new Vector2(mTexture1.Width - num / 2f, mTexture1.Height / 2f), Color.White * alpha, scale + wiggle);
                mTexture2.Draw(position + new Vector2(mTexture1.Width / 2, 0f), new Vector2(mTexture2.Width - num / 2f, mTexture2.Height / 2f), Color.White * alpha, scale + wiggle);
            }
        }

        /// <summary>
        /// Draws text for a double button in the specified position.
        /// </summary>
        private static void drawText(string text, Vector2 position, float justify, float scale, float alpha) {
            float x = ActiveFont.Measure(text).X;
            ActiveFont.DrawOutline(text, position, new Vector2(justify / x, 0.5f), Vector2.One * scale, Color.White * alpha, 2f, Color.Black * alpha);
        }
        
        /// <summary>
        /// Find the first entity in the level data with the specified name.
        /// </summary>
        private static EntityData findEntityData(LevelData levelData, string entityName) =>
            levelData.Entities.FirstOrDefault(e => e.Name == entityName);
        
        #endregion

        #region Nested Classes

        /// <summary>
        /// Represents a map feature as an image.
        /// </summary>
        private class FeatureImage : Image {
            public readonly LobbyMapController.FeatureInfo Info;

            public FeatureImage(LobbyMapController.FeatureInfo info) : base(null) {
                Info = info;

                var icon = info.Icon;
                if (info.Type == LobbyMapController.FeatureType.Map) {
                    if (info.MapInfo.Difficulty >= 0 && GFX.Gui.Has(icon + info.MapInfo.Difficulty)) {
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
            public LobbyMapController.FeatureInfo[] Features;

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
