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
    [Tracked]
    public class LobbyMapUI : Entity {
        #region Fields

        // all lobbies in the collab
        private readonly List<LobbySelection> lobbySelections = new List<LobbySelection>();

        // all warps for the selected lobby
        private List<LobbyMapController.FeatureInfo> allWarps;
        private List<LobbyMapController.FeatureInfo> activeWarps;

        // current lobby setup
        private int selectedLobbyIndex;
        private int[] selectedWarpIndexes;
        private LobbyMapController.ControllerInfo lobbyMapInfo;
        private readonly List<LobbyMapController.FeatureInfo> lobbyMapFeatures = new List<LobbyMapController.FeatureInfo>();
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

        // current view
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

        #endregion

        public LobbyMapUI() {
            Tag = Tags.PauseUpdate | Tags.HUD;
            Depth = Depths.FGTerrain - 2;
            Visible = false;

            bounds = new Rectangle(100, 182, Engine.Width - 2 * 100, Engine.Height - 2 * 182);

            renderTarget = VirtualContent.CreateRenderTarget("CU2_LobbyMapUI", bounds.Width, bounds.Height);

            Add(new BeforeRenderHook(beforeRender));
            Add(new Coroutine(mapFocusRoutine()));
        }

        private Vector2 originForPosition(Vector2 point) {
            var tileX = point.X / 8f;
            var tileY = point.Y / 8f;
            return new Vector2(tileX / (overlayTexture?.Width ?? 1), tileY / (overlayTexture?.Height ?? 1));
        }

        #region Entity Overrides

        public override void Added(Scene scene) {
            base.Added(scene);

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

            if (focused) {
                if (Input.MenuDown.Pressed) {
                    if (selectedWarpIndexes[selectedLobbyIndex] < activeWarps.Count - 1) {
                        Audio.Play("event:/ui/main/rollover_down");
                        selectedWarpIndexes[selectedLobbyIndex]++;
                    }
                } else if (Input.MenuUp.Pressed) {
                    if (selectedWarpIndexes[selectedLobbyIndex] > 0) {
                        Audio.Play("event:/ui/main/rollover_up");
                        selectedWarpIndexes[selectedLobbyIndex]--;
                    }
                }

                if (Input.MenuLeft.Pressed) {
                    if (selectedLobbyIndex > 0) {
                        Audio.Play("event:/ui/main/rollover_down");
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex--;
                        updateSelectedLobby();
                    }
                } else if (Input.MenuRight.Pressed) {
                    if (selectedLobbyIndex < lobbySelections.Count - 1) {
                        Audio.Play("event:/ui/main/rollover_up");
                        lastSelectedWarpIndex = -1;
                        selectedLobbyIndex++;
                        updateSelectedLobby();
                    }
                }

                if (Input.MenuJournal.Pressed) {
                    Audio.Play("event:/ui/main/rollover_down");
                    zoomLevel--;
                    if (zoomLevel < 0) {
                        zoomLevel = lobbyMapInfo.ZoomLevels.Length - 1;
                    }

                    targetScale = lobbyMapInfo.ZoomLevels[zoomLevel];
                    scaleTimeRemaining = scale_time_seconds;
                    shouldCentreOrigin = zoomLevel == 0;

                    if (shouldCentreOrigin || zoomLevel == lobbyMapInfo.ZoomLevels.Length - 1) {
                        targetOrigin = shouldCentreOrigin ? new Vector2(0.5f) : selectedOrigin;
                        translateTimeRemaining = translate_time_seconds;
                    }
                }

                var close = false;
                if (Input.MenuConfirm.Pressed) {
                    var warp = activeWarps[selectedWarpIndexes[selectedLobbyIndex]];
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
                    closeScreen();
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
            if (lastSelectedWarpIndex != selectedWarpIndexes[selectedLobbyIndex]) {
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

        private bool IsVisited(Vector2 position, byte threshold = 0x7F) =>
            visitedTiles.TryGet((int)(position.X / 8), (int)(position.Y / 8), out var value) && value > threshold;

        #region Lobby Configuration

        private void getLobbyControllers(Level level) {
            // we can only return lobbies that have at least one warp unlocked, or this one
            var collabName = level.Session.Area.SID.Substring(0, level.Session.Area.SID.IndexOf("/", StringComparison.Ordinal) + 1);
            var activeWarpLobbyKeys = CollabModule.Instance.SaveData.ActivatedLobbyWarps.Keys.Where(k => k.StartsWith(collabName)).ToList();
            if (!activeWarpLobbyKeys.Contains(level.Session.Area.SID)) {
                activeWarpLobbyKeys.Add(level.Session.Area.SID);
            }

            lobbySelections.Clear();

            foreach (var key in activeWarpLobbyKeys) {
                var mapData = AreaData.Get(key)?.Mode.FirstOrDefault()?.MapData;
                var entityData = mapData?.Levels.Select(l => findEntityData(l, LobbyMapController.ENTITY_NAME)).FirstOrDefault();

                if (entityData != null) {
                    lobbySelections.Add(new LobbySelection(entityData, mapData));
                }
            }

            lobbySelections.Sort((lhs, rhs) => lhs.Info.LobbyIndex - rhs.Info.LobbyIndex);

            selectedLobbyIndex = lobbySelections.FindIndex(s => s.SID == level.Session.Area.SID);
            selectedWarpIndexes = new int[lobbySelections.Count];
        }

        public void updateSelectedLobby(bool first = false) {
            var selection = lobbySelections[selectedLobbyIndex];
            lobbyMapInfo = selection.Info;

            // get the feature infos
            lobbyMapFeatures.Clear();
            foreach (var data in selection.Data.Level.Entities.Concat(selection.Data.Level.Triggers)) {
                if (LobbyMapController.FeatureInfo.TryParse(data, lobbyMapInfo, out var value)) {
                    value.SID = selection.SID;
                    value.Room = selection.Room;
                    lobbyMapFeatures.Add(value);
                }
            }

            // regenerate feature components
            featureComponents.ForEach(c => c.RemoveSelf());
            featureComponents.Clear();
            featureComponents.AddRange(lobbyMapFeatures.Where(f => lobbyMapInfo.ShouldShowFeature(f)).Select(createFeatureComponent));
            featureComponents.ForEach(Add);

            // find warps
            allWarps = lobbyMapFeatures.Where(f => f.Type == LobbyMapController.FeatureType.Warp).ToList();
            activeWarps = allWarps.ToList(); // TODO: only keep active
            
            // if this is the first time we've selected a lobby, select the nearest warp
            if (first && Engine.Scene is Level level && level.Tracker.GetEntity<Player>() is Player player) {
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
            
            var selectedWarp = activeWarps[selectedWarpIndexes[selectedLobbyIndex]];

            // get or create a visit manager
            visitManager = new LobbyVisitManager(selection.SID, selection.Room);

            // generate the 2d array of visited tiles
            visitedTiles = GenerateVisitedTiles(lobbyMapInfo, visitManager);

            // get the map texture
            mapTexture = GFX.Gui[lobbyMapInfo.MapTexture].Texture.Texture;

            // generate the overlay texture
            overlayTexture?.Dispose();
            overlayTexture = new Texture2D(Engine.Instance.GraphicsDevice, lobbyMapInfo.RoomWidth, lobbyMapInfo.RoomHeight, false, SurfaceFormat.Alpha8);
            overlayTexture.SetData(visitedTiles.Data);

            // set view
            if (zoomLevel < 0) {
                zoomLevel = lobbyMapInfo.DefaultZoomLevel;
            }
            zoomLevel = Calc.Clamp(zoomLevel, 0, lobbyMapInfo.ZoomLevels.Length);
            actualScale = lobbyMapInfo.ZoomLevels[zoomLevel];
            shouldCentreOrigin = zoomLevel == 0;
            selectedOrigin = originForPosition(selectedWarp.Position);
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
            Add(playerIcon = new Image(GFX.Gui["maps/player"]));
            playerIcon.CenterOrigin();
            Add(playerIconHair = new Image(GFX.Gui["maps/player_hair"]));
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

        private Component createFeatureComponent(LobbyMapController.FeatureInfo featureInfo) {
            return new FeatureImage(featureInfo);
        }

        private void resetPlayerFlash() {
            playerIcon.Visible = playerIconHair.Visible = true;
            playerVisibleForceTime = 0.8f;
        }

        #endregion

        #region Rendering

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

            Engine.Graphics.GraphicsDevice.SetRenderTarget(renderTarget);
            Engine.Graphics.GraphicsDevice.Clear(Color.Transparent);

            if (!CollabModule.Instance.SaveData.RevealMap) {
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState {
                    AlphaSourceBlend = Blend.One,
                    AlphaDestinationBlend = Blend.Zero,
                    ColorSourceBlend = Blend.Zero,
                    ColorDestinationBlend = Blend.Zero,
                });
                Draw.SpriteBatch.Draw(overlayTexture, destRect, Color.White);
                Draw.SpriteBatch.End();

                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, new BlendState {
                    AlphaSourceBlend = Blend.Zero,
                    AlphaDestinationBlend = Blend.One,
                    ColorSourceBlend = Blend.DestinationAlpha,
                    ColorDestinationBlend = Blend.Zero,
                });
            } else {
                Draw.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            }

            Draw.SpriteBatch.Draw(mapTexture, destRect, Color.White);
            Draw.SpriteBatch.End();

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

        private void drawBackground() {
            Draw.Rect(new Vector2(100, 180), 1720, 840, Color.Black * 0.9f);
        }

        private void drawForeground() {
            const int margin = 8;
            const int thickness = 8;
            const int safety = 10;
            var safeBounds = new Rectangle(-safety, -safety, Engine.Width + 2 * safety, Engine.Height + 2 * safety);
            var border = bounds;
            border.Inflate(margin + thickness, margin + thickness);

            Draw.Rect(safeBounds.Left, safeBounds.Top, safeBounds.Width, bounds.Top - safeBounds.Top, Color.Black);
            Draw.Rect(safeBounds.Left, bounds.Bottom, safeBounds.Width, safeBounds.Bottom - bounds.Bottom, Color.Black);
            Draw.Rect(safeBounds.Left, bounds.Top - safety, bounds.Left - safeBounds.Left, bounds.Height + 2 * safety, Color.Black);
            Draw.Rect(bounds.Right, bounds.Top - safety, safeBounds.Right - bounds.Right, bounds.Height + 2 * safety, Color.Black);
            Draw.Rect(border.Left, border.Top, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Bottom - thickness, border.Width, thickness, Color.White);
            Draw.Rect(border.Left, border.Top, thickness, border.Height, Color.White);
            Draw.Rect(border.Right - thickness, border.Top, thickness, border.Height, Color.White);

            var lobby = lobbySelections[selectedLobbyIndex];
            var warp = activeWarps[selectedWarpIndexes[selectedLobbyIndex]];
            var title = Dialog.Clean(lobby.SID);
            var colorAlpha = 1f;

            ActiveFont.DrawEdgeOutline(title, new Vector2(Celeste.TargetWidth / 2f, 80f), new Vector2(0.5f, 0.5f), Vector2.One * 2f, Color.Gray * colorAlpha, 4f, Color.DarkSlateBlue * colorAlpha, 2f, Color.Black * colorAlpha);

            if (selectedLobbyIndex > 0) {
                arrowTexture.DrawCentered(new Vector2(960f - ActiveFont.Measure(title).X - 100f, 80f), Color.White * colorAlpha);
            }

            if (selectedLobbyIndex < lobbySelections.Count - 1) {
                arrowTexture.DrawCentered(new Vector2(960f + ActiveFont.Measure(title).X + 100f, 80f), Color.White * colorAlpha, 1f, (float) Math.PI);
            }

            if (lobbyMapInfo.ShowHeartCount && heartSprite != null) {
                var heartCountColor = heartCount >= lobbyMapInfo.TotalMaps ? Color.Gold : Color.White;
                var heartText = $"{heartCount} / {lobbyMapInfo.TotalMaps}";
                var measured = ActiveFont.Measure(heartText);
                var position = new Vector2(heartSprite.Position.X + heartSprite.Width / 2f + 10f + measured.X / 2f, heartSprite.Position.Y + heartSprite.Height / 4f); 
                ActiveFont.DrawOutline(heartText, position, new Vector2(0.5f), Vector2.One, heartCountColor, 2f, Color.Black);
            }

            if (!string.IsNullOrWhiteSpace(warp.DialogKey)) {
                var clean = Dialog.Clean(warp.DialogKey);
                ActiveFont.DrawOutline(clean, new Vector2(bounds.Center.X, bounds.Bottom - 30f), new Vector2(0.5f), Vector2.One, Color.White, 2f, Color.Black);
            }
        }

        private void drawMap() {
            if (renderTarget?.IsDisposed != false) return;
            Draw.SpriteBatch.Draw(renderTarget, new Vector2(bounds.Left, bounds.Top), Color.White);
        }

        private void updateFeatures() {
            // for now we assume features are all Images because reasons
            var scale = finalScale;
            var actualWidth = mapTexture.Width * scale;
            var actualHeight = mapTexture.Height * scale;
            var scaleOffset = lobbyMapInfo.ZoomLevels[1] - actualScale;
            var imageScale = scaleOffset <= 0 ? 1f : Calc.LerpClamp(1f, 0.75f, scaleOffset / (lobbyMapInfo.ZoomLevels[1] - lobbyMapInfo.ZoomLevels[0]));
            var reveal = CollabModule.Instance.SaveData.RevealMap;

            foreach (FeatureImage image in featureComponents) {
                var origin = originForPosition(image.Info.Position);
                var originOffset = origin - actualOrigin;
                image.Position = new Vector2(bounds.Center.X + originOffset.X * actualWidth, bounds.Center.Y + originOffset.Y * actualHeight);
                image.Scale = new Vector2(imageScale);
                image.Visible = reveal || IsVisited(image.Info.Position);
            }

            if (playerIcon != null) {
                var selectedOriginOffset = selectedOrigin - actualOrigin;
                playerIcon.Position = playerIconHair.Position = new Vector2(bounds.Center.X + selectedOriginOffset.X * actualWidth, bounds.Center.Y + selectedOriginOffset.Y * actualHeight);
            }
        }

        #endregion

        #region Lifetime

        private void openScreen() {
            if (Scene is Level level) {
                level.PauseLock = true;
                level.Session.SetFlag("CU2_Lobby_Map_Opened", true);

                if (level.Tracker.GetEntity<Player>() is Player player) {
                    player.StateMachine.State = Player.StDummy;
                }

                Audio.Play(SFX.ui_game_pause);
                Add(new Coroutine(transitionRoutine(onFadeOut: () => {
                    Visible = true;
                })));
            }
        }

        private void closeScreen() {
            Audio.Play(SFX.ui_game_unpause);
            Add(new Coroutine(transitionRoutine(onFadeOut: () => {
                Visible = false;
            }, onFadeIn: () => {
                if (Scene is Level level) {
                    level.PauseLock = false;
                    level.Session.SetFlag("CU2_Lobby_Map_Opened", false);

                    if (level.Tracker.GetEntity<Player>() is Player player) {
                        player.StateMachine.State = Player.StNormal;
                    }
                }

                RemoveSelf();
            })));
        }

        #endregion

        #region Routines

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

        private static ByteArray2D GenerateVisitedTiles(LobbyMapController.ControllerInfo config, LobbyVisitManager visitManager) {
            var circle = CreateCircleData(config.ExplorationRadius - 1, config.ExplorationRadius + 1);

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
        private static ByteArray2D CreateCircleData(int hardRadius, int softRadius) {
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

        private static void teleportToChapter(int areaId, string room, Vector2 position) {
            var levelData = AreaData.Get(new AreaKey(areaId)).Mode[0].MapData.Get(room);
            var session = new Session(new AreaKey(areaId)) {
                Level = room,
                FirstLevel = false,
                RespawnPoint = levelData.Spawns.ClosestTo(levelData.Position + position),
            };
            LevelEnter.Go(session, fromSaveData: false);
        }

        #endregion

        private EntityData findEntityData(LevelData levelData, string entityName) =>
            levelData.Entities.FirstOrDefault(e => e.Name == entityName);

        public class FeatureImage : Image {
            public LobbyMapController.FeatureInfo Info;

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

        private class LobbySelection {
            public LobbyMapController.ControllerInfo Info;
            public EntityData Data;
            public string SID;
            public string Room;

            public LobbySelection(EntityData data, MapData map) {
                Info = new LobbyMapController.ControllerInfo(data);
                Data = data;
                SID = map.Area.SID;
                Room = data.Level.Name;
            }
        }
    }
}
