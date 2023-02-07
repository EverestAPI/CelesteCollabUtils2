using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    [CustomEntity("CollabUtils2/LobbyMapWarp")]
    public class LobbyMapWarp : Entity {
        private readonly string warpSpritePath;
        private readonly bool warpSpriteFlipX;
        private readonly bool playActivateSprite;
        private readonly bool activateSpriteFlipX;
        private readonly Facings playerFacing;

        private LobbyMapController.MarkerInfo info;

        private Image image;

        public LobbyMapWarp(EntityData data, Vector2 offset) : base(data.Position + offset) {
            warpSpritePath = data.Attr("warpSpritePath", "decals/1-forsakencity/bench_concrete");
            warpSpriteFlipX = data.Bool("warpSpriteFlipX");
            playActivateSprite = data.Bool("playActivateSprite", true);
            activateSpriteFlipX = data.Bool("activateSpriteFlipX");
            playerFacing = data.Enum("playerFacing", Facings.Right);
            Depth = data.Int("depth", Depths.Below);

            LobbyMapController.MarkerInfo.TryParse(data, null, out info);

            if (!string.IsNullOrWhiteSpace(warpSpritePath)) {
                Add(image = new Image(GFX.Game[warpSpritePath]));
                image.Effects = warpSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                image.JustifyOrigin(0.5f, 1f);
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
                if (playActivateSprite) {
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

            var playerSprite = GFX.SpriteBank.Create("CollabUtils2_sitBench");
            playerSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Add(playerSprite);
            playerSprite.Play("sit");

            var playerHairSprite = GFX.SpriteBank.Create("CollabUtils2_sitBench");
            playerHairSprite.Effects = activateSpriteFlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            playerHairSprite.Color = player.Hair.Color;
            Add(playerHairSprite);
            playerHairSprite.Play("sitHair");

            while (playerSprite.Animating) {
                yield return null;
            }

            player.Scene.Add(new LobbyMapUI());

            yield return 0.5f;

            playerSprite.RemoveSelf();
            playerHairSprite.RemoveSelf();

            player.Sprite.Visible = player.Hair.Visible = true;
        }
    }
}
