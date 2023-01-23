using Microsoft.Xna.Framework;
using Monocle;
using System.Collections;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2.Cutscenes {
    class TeleportCutscene : CutsceneEntity {
        private readonly Player player;
        private readonly Vector2 spawnPoint;
        private readonly string room;
        private float timer;
        private int cameraX;
        private int cameraY;
        private bool cameraOnPlayer;
        private bool skipFirstWipe;
        private string wipeType;
        private float wipeDuration;
        private bool respawnAnim;
        private bool wakeUpAnim;
        private Vector2 spawnPosition;
        private bool oldRespawn;
        private bool useLevelWipe;
        private bool faceLeft;
        private float currentSpriterate;

        private static FieldInfo PlayerOnGround = typeof(Player).GetField("onGround", BindingFlags.Instance | BindingFlags.NonPublic);

        public TeleportCutscene(Player player, string room, Vector2 spawnPoint, int cameraX, int cameraY, bool cameraOnPlayer, float timer, string wipeType, float wipeDuration = 0.5f, bool skipFirstWipe = false,
            bool respawnAnim = false, bool useLevelWipe = false, bool wakeUpAnim = false, float spawnPositionX = 0f, float spawnPositionY = 0f, bool oldRespawn = false, bool faceLeft = false) : base(false) {
            Tag = Tags.FrozenUpdate;
            this.player = player;
            this.room = room;
            this.spawnPoint = spawnPoint;
            this.timer = timer;
            this.wipeType = wipeType;
            this.wipeDuration = wipeDuration;
            this.cameraX = cameraX;
            this.cameraY = cameraY;
            this.cameraOnPlayer = cameraOnPlayer;
            this.skipFirstWipe = skipFirstWipe;
            this.respawnAnim = respawnAnim;
            this.wakeUpAnim = wakeUpAnim;
            this.useLevelWipe = useLevelWipe;
            this.oldRespawn = oldRespawn;
            this.faceLeft = faceLeft;
            spawnPosition = new Vector2(spawnPositionX, spawnPositionY);
        }

        public override void OnBegin(Level level) {
            Add(new Coroutine(Cutscene(level)));
        }

        private IEnumerator Cutscene(Level level) {
            player.StateMachine.State = Player.StDummy;
            yield return null;
            while (timer > 0f) {
                yield return null;
                timer -= Engine.DeltaTime;
            }

            if (level.Wipe != null) {
                level.Wipe.Cancel();
            }

            if (!skipFirstWipe) {
                if (useLevelWipe) {
                    level.DoScreenWipe(false, () => EndCutscene(level));
                } else {
                    switch (wipeType) {
                        case "Spotlight":
                            SpotlightWipe WipeA = new SpotlightWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeA);
                            break;
                        case "Curtain":
                            CurtainWipe WipeB = new CurtainWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeB);
                            break;
                        case "Mountain":
                            MountainWipe WipeC = new MountainWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeC);
                            break;
                        case "Dream":
                            DreamWipe WipeD = new DreamWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeD);
                            break;
                        case "Starfield":
                            StarfieldWipe WipeE = new StarfieldWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeE);
                            break;
                        case "Wind":
                            WindWipe WipeF = new WindWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeF);
                            break;
                        case "Drop":
                            DropWipe WipeG = new DropWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeG);
                            break;
                        case "Fall":
                            FallWipe WipeH = new FallWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeH);
                            break;
                        case "KeyDoor":
                            KeyDoorWipe WipeI = new KeyDoorWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeI);
                            break;
                        case "Angled":
                            AngledWipe WipeJ = new AngledWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeJ);
                            break;
                        case "Heart":
                            HeartWipe WipeK = new HeartWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeK);
                            break;
                        case "Fade":
                            FadeWipe WipeL = new FadeWipe(level, false, () => EndCutscene(level)) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeL);
                            break;
                        default:
                            EndCutscene(level);
                            break;
                    }
                }
            } else {
                EndCutscene(level);
            }
        }

        public override void OnEnd(Level level) {
            level.OnEndOfFrame += () => {
                string Prefix = level.Session.Area.GetLevelSet();
                // XaphanModule.ModSaveData.SavedFlags.Add(Prefix + "_teleporting"); TODO
                Leader.StoreStrawberries(player.Leader);
                level.Remove(player);
                level.UnloadLevel();
                level.Session.Level = room;
                level.Session.FirstLevel = false;
                if (spawnPosition == Vector2.Zero && !oldRespawn) {
                    level.Session.RespawnPoint = level.GetSpawnPoint(new Vector2(level.Bounds.Left, level.Bounds.Top) + spawnPoint);
                }

                level.LoadLevel(respawnAnim ? Player.IntroTypes.Respawn : Player.IntroTypes.None);
                
                if (spawnPosition != Vector2.Zero) {
                    Player player2 = level.Tracker.GetEntity<Player>();
                    player2.Position = spawnPosition;
                    if (wakeUpAnim) {
                        player2.StateMachine.State = 11;
                        player2.DummyAutoAnimate = false;
                        currentSpriterate = player2.Sprite.Rate;
                        player2.Sprite.Play("wakeUp");
                        player2.Sprite.Rate = 2f;
                        player2.Sprite.OnLastFrame += RestaurePreviousRate;
                    }

                    player2.Facing = faceLeft ? Facings.Left : Facings.Right;

                    int safety = 184;
                    while (safety-- > 0 && !(bool)PlayerOnGround.GetValue(player2)) {
                        player2.MoveV(1);
                    }

                    if (safety == 0) {
                        player2.Position = level.Session.RespawnPoint.GetValueOrDefault();
                    }
                }

                level.Wipe?.Cancel();

                if (cameraOnPlayer) {
                    level.Camera.Position = level.Tracker.GetEntity<Player>()?.CameraTarget ?? Vector2.Zero;
                } else {
                    level.Camera.Position = new Vector2(level.Bounds.Left + cameraX, level.Bounds.Top + cameraY);
                }

                if (useLevelWipe) {
                    level.DoScreenWipe(true);
                } else {
                    switch (wipeType) {
                        case "Spotlight":
                            SpotlightWipe WipeA = new SpotlightWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeA);
                            break;
                        case "Curtain":
                            CurtainWipe WipeB = new CurtainWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeB);
                            break;
                        case "Mountain":
                            MountainWipe WipeC = new MountainWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeC);
                            break;
                        case "Dream":
                            DreamWipe WipeD = new DreamWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeD);
                            break;
                        case "Starfield":
                            StarfieldWipe WipeE = new StarfieldWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeE);
                            break;
                        case "Wind":
                            WindWipe WipeF = new WindWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeF);
                            break;
                        case "Drop":
                            DropWipe WipeG = new DropWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeG);
                            break;
                        case "Fall":
                            FallWipe WipeH = new FallWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeH);
                            break;
                        case "KeyDoor":
                            KeyDoorWipe WipeI = new KeyDoorWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeI);
                            break;
                        case "Angled":
                            AngledWipe WipeJ = new AngledWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeJ);
                            break;
                        case "Heart":
                            HeartWipe WipeK = new HeartWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeK);
                            break;
                        case "Fade":
                            FadeWipe WipeL = new FadeWipe(level, true) {
                                Duration = wipeDuration
                            };
                            level.Add(WipeL);
                            break;
                        default:
                            break;
                    }
                }

                Leader.RestoreStrawberries(level.Tracker.GetEntity<Player>().Leader);
                // XaphanModule.ModSaveData.SavedFlags.Remove(Prefix + "_teleporting"); TODO
                level.Tracker.GetEntity<Player>().StateMachine.State = Player.StNormal;
            };
        }

        private void RestaurePreviousRate(string s) {
            player.Sprite.Rate = currentSpriterate;
            player.StateMachine.State = Player.StNormal;
            player.DummyAutoAnimate = true;
        }
    }
}
