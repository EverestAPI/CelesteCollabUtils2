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
    [CustomEntity("CollabUtils2/ChapterPanelTrigger")]
    public class ChapterPanelTrigger : Trigger {

        public enum ReturnToLobbyMode {
            SetReturnToHere, RemoveReturn, DoNotChangeReturn
        }

        public string map;

        public ReturnToLobbyMode returnToLobbyMode;

        private readonly TalkComponent talkComponent;

        public ChapterPanelTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            map = data.Attr("map");
            returnToLobbyMode = data.Enum("returnToLobbyMode", ReturnToLobbyMode.SetReturnToHere);

            Add(talkComponent = new TalkComponent(
                new Rectangle(0, 0, data.Width, data.Height),
                data.Nodes.Length != 0 ? (data.Nodes[0] - data.Position) : new Vector2(data.Width / 2f, data.Height / 2f),
                player => InGameOverworldHelper.OpenChapterPanel(player, map, returnToLobbyMode)
            ) { PlayerMustBeFacing = false });
        }

        public override void Update() {
            base.Update();
            talkComponent.Enabled = !InGameOverworldHelper.IsOpen;
        }

    }
}
