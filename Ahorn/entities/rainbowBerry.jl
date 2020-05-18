module CollabUtils2RainbowBerry

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/RainbowBerry" RainbowBerry(x::Integer, y::Integer, levelSet::String="SpringCollab2020/1-Beginner")

const placements = Ahorn.PlacementDict(
    "Rainbow Berry (Collab Utils 2)" => Ahorn.EntityPlacement(
        RainbowBerry
    )
)

const sprite = "CollabUtils2/rainbowBerry/rberry0030.png"

function Ahorn.selection(entity::RainbowBerry)
    x, y = Ahorn.position(entity)
    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::RainbowBerry, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end
