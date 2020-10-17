module CollabUtils2SilverBerry

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/SilverBerry" SilverBerry(x::Integer, y::Integer)

const placements = Ahorn.PlacementDict(
    "Silver Berry (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        SilverBerry
    )
)

const sprite = "CollabUtils2/silverBerry/idle00.png"

function Ahorn.selection(entity::SilverBerry)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::SilverBerry, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end
