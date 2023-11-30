module CollabUtils2SpeedBerry

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/SpeedBerry" SpeedBerry(x::Integer, y::Integer, bronzeTime::Number=15.0, silverTime::Number=10.0, goldTime::Number=5.0)

const placements = Ahorn.PlacementDict(
    "Speed Berry (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        SpeedBerry
    )
)

sprite = "CollabUtils2/speedBerry/Idle_g06"

function Ahorn.selection(entity::SpeedBerry)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::SpeedBerry, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end