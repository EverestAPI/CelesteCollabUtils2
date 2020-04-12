using Celeste.Mod.CollabUtils2.Entities;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/JournalTrigger")]
    public class JournalTrigger : Trigger {

        public string levelset;

        private readonly TalkComponent talkComponent;

        public JournalTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            levelset = data.Attr("levelset");

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                player => InGameOverworldHelper.OpenJournal(player, levelset)
            ) { PlayerMustBeFacing = false });
        }

        public override void Update() {
            base.Update();
            talkComponent.Enabled = !InGameOverworldHelper.IsOpen;
        }

    }
}
