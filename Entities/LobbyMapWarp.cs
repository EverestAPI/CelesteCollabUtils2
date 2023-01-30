using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity(ENTITY_NAME)]
    public class LobbyMapWarp : Entity {
        public const string ENTITY_NAME = "CollabUtils2/LobbyMapWarp";

        private readonly string activateSpritePath;
        private readonly float activateSpriteOriginX;
        private readonly float activateSpriteOriginY;
        private readonly bool activateSpriteFlipX;
        private readonly Facings playerFacing;
        
        private LobbyMapController.FeatureInfo info;

        public LobbyMapWarp(EntityData data, Vector2 offset) : base(data.Position + offset) {
            activateSpriteFlipX = data.Bool("activateSpriteFlipX");
            activateSpritePath = data.Attr("activateSpritePath");
            activateSpriteOriginX = data.Float("activateSpriteOriginX", 0.5f);
            activateSpriteOriginY = data.Float("activateSpriteOriginY", 0.5f);
            playerFacing = (Facings) data.Int("playerFacing", (int) Facings.Right);

            LobbyMapController.FeatureInfo.TryParse(data, null, out info);
            
            Add(new TalkComponent(new Rectangle(-16, -32, 32, 32), new Vector2(0, data.Float("interactOffsetY", -16f)), onTalk) {
                PlayerMustBeFacing = false,
            });
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            var level = scene as Level;
            info.SID = level.Session.Area.SID;
            info.Room = level.Session.Level;
        }

        private void onTalk(Player player) {
            if (player.Scene is Level level) {
                if (!string.IsNullOrWhiteSpace(activateSpritePath)) {
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
            
            var playerSprite = new Sprite(GFX.Game, activateSpritePath);
            playerSprite.Add("idle", "", 0.08f);
            playerSprite.Justify = new Vector2(activateSpriteOriginX, activateSpriteOriginY);
            playerSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Add(playerSprite);
            playerSprite.Play("idle");

            Sprite playerHairSprite = null;
            if (GFX.Game.Has(activateSpritePath + "Hair")) {
                playerHairSprite = new Sprite(GFX.Game, activateSpritePath + "Hair");
                playerHairSprite.Add("idle", "", 0.08f);
                playerHairSprite.Justify = new Vector2(activateSpriteOriginX, activateSpriteOriginY);
                playerHairSprite.Color = player.Hair.Color;
                playerHairSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Add(playerHairSprite);
                playerHairSprite.Play("idle");
            }

            while (playerSprite.Animating) {
                yield return null;
            }
            
            player.Scene.Add(new LobbyMapUI());

            yield return 0.1f;
            
            playerSprite.RemoveSelf();
            playerHairSprite?.RemoveSelf();
            
            player.Sprite.Visible = player.Hair.Visible = true;
        }
    }
}
