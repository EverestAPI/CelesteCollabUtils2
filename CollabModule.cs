using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Monocle;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabModule : EverestModule {

        public static CollabModule Instance;

        private static Hook hookOrigSessionCtor;

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
            SpeedBerry.Load();
            AreaCompleteInfoInLevel.Load();
            SilverBlock.Load();
            MiniHeartDoorUnlockCutsceneTrigger.Load();

            Everest.Content.OnUpdate += onModAssetUpdate;

            hookOrigSessionCtor = new Hook(typeof(Session).GetMethod("orig_ctor"), typeof(CollabModule).GetMethod("onNewSession", BindingFlags.NonPublic | BindingFlags.Static));
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
            SpeedBerry.Unload();
            AreaCompleteInfoInLevel.Unload();
            SilverBlock.Unload();
            MiniHeartDoorUnlockCutsceneTrigger.Unload();

            Everest.Content.OnUpdate -= onModAssetUpdate;

            hookOrigSessionCtor?.Dispose();
            hookOrigSessionCtor = null;
        }

        public override void Initialize() {
            base.Initialize();

            LobbyHelper.OnInitialize();
            InGameOverworldHelper.Initialize();
        }

        private void onModAssetUpdate(ModAsset oldAsset, ModAsset newAsset) {
            if (newAsset?.PathVirtual == "CollabUtils2CollabID") {
                LobbyHelper.LoadCollabIDFile(newAsset);
            }
            if (newAsset != null && newAsset.PathVirtual.StartsWith("Graphics/CollabUtils2/CrystalHeartSwaps_")) {
                reloadCrystalHeartSwapSpriteBanks();
            }
        }

        public override void LoadContent(bool firstLoad) {
            reloadCrystalHeartSwapSpriteBanks();
        }

        public override void DeserializeSession(int index, byte[] data) {
            base.DeserializeSession(index, data);

            if (data == null && global::Celeste.SaveData.Instance?.CurrentSession_Safe != null) {
                // the session is new, but this isn't a newly created save file.
                ReturnToLobbyHelper.OnSessionCreated();
                LobbyHelper.OnSessionCreated();
            }
        }

        private class RandomizedFlagsMapMeta {
            public Dictionary<string, float> CollabUtilsRandomizedFlags { get; set; } = new Dictionary<string, float>();
        }

        private static void onNewSession(On.Celeste.Session.orig_ctor_AreaKey_string_AreaStats orig, Session self, AreaKey area, string checkpoint, AreaStats oldStats) {
            orig(self, area, checkpoint, oldStats);

            if (Everest.Content.Map.TryGetValue("Maps/" + area.GetSID(), out ModAsset asset) && asset.TryGetMeta(out RandomizedFlagsMapMeta meta)) {
                double diceRoll = new Random().NextDouble();
                foreach (KeyValuePair<string, float> flag in meta?.CollabUtilsRandomizedFlags ?? new Dictionary<string, float>()) {
                    if (diceRoll < flag.Value) {
                        self.SetFlag(flag.Key, true);
                        break;
                    }
                    diceRoll -= flag.Value;
                }
            }
        }

        public override void PrepareMapDataProcessors(MapDataFixup context) {
            base.PrepareMapDataProcessors(context);

            context.Add<CollabMapDataProcessor>();
        }

        private static void reloadCrystalHeartSwapSpriteBanks() {
            // let's get an empty sprite bank.
            SpriteBank crystalHeartSwaps = new SpriteBank(GFX.Gui, "Graphics/CollabUtils2/Empty.xml");

            // get all the "CrystalHeartSwaps" xmls across the loaded mods.
            foreach (string xmlPath in Everest.Content.Map
                .Where(path => path.Value.Type == typeof(AssetTypeXml) && path.Key.StartsWith("Graphics/CollabUtils2/CrystalHeartSwaps_"))
                .Select(path => path.Key)) {

                // load the xml and merge it into the crystalHeartSwaps bank.
                SpriteBank newHeartSwaps = new SpriteBank(GFX.Gui, xmlPath + ".xml");

                foreach (KeyValuePair<string, SpriteData> kvpBank in newHeartSwaps.SpriteData) {
                    crystalHeartSwaps.SpriteData[kvpBank.Key] = kvpBank.Value;
                }

                Logger.Log("CollabUtils2/CollabModule", $"Loaded {newHeartSwaps.SpriteData.Count} sprite(s) from {xmlPath}");
            }

            // we are done loading all crystal heart swaps! this is the one we are going to use in chapter panels.
            InGameOverworldHelper.HeartSpriteBank = crystalHeartSwaps;

            Logger.Log(LogLevel.Info, "CollabUtils2/CollabModule", $"Reloaded CrystalHeartSwaps.xml: {crystalHeartSwaps.SpriteData.Count} sprite(s) are registered");
        }
    }
}
