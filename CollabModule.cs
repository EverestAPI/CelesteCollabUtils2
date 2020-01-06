using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod.CollabUtils2 {
    public class CollabModule : EverestModule {

        public static CollabModule Instance;
        
        public CollabModule() {
            Instance = this;
        }

        public override void Load() {
        }

        public override void Unload() {
        }
    }
}
