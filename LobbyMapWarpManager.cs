using Celeste.Mod.CollabUtils2.Cutscenes;
using Celeste.Mod.CollabUtils2.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    public static class LobbyMapWarpManager {
        public static List<WarpInfo> GetAllWarps(string lobbySid, EntityData entityData) {
            var warps = entityData.Level.Entities.Where(e => e.Name == LobbyMapWarp.ENTITY_NAME).ToArray();
            // var activeWarps = CollabModule.Instance.SaveData.ActivatedLobbyWarps.TryGetValue(lobbySid, out var warpList);
            // return activeWarps ? warps.Select(d => new WarpInfo(d)).ToList() : default;
            return warps.Select(d => new WarpInfo(d)).ToList();
        }
        
        public static void Teleport(WarpInfo warp, string wipeType, float wipeDuration)
        {
            if (Engine.Scene is Level level && level.Tracker.GetEntity<Player>() is Player player)
            {
                int currentAreaId = level.Session.Area.ID;
                if (warp.AreaId == currentAreaId)
                {
                    level.Add(new TeleportCutscene(player, warp.Room, warp.Position, 0, 0, true, 0f, wipeType, wipeDuration));
                }
                else
                {
                    // XaphanModule.ModSaveData.DestinationRoom = warp.Room;
                    // XaphanModule.ModSaveData.Spawn = warp.Position;
                    // XaphanModule.ModSaveData.Wipe = wipeType;
                    // XaphanModule.ModSaveData.WipeDuration = wipeDuration;

                    ScreenWipe wipe = null;
                    if (typeof(Celeste).Assembly.GetType($"Celeste.{wipeType}Wipe") is Type type)
                    {
                        wipe = (ScreenWipe)Activator.CreateInstance(type, new object[] {
                            level, false, new Action(() => TeleportToChapter(warp.AreaId))
                        });
                    }
                    else
                    {
                        wipe = new FadeWipe(level, false, new Action(() => TeleportToChapter(warp.AreaId)));
                    }

                    wipe.Duration = Math.Min(1.35f, wipeDuration);
                }
            }
        }
        
        private static void TeleportToChapter(int areaId)
        {
            if (Engine.Scene is Level level)
            {
                // if (XaphanModule.useMergeChaptersController && (level.Session.Area.LevelSet == "Xaphan/0" ? !XaphanModule.ModSaveData.SpeedrunMode : true))
                // {
                //     long currentTime = level.Session.Time;
                //     LevelEnter.Go(new Session(new AreaKey(areaId))
                //         {
                //             Time = currentTime,
                //             DoNotLoad = XaphanModule.ModSaveData.SavedNoLoadEntities[level.Session.Area.LevelSet],
                //             Strawberries = XaphanModule.ModSaveData.SavedSessionStrawberries[level.Session.Area.LevelSet]
                //         }
                //         , fromSaveData: false);
                // }
                // else
                {
                    LevelEnter.Go(new Session(new AreaKey(areaId)), fromSaveData: false);
                }
            }
        }

        public struct WarpInfo {
            public string ID;
            public string DialogKey;
            public int AreaId;
            public string Room;
            public Vector2 Position;
            public bool Active;

            public WarpInfo(EntityData data) {
                ID = data.Attr("warpId");
                DialogKey = data.Attr("dialogKey");
                AreaId = data.Int("areaId");
                Room = data.Attr("room");
                Position = data.Position;
                Active = false;
            }
            
            public WarpInfo(string id, string dialogKey = "", int areaId = 0, string room = "", Vector2 position = default) {
                ID = id;
                DialogKey = dialogKey;
                AreaId = areaId;
                Room = room;
                Position = position;
                Active = false;
            }
        }
    }
}
