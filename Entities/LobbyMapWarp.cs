using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity(ENTITY_NAME)]
    public class LobbyMapWarp : Entity {
        public const string ENTITY_NAME = "CollabUtils2/LobbyMapWarp";

        private readonly string spriteName;
        private readonly string spriteAnimation;
        private readonly string activateSpriteName;
        private readonly string activateSpriteAnimation;
        private readonly Facings playerFacing;
        
        private readonly Sprite sprite;
        private LobbyMapController.FeatureInfo info;

        public LobbyMapWarp(EntityData data, Vector2 offset) : base(data.Position + offset) {
            spriteName = data.Attr("spriteName");
            spriteAnimation = data.Attr("spriteAnimation");
            activateSpriteName = data.Attr("activateSpriteName");
            playerFacing = (Facings) data.Int("playerFacing", (int) Facings.Right);

            LobbyMapController.FeatureInfo.TryParse(data, default, out info);
            
            Add(new TalkComponent(new Rectangle(-16, -32, 32, 32), Vector2.Zero, onTalk) {
                PlayerMustBeFacing = false,
            });

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

                if (!string.IsNullOrWhiteSpace(activateSpriteName)) {
                    Add(new Coroutine(activateRoutine(player)));
                } else {
                    level.Add(new LobbyMapUI());
                }
            }
        }

        private IEnumerator activateRoutine(Player player) {
            if (player == null) yield break;

            player.StateMachine.State = Player.StDummy;
            yield return player.DummyWalkToExact((int)X, false, 1f, true);
            player.Facing = playerFacing;

            player.Sprite.Visible = player.Hair.Visible = false;

            var playerSprite = new Sprite(GFX.Game, activateSpriteName);
            playerSprite.Add("idle", "", 0.08f);
            playerSprite.CenterOrigin();
            playerSprite.Position += new Vector2(0, -16);
            Add(playerSprite);

            var playerHairSprite = new Sprite(GFX.Game, activateSpriteName + "Hair");
            playerHairSprite.Add("idle", "", 0.08f);
            playerHairSprite.CenterOrigin();
            playerHairSprite.Position += new Vector2(0, -16);
            playerHairSprite.Color = player.Hair.Color;
            Add(playerHairSprite);
            
            playerSprite.Play("idle");
            playerHairSprite.Play("idle");

            while (playerSprite.Animating) {
                yield return null;
            }
            
            player.Scene.Add(new LobbyMapUI());

            yield return 0.1f;
            
            playerSprite.RemoveSelf();
            playerHairSprite.RemoveSelf();
            
            player.Sprite.Visible = player.Hair.Visible = true;
        }
    }
}
