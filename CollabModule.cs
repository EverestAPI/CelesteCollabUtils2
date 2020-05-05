using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using System;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabModule : EverestModule {

        public static CollabModule Instance;

        public override Type SessionType => typeof(CollabSession);
        public CollabSession Session => _Session as CollabSession;

        public CollabModule() {
            Instance = this;
        }

        public override void Load() {
            Everest.Events.Level.OnLoadEntity += OnLoadEntity;
            InGameOverworldHelper.Load();
            ReturnToLobbyHelper.Load();
            MiniHeartDoor.Load();
        }

        public override void Unload() {
            Everest.Events.Level.OnLoadEntity -= OnLoadEntity;
            InGameOverworldHelper.Unload();
            ReturnToLobbyHelper.Unload();
            MiniHeartDoor.Unload();
        }

        public override void LoadSession(int index, bool forceNew) {
            base.LoadSession(index, forceNew);

            if (forceNew) {
                ReturnToLobbyHelper.OnSessionCreated();
            }
        }

        private static bool OnLoadEntity(Level level, LevelData levelData, Vector2 offset, EntityData entityData) {
            // Allow using the Everest flag trigger with custom "commands" because I don't have time for Ahorn plugins. -ade
            if (entityData.Name == "everest/flagTrigger" && entityData.Attr("flag").StartsWith("/collab2:")) {
                string[] args = entityData.Attr("flag").Substring(9).Split(' ');
                switch (args[0]) {
                    case "mapswap":
                        level.Add(new MapSwapTrigger(entityData, offset) {
                            map = args.ElementAtOrDefault(1),
                            side = args.ElementAtOrDefault(2),
                            room = args.ElementAtOrDefault(3)
                        });
                        return true;

                    case "chapter":
                        level.Add(new ChapterPanelTrigger(entityData, offset) {
                            map = args.ElementAtOrDefault(1)
                        });
                        return true;

                    case "journal":
                        level.Add(new JournalTrigger(entityData, offset) {
                            levelset = args.ElementAtOrDefault(1)
                        });
                        return true;
                }
            }

            return false;
        }

    }
}
