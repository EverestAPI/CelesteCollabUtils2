using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using System;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabModule : EverestModule {

        public static CollabModule Instance;

        public override Type SettingsType => typeof(CollabSettings);
        public CollabSettings Settings => _Settings as CollabSettings;

        public override Type SaveDataType => typeof(CollabSaveData);
        public CollabSaveData SaveData => _SaveData as CollabSaveData;

        public override Type SessionType => typeof(CollabSession);
        public CollabSession Session => _Session as CollabSession;

        public CollabModule() {
            Instance = this;
        }

        public override void Load() {
            Logger.SetLogLevel("CollabUtils2", LogLevel.Info);

            InGameOverworldHelper.Load();
            ReturnToLobbyHelper.Load();
            StrawberryHooks.Load();
            MiniHeartDoor.Load();
            LobbyHelper.Load();
            SpeedBerryTimerDisplay.Load();
            SpeedBerryPBInChapterPanel.Load();
            JournalTrigger.Load();
            CustomCrystalHeartHelper.Load();
            GoldenBerryPlayerRespawnPoint.Load();
        }

        public override void Unload() {
            InGameOverworldHelper.Unload();
            ReturnToLobbyHelper.Unload();
            StrawberryHooks.Unload();
            MiniHeartDoor.Unload();
            LobbyHelper.Unload();
            SpeedBerryTimerDisplay.Unload();
            SpeedBerryPBInChapterPanel.Unload();
            JournalTrigger.Unload();
            CustomCrystalHeartHelper.Unload();
            GoldenBerryPlayerRespawnPoint.Unload();
        }

        public override void LoadContent(bool firstLoad) {
            SilverBerry.LoadContent();
            RainbowBerry.LoadContent();
            SpeedBerry.LoadContent();
            InGameOverworldHelper.LoadContent();
        }

        public override void LoadSession(int index, bool forceNew) {
            base.LoadSession(index, forceNew);

            if (forceNew) {
                ReturnToLobbyHelper.OnSessionCreated();
                LobbyHelper.OnSessionCreated();
            }
        }

        public override void PrepareMapDataProcessors(MapDataFixup context) {
            base.PrepareMapDataProcessors(context);

            context.Add<CollabMapDataProcessor>();
        }
    }
}
