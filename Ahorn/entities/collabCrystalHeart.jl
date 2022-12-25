module CollabUtils2CollabCrystalHeart

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/CollabCrystalHeart" CollabCrystalHeart(x::Integer, y::Integer, removeCameraTriggers::Bool=false)

const placements = Ahorn.PlacementDict(
    "Crystal Heart (Return to Lobby) (Collab Utils 2)" => Ahorn.EntityPlacement(
        CollabCrystalHeart
    ),
)

const sprite = "collectables/heartGem/0/00.png"

function Ahorn.selection(entity::CollabCrystalHeart)
    x, y = Ahorn.position(entity)

    return Ahorn.getSpriteRectangle(sprite, x, y)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::CollabCrystalHeart, room::Maple.Room) = Ahorn.drawSprite(ctx, sprite, 0, 0)

end
