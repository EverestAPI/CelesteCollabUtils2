using Monocle;
using System.Collections;

namespace Celeste.Mod.CollabUtils2.UI {
    public class OuiHelper_EnterChapterPanel : Oui {

        public static bool Start;

        public OuiHelper_EnterChapterPanel() {
        }

        public override bool IsStart(Overworld overworld, Overworld.StartMode start) {
            if (Start) {
                Start = false;
                Add(new Coroutine(Enter(null)));
                return true;
            }

            return false;
        }

        public override IEnumerator Enter(Oui from) {
            Audio.Play("event:/ui/world_map/icon/select");
            Overworld.Goto<OuiChapterPanel>();
            yield break;
        }

        public override IEnumerator Leave(Oui next) {
            yield break;
        }

    }
}
