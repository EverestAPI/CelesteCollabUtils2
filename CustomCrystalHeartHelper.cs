using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.CollabUtils2 {
    class CustomCrystalHeartHelper {

        public static void Load() {
            On.Celeste.HeartGem.Awake += customizeParticles;
            On.Celeste.Poem.ctor += customizePoemDisplay;
        }

        public static void Unload() {
            On.Celeste.HeartGem.Awake -= customizeParticles;
            On.Celeste.Poem.ctor -= customizePoemDisplay;
        }

        private static void customizeParticles(On.Celeste.HeartGem.orig_Awake orig, HeartGem self, Scene scene) {
            orig(self, scene);

            if (!self.IsGhost && LobbyHelper.IsHeartSide(self.SceneAs<Level>().Session.Area.GetSID())) {
                // we are in a heartside: make the heart particles match the heart sprite.
                DynData<HeartGem> selfData = new DynData<HeartGem>(self);
                switch (selfData.Get<Sprite>("sprite").Texture.AtlasPath) {
                    case "collectables/heartGem/1/00":
                        selfData["shineParticle"] = HeartGem.P_RedShine;
                        break;
                    case "collectables/heartGem/2/00":
                        selfData["shineParticle"] = HeartGem.P_GoldShine;
                        break;
                    case "CollabUtils2/crystalHeart/expert/00":
                        selfData["shineParticle"] = new ParticleType(HeartGem.P_BlueShine) {
                            Color = Color.Orange
                        };
                        break;
                    case "CollabUtils2/crystalHeart/grandmaster/00":
                        selfData["shineParticle"] = new ParticleType(HeartGem.P_BlueShine) {
                            Color = Color.DarkViolet
                        };
                        break;
                }
            }
        }

        private static void customizePoemDisplay(On.Celeste.Poem.orig_ctor orig, Poem self, string text, int heartIndex, float heartAlpha) {
            orig(self, text, heartIndex, heartAlpha);

            string sid = (Engine.Scene as Level).Session.Area.GetSID();
            if (InGameOverworldHelper.HeartSpriteBank.Has("crystalHeart_" + sid.DialogKeyify())) {
                // we have a custom heart in our sprite bank, use it.
                InGameOverworldHelper.HeartSpriteBank.CreateOn(self.Heart, "crystalHeart_" + sid.DialogKeyify());
                self.Heart.Play("spin");

                // and adjust the screen color to the heart.
                DynData<Poem> selfData = new DynData<Poem>(self);
                switch (self.Heart.Texture.AtlasPath) {
                    case "collectables/heartgem/1/spin00":
                        selfData["Color"] = Calc.HexToColor("ff668a");
                        break;
                    case "collectables/heartgem/2/spin00":
                        selfData["Color"] = Calc.HexToColor("D2AD01");
                        break;
                    case "CollabUtils2/crystalHeart/expert/spin00":
                        selfData["Color"] = Color.Orange;
                        break;
                    case "CollabUtils2/crystalHeart/grandmaster/spin00":
                        selfData["Color"] = Color.DarkViolet;
                        break;
                }
            }
        }
    }
}
