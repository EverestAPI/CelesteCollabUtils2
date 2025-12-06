using Celeste.Mod.CollabUtils2.UI;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System.Text.RegularExpressions;

namespace Celeste.Mod.CollabUtils2 {
    class CustomCrystalHeartHelper {

        internal static void Load() {
            On.Celeste.HeartGem.Awake += customizeParticles;
            On.Celeste.Poem.ctor += customizePoemDisplay;
        }

        internal static void Unload() {
            On.Celeste.HeartGem.Awake -= customizeParticles;
            On.Celeste.Poem.ctor -= customizePoemDisplay;
        }

        private static void customizeParticles(On.Celeste.HeartGem.orig_Awake orig, HeartGem self, Scene scene) {
            orig(self, scene);

            if (!self.IsGhost && LobbyHelper.IsHeartSide(self.SceneAs<Level>().Session.Area.GetSID())) {
                // we are in a heartside: make the heart particles match the heart sprite.
                switch (self.sprite.Texture.AtlasPath) {
                    case "collectables/heartGem/1/00":
                        self.shineParticle = HeartGem.P_RedShine;
                        break;
                    case "collectables/heartGem/2/00":
                        self.shineParticle = HeartGem.P_GoldShine;
                        break;
                    case "CollabUtils2/crystalHeart/expert/00":
                        self.shineParticle = new ParticleType(HeartGem.P_BlueShine) {
                            Color = Color.Orange
                        };
                        break;
                    case "CollabUtils2/crystalHeart/grandmaster/00":
                        self.shineParticle = new ParticleType(HeartGem.P_BlueShine) {
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
                switch (self.Heart.Texture.AtlasPath) {
                    case "collectables/heartgem/1/spin00":
                        self.Color = Calc.HexToColor("ff668a");
                        break;
                    case "collectables/heartgem/2/spin00":
                        self.Color = Calc.HexToColor("D2AD01");
                        break;
                    case "CollabUtils2/crystalHeart/expert/spin00":
                        self.Color = Color.Orange;
                        break;
                    case "CollabUtils2/crystalHeart/grandmaster/spin00":
                        self.Color = Calc.HexToColor("d9a2ff");
                        break;
                    default:
                        Match match = Regex.Match(self.Heart.Texture.AtlasPath, "poemtextcolor_([0-9a-fA-F]{6})");
                        if (match.Success) {
                            self.Color = Calc.HexToColor(match.Groups[1].Value);
                        }
                        break;
                }
            }
        }
    }
}
