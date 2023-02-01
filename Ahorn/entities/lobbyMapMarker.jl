module CollabUtils2LobbyMapMarker

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/LobbyMapMarker" LobbyMapMarker(x::Integer, y::Integer, icon::String="CollabUtils2/lobbies/memorial")

const placements = Ahorn.PlacementDict(
    "Lobby Map Marker (Collab Utils 2)" => Ahorn.EntityPlacement(
        LobbyMapMarker
    )
)

const sprite = "CollabUtils2/editor_lobbymapmarker"

function Ahorn.selection(entity::LobbyMapMarker)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::LobbyMapMarker, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end