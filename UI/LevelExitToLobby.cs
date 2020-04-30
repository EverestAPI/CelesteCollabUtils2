using Microsoft.Xna.Framework;
using Monocle;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.UI {
    class LevelExitToLobby : Scene {
        private string targetSID;
        private string targetRoom;
        private Vector2? targetSpawnPoint;

        public LevelExitToLobby() : base() {
            Add(new HudRenderer());
        }

        public override void Begin() {
            base.Begin();

            targetSID = CollabModule.Instance.Session.LobbySID;
            targetRoom = CollabModule.Instance.Session.LobbyRoom;
            targetSpawnPoint = CollabModule.Instance.Session.LobbySpawnPoint;

            CollabModule.Instance.Session.LobbySID = null;
            CollabModule.Instance.Session.LobbyRoom = null;
            CollabModule.Instance.Session.LobbySpawnPoint = null;

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

            Session session = new Session(AreaData.Get(targetSID).ToKey());
            session.FirstLevel = false;
            session.StartedFromBeginning = false;
            session.Level = targetRoom;
            session.RespawnPoint = targetSpawnPoint;
            LevelLoader loader = new LevelLoader(session);
            Engine.Scene = loader;
        }
    }
}
