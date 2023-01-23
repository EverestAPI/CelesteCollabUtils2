using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity(ENTITY_NAME)]
    public class LobbyMapWarp : Entity {
        public const string ENTITY_NAME = "CollabUtils2/LobbyMapWarp";

        private readonly string spriteName;
        private readonly string spriteAnimation;
        private readonly Sprite sprite;
        private LobbyMapController.FeatureInfo info;

        public LobbyMapWarp(EntityData data, Vector2 offset) : base(data.Position + offset) {
            spriteName = data.Attr("spriteName");
            spriteAnimation = data.Attr("spriteAnimation");

            LobbyMapController.FeatureInfo.TryParse(data, default, out info);
            
            Add(new TalkComponent(new Rectangle(-16, -32, 32, 32), Vector2.Zero, onTalk));

            if (!string.IsNullOrWhiteSpace(spriteName)) {
                Add(sprite = GFX.SpriteBank.Create(spriteName));
                sprite.JustifyOrigin(0.5f, 1f);
                if (!string.IsNullOrWhiteSpace(spriteAnimation)) {
                    sprite.Play(spriteAnimation);
                }
            }
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            var level = scene as Level;
            info.SID = level.Session.Area.SID;
            info.Room = level.Session.Level;
        }

        private void onTalk(Player player) {
            if (player.Scene is Level level) {
                if (!CollabModule.Instance.SaveData.ActivatedLobbyWarps.TryGetValue(level.Session.Area.SID, out List<string> warps)) {
                    CollabModule.Instance.SaveData.ActivatedLobbyWarps[level.Session.Area.SID] = warps = new List<string>();
                }

                if (!warps.Contains(info.FeatureId)) {
                    warps.Add(info.FeatureId);
                }
                
                level.Add(new LobbyMapUI());
            }
        }
    }
}
