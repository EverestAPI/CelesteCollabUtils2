using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using System;

namespace Celeste.Mod.CollabUtils2.Triggers {
    [CustomEntity("CollabUtils2/MapSwapTrigger")]
    public class MapSwapTrigger : Trigger {

        public string map;
        public string side;
        public string room;

        private bool swapping;

        public MapSwapTrigger(EntityData data, Vector2 offset)
            : base(data, offset) {
            map = data.Attr("map");
            side = data.Attr("side");
            room = data.Attr("room");
        }

        public override void OnEnter(Player player) {
            base.OnEnter(player);

            if (swapping)
                return;
            swapping = true;

            Level level = SceneAs<Level>();

            level.DoScreenWipe(false, () => {
                if (string.IsNullOrEmpty(room) || room == "-")
                    room = null;

                if (!Enum.TryParse(side, out AreaMode mode))
                    mode = AreaMode.Normal;

                // TODO: Keep track of where the session actually began. Don't overwrite StartCheckpoint.

                Session session = level.Session;
                session.Area = AreaData.Get(map)?.ToKey(mode) ?? new AreaKey(-1);
                if (session.Area.ID == -1) {
                    LevelEnter.Go(new Session(new AreaKey(-1)), false);
                    return;
                }

                session.StartCheckpoint = room;
                session.Level = room;
                session.StartedFromBeginning = false;
                session.RespawnPoint = null;
                LevelEnter.Go(session, false);
            });
        }

    }
}
