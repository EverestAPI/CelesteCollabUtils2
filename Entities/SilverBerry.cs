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

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/RainbowBerry.xml");
        }

        public SilverBerry(EntityData data, Vector2 offset, EntityID gid) : base(data, offset, gid) {
            new DynData<Strawberry>(this)["Golden"] = true;
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Session session = (scene as Level).Session;
            if (session.FurthestSeenLevel != session.Level ||
                (!SaveData.Instance.CheatMode && !SaveData.Instance.Areas_Safe[session.Area.ID].Modes[(int) session.Area.Mode].Completed)) {

                // we went in a further screen or didn't complete the level once yet: don't have the berry spawn.
                RemoveSelf();
            }
        }
    }
}
