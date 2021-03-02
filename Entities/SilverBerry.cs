using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// A silver berry is really just a golden berry with a different collect sound and a different sprite...
    /// </summary>
    [CustomEntity("CollabUtils2/SilverBerry")]
    [RegisterStrawberry(tracked: false, blocksCollection: true)]
    class SilverBerry : Strawberry {
        public static SpriteBank SpriteBank;

        private static ParticleType P_SilverGlow;
        private static ParticleType P_OrigGoldGlow;
        private static ParticleType P_SilverGhostGlow;
        private static ParticleType P_OrigGhostGlow;

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/SilverBerry.xml");
        }

        private bool spawnedThroughGiveSilver = false;

        public SilverBerry(EntityData data, Vector2 offset, EntityID gid) : base(data, offset, gid) {
            new DynData<Strawberry>(this)["Golden"] = true;

            if (P_SilverGlow == null) {
                P_SilverGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("BABBC0"),
                    Color2 = Calc.HexToColor("6A8497")
                };
                P_SilverGhostGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("818E9E"),
                    Color2 = Calc.HexToColor("36585B")
                };
                P_OrigGoldGlow = P_GoldGlow;
                P_OrigGhostGlow = P_GhostGlow;
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Session session = (scene as Level).Session;
            if (!spawnedThroughGiveSilver && (session.FurthestSeenLevel != session.Level ||
                (!SaveData.Instance.CheatMode && !SaveData.Instance.Areas_Safe[session.Area.ID].Modes[(int) session.Area.Mode].Completed))) {

                // we went in a further screen or didn't complete the level once yet: don't have the berry spawn.
                RemoveSelf();
            }
        }

        public override void Update() {
            P_GoldGlow = P_SilverGlow;
            P_GhostGlow = P_SilverGhostGlow;
            base.Update();
            P_GoldGlow = P_OrigGoldGlow;
            P_GhostGlow = P_OrigGhostGlow;
        }

        [Command("give_silver", "(Collab Utils 2) gives you a silver strawberry")]
        private static void cmdGiveSilver() {
            if (Engine.Scene is Level level) {
                Player player = level.Tracker.GetEntity<Player>();
                if (player != null) {
                    EntityData entityData = new EntityData();
                    entityData.Position = player.Position + new Vector2(0f, -16f);
                    entityData.ID = Calc.Random.Next();
                    entityData.Name = "CollabUtils2/SilverBerry";
                    SilverBerry silverBerry = new SilverBerry(entityData, Vector2.Zero, new EntityID(level.Session.Level, entityData.ID));
                    silverBerry.spawnedThroughGiveSilver = true;
                    level.Add(silverBerry);
                }
            }
        }
    }
}
