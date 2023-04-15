using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.Entities {
    [Tracked]
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

            var talkRect = new Rectangle(-16, -32, 32, 32);
            Collidable = true;
            Collider = new Hitbox(talkRect.Width, talkRect.Height, talkRect.X, talkRect.Y);

            Add(new TalkComponent(talkRect, new Vector2(0, data.Float("interactOffsetY", -16f)), OnTalk) {
                PlayerMustBeFacing = false,
            });
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            var level = scene as Level;
            info.SID = level.Session.Area.SID;
            info.Room = level.Session.Level;
        }

        public void OnTalk(Player player) {
            // don't allow this to somehow trigger twice from the same action
            if (player.Scene is Level level && level.CanRetry) {
                LobbyMapUI.SetLocked(true);
                if (level.Tracker.GetEntity<LobbyMapController>() is LobbyMapController lmc) {
                    lmc.VisitManager?.ActivateWarp(info.MarkerId);
                }
                if (playActivateSprite) {
                    Add(new Coroutine(activateRoutine(player)));
                } else {
                    level.Add(new LobbyMapUI());
                }
            }
        }

        private IEnumerator activateRoutine(Player player) {
            if (player == null) yield break;

            LobbyMapUI.SetLocked(true, Scene, player);
            yield return player.DummyWalkToExact((int)X, false, 1f, true);

            // handle the case where we're dead or not on the ground in front of it
            if (!validPlayer(player)) {
                if (!player.Dead) {
                    LobbyMapUI.SetLocked(false, Scene, player);
                }
                yield break;
            }

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

            Audio.Play("event:/char/madeline/backpack_drop");

            // loop until animation is finished or the player can no longer use the bench
            while (playerSprite.Animating && validPlayer(player)) {
                // force locked while animating to prevent jank
                LobbyMapUI.SetLocked(true, Scene, player);
                yield return null;
            }

            // show the UI if the player successfully sat down
            if (validPlayer(player)) {
                player.Scene.Add(new LobbyMapUI());
            } else {
                LobbyMapUI.SetLocked(false, Scene, player);
            }

            yield return 0.5f;

            playerSprite.RemoveSelf();
            playerHairSprite.RemoveSelf();

            player.Sprite.Visible = player.Hair.Visible = true;
        }

        private bool validPlayer(Player player) {
            // validates that the player is standing on the ground in front of the bench, and isn't dead
            return (Center - player.Center).LengthSquared() < 16 * 16 && !player.Dead && player.OnGround();
        }
    }
}
