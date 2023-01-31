using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System.Collections;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/LobbyMapWarp")]
    public class LobbyMapWarp : Entity {
        private readonly string spritePath;
        private readonly bool spriteFlipX;
        private readonly string activateSpritePath;
        private readonly bool activateSpriteFlipX;
        private readonly Facings playerFacing;
        
        private LobbyMapController.FeatureInfo info;

        private Sprite sprite;

        public LobbyMapWarp(EntityData data, Vector2 offset) : base(data.Position + offset) {
            spritePath = data.Attr("spritePath", "decals/1-forsakencity/bench_concrete");
            spriteFlipX = data.Bool("spriteFlipX");
            activateSpritePath = data.Attr("activateSpritePath", "CollabUtils2/characters/sitBench");
            activateSpriteFlipX = data.Bool("activateSpriteFlipX");
            playerFacing = (Facings) data.Int("playerFacing", (int) Facings.Right);
            Depth = data.Int("depth", Depths.Below);

            LobbyMapController.FeatureInfo.TryParse(data, null, out info);

            if (!string.IsNullOrWhiteSpace(spritePath)) {
                Add(sprite = new Sprite(GFX.Game, spritePath));
                sprite.Effects = spriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                sprite.Justify = new Vector2(0.5f, 1f);
                sprite.Add("idle", string.Empty);
                sprite.Play("idle");
            }
            
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
            playerSprite.Justify = new Vector2(0.5f, 1f);
            playerSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Add(playerSprite);
            playerSprite.Play("idle");

            Sprite playerHairSprite = null;
            var hairSpritePath = activateSpritePath + "Hair";
            if (GFX.Game.Textures.Keys.Any(t => t.StartsWith(hairSpritePath))) {
                playerHairSprite = new Sprite(GFX.Game, activateSpritePath + "Hair");
                playerHairSprite.Add("idle", "", 0.08f);
                playerHairSprite.Justify = new Vector2(0.5f, 1f);
                playerHairSprite.Color = player.Hair.Color;
                playerHairSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Add(playerHairSprite);
                playerHairSprite.Play("idle");
            }

            while (playerSprite.Animating) {
                yield return null;
            }
            
            player.Scene.Add(new LobbyMapUI());

            yield return 0.5f;
            
            playerSprite.RemoveSelf();
            playerHairSprite?.RemoveSelf();

            player.Sprite.Visible = player.Hair.Visible = true;
        }
    }
}
