using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.UI {
    class LevelExitToLobby : Scene {
        private string targetSID;
        private string targetRoom;
        private Vector2 targetSpawnPoint;
        private LevelExit.Mode exitMode;

        public LevelExitToLobby(LevelExit.Mode exitMode, Session currentSession) : base() {
            Add(new HudRenderer());

            // calling the LevelExit constructor triggers the Level.Exit Everest event, so that makes mods less confused about what's going on.
            new LevelExit(exitMode, currentSession);
            this.exitMode = exitMode;
        }

        public override void Begin() {
            base.Begin();

            targetSID = CollabModule.Instance.Session.LobbySID;
            targetRoom = CollabModule.Instance.Session.LobbyRoom;
            targetSpawnPoint = new Vector2(CollabModule.Instance.Session.LobbySpawnPointX, CollabModule.Instance.Session.LobbySpawnPointY);

            CollabModule.Instance.Session.LobbySID = null;
            CollabModule.Instance.Session.LobbyRoom = null;
            CollabModule.Instance.Session.LobbySpawnPointX = 0;
            CollabModule.Instance.Session.LobbySpawnPointY = 0;

            SaveLoadIcon.Show(this);
            Entity coroutineHolder;
            Add(coroutineHolder = new Entity());
            coroutineHolder.Add(new Coroutine(Routine()));
            Stats.Store();
            RendererList.UpdateLists();
        }

        private IEnumerator Routine() {
            UserIO.SaveHandler(file: true, settings: true);
            while (UserIO.Saving) {
                yield return null;
            }
            while (SaveLoadIcon.OnScreen) {
                yield return null;
            }

            if (targetSID == null) {
                // failsafe: if there is no lobby to return to, return to map instead.
                Engine.Scene = new OverworldLoader(
                    exitMode == LevelExit.Mode.Completed ? Overworld.StartMode.AreaComplete : Overworld.StartMode.AreaQuit);
            } else {
                Session session = new Session(AreaData.Get(targetSID).ToKey());
                session.FirstLevel = false;
                session.StartedFromBeginning = false;
                session.Level = targetRoom;
                session.RespawnPoint = targetSpawnPoint;
                new DynData<Session>(session)["pauseTimerUntilAction"] = true;
                LevelEnter.Go(session, false);
            }
        }
    }
}
