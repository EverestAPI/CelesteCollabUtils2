using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using MonoMod.Utils;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/JournalTrigger")]
    public class JournalTrigger : Trigger {
        private string levelset;

        private readonly TalkComponent talkComponent;

        public JournalTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            levelset = data.Attr("levelset");

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                player => {
                    JournalHelper.VanillaJournal = data.Bool("vanillaJournal", defaultValue: false);
                    JournalHelper.ShowOnlyDiscovered = data.Bool("showOnlyDiscovered", defaultValue: false);
                    InGameOverworldHelper.OpenJournal(player, levelset);
                }
            ) { PlayerMustBeFacing = false });
        }

        public override void Update() {
            base.Update();
            talkComponent.Enabled = !InGameOverworldHelper.IsOpen;
        }
    }
}
