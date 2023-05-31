using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/WarpPedestal")]
    public class WarpPedestal : Entity {
        private readonly string map;
        private readonly string returnToLobbyMode;
        private readonly bool allowSaving;
        private readonly string fillSoundEffect;
        private readonly int bubbleOffsetY;

        public WarpPedestal(Vector2 position, string spriteName, string map, string returnToLobbyMode, bool allowSaving, string fillSoundEffect, int bubbleOffsetY) : base(position) {
            this.map = map;
            this.returnToLobbyMode = returnToLobbyMode;
            this.allowSaving = allowSaving;
            this.fillSoundEffect = fillSoundEffect;
            this.bubbleOffsetY = bubbleOffsetY;

            // check if the map was already completed
            AreaData areaData = AreaData.Get(map);
            bool complete = false;
            if (areaData != null) {
                complete = SaveData.Instance.GetAreaStatsFor(areaData.ToKey())?.Modes[0].Completed ?? false;
            }

            string animation;
            if (complete) {
                if (!CollabModule.Instance.SaveData.CompletedWarpPedestalSIDs.Contains(map)) {
                    // map is complete but jar wasn't filled yet!
                    animation = "before_fill";
                    CollabModule.Instance.SaveData.CompletedWarpPedestalSIDs.Add(map);
                } else {
                    // map is complete and jar was already filled.
                    animation = "full";
                }
            } else {
                // map wasn't completed yet.
                animation = "empty";
            }

            Sprite sprite = GFX.SpriteBank.Create(spriteName);
            sprite.Play(animation);
            Add(sprite);

            // play the fill sound at the right time.
            if (animation == "before_fill" && !string.IsNullOrEmpty(fillSoundEffect)) {
                sprite.OnChange = (lastAnimationId, currentAnimationId) => {
                    if (currentAnimationId == "fill") {
                        Add(new SoundSource(new Vector2(0, -20f), fillSoundEffect) { RemoveOnOneshotEnd = true });
                    }
                };
            }

            Depth = Depths.NPCs;
        }

        public WarpPedestal(EntityData data, Vector2 offset) : this(data.Position + offset, data.Attr("sprite"), data.Attr("map"),
            data.Attr("returnToLobbyMode"), data.Bool("allowSaving"), data.Attr("fillSoundEffect"), data.Int("bubbleOffsetY")) { }

        public override void Added(Scene scene) {
            base.Added(scene);

            // spawn a chapter panel trigger from Collab Utils that will take care of the actual teleporting.
            scene.Add(new ChapterPanelTrigger(new EntityData() {
                Position = Position - new Vector2(24f, 32f),
                Width = 48,
                Height = 32,
                Nodes = new Vector2[] { Position - new Vector2(0, bubbleOffsetY) },
                Values = new Dictionary<string, object>() {
                    { "map", map },
                    { "returnToLobbyMode", returnToLobbyMode },
                    { "allowSaving", allowSaving }
                }
            }, Vector2.Zero));
        }
    }
}
