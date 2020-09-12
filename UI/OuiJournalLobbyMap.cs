using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.UI {
    class OuiJournalLobbyMap : OuiJournalPage {
        private MTexture mapImage;

        public OuiJournalLobbyMap(OuiJournal journal, MTexture mapImage) : base(journal) {
            PageTexture = "page";

            this.mapImage = mapImage;
        }

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);

            Draw.SpriteBatch.Begin();
            mapImage.DrawCentered(new Vector2(PageWidth / 2, PageHeight / 2));
            Draw.SpriteBatch.End();
        }
    }
}
