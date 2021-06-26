using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.CollabUtils2.UI {
    // heavily based on the parts of OuiFileSelectSlot that make up the "are you sure you want to delete this file" message.
    public class AssistSkipConfirmUI : Entity {
        private float openingEase;
        private bool opened;
        private int currentlySelectedOption; // 0 is yes, 1 is no

        private Action onConfirm;
        private Action onCancel;

        private Wiggler wiggler;

        public AssistSkipConfirmUI(Action onConfirm, Action onCancel) : base() {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            Tag = Tags.HUD | Tags.PauseUpdate;

            Add(wiggler = Wiggler.Create(0.4f, 4f));
        }

        public override void Added(Scene scene) {
            base.Added(scene);
            opened = true;
        }

        public override void Update() {
            base.Update();

            // fade the menu in or out
            openingEase = Calc.Approach(openingEase, opened ? 1f : 0f, Engine.DeltaTime * 4f);

            if (opened) {
                if (Input.MenuCancel.Pressed) {
                    // cancelled out
                    opened = false;
                    wiggler.Start();
                    Audio.Play("event:/ui/main/button_back");
                    onCancel();

                } else if (Input.MenuUp.Pressed && currentlySelectedOption > 0) {
                    // move up to "yes"
                    currentlySelectedOption = 0;
                    wiggler.Start();
                    Audio.Play("event:/ui/main/rollover_up");

                } else if (Input.MenuDown.Pressed && currentlySelectedOption < 1) {
                    // move down to "no"
                    currentlySelectedOption = 1;
                    wiggler.Start();
                    Audio.Play("event:/ui/main/rollover_down");

                } else if (Input.MenuConfirm.Pressed) {
                    if (currentlySelectedOption == 1) {
                        // pressed "no"
                        opened = false;
                        wiggler.Start();
                        Audio.Play("event:/ui/main/button_back");
                        onCancel();
                    } else {
                        // pressed "yes"
                        RemoveSelf();
                        Audio.Play("event:/ui/main/button_select");
                        onConfirm();
                    }
                }
            } else if (openingEase <= 0f) {
                // finished fading out
                RemoveSelf();
            }
        }

        public override void Render() {
            base.Render();

            float currentWiggle = wiggler.Value * 8f;

            if (openingEase > 0f) {
                float openingEaseEased = Ease.CubeOut(openingEase);
                Vector2 anchor = new Vector2(960f, 540f);
                float lineHeight = ActiveFont.LineHeight;

                // draw background
                Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * openingEaseEased * 0.9f);

                // draw text
                ActiveFont.Draw(Dialog.Clean("collabutils2_assist_skip_confirm"),
                    position: anchor + new Vector2(
                        x: 0f,
                        y: -16f - 64f * (1f - openingEaseEased)),
                    justify: new Vector2(0.5f, 1f),
                    scale: Vector2.One,
                    color: Color.White * openingEaseEased);

                ActiveFont.DrawOutline(Dialog.Clean("collabutils2_assist_skip_confirm_yes"),
                    position: anchor + new Vector2(
                        x: ((opened && currentlySelectedOption == 0) ? currentWiggle : 0f) * 1.2f * openingEaseEased,
                        y: 16f + 64f * (1f - openingEaseEased)),
                    justify: new Vector2(0.5f, 0f),
                    scale: Vector2.One * 0.8f,
                    color: opened ? selectionColor(currentlySelectedOption == 0) : Color.Gray,
                    stroke: 2f,
                    strokeColor: Color.Black * openingEaseEased);

                ActiveFont.DrawOutline(Dialog.Clean("collabutils2_assist_skip_confirm_no"),
                    position: anchor + new Vector2(
                        x: ((opened && currentlySelectedOption == 1) ? currentWiggle : 0f) * 1.2f * openingEaseEased,
                        y: 16f + lineHeight + 64f * (1f - openingEaseEased)),
                    justify: new Vector2(0.5f, 0f),
                    scale: Vector2.One * 0.8f,
                    color: opened ? selectionColor(currentlySelectedOption == 1) : Color.Gray,
                    stroke: 2f,
                    strokeColor: Color.Black * openingEaseEased);
            }
        }

        private Color selectionColor(bool selected) {
            if (selected) {
                if (!Settings.Instance.DisableFlashes && !Scene.BetweenInterval(0.1f)) {
                    return TextMenu.HighlightColorB;
                }
                return TextMenu.HighlightColorA;
            }
            return Color.White;
        }
    }
}
