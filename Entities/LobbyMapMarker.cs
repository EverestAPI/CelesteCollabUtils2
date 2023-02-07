using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.CollabUtils2.Entities {
    /// <summary>
    /// This entity does nothing in-game. Its EntityData will be read by LobbyMapUI and rendered on the map.
    /// </summary>
    [CustomEntity("CollabUtils2/LobbyMapMarker")]
    public class LobbyMapMarker : Entity {
        public LobbyMapMarker(EntityData data, Vector2 offset) : base(data.Position + offset) {
            Active = false;
            Visible = false;
        }
    }
}
