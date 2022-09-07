module CollabUtils2GoldenBerryPlayerRespawnPoint

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/GoldenBerryPlayerRespawnPoint" GoldenBerryPlayerRespawnPoint(x::Integer, y::Integer)

const placements = Ahorn.PlacementDict(
    "Golden Berry Player Respawn Point (Collab Utils 2)" => Ahorn.EntityPlacement(
        GoldenBerryPlayerRespawnPoint
    )
)

const sprite = "characters/player/sitDown00"
const tint = (0.8, 0.6, 0.1, 0.75)

function Ahorn.selection(entity::GoldenBerryPlayerRespawnPoint)
    x, y = Ahorn.position(entity)

    return Ahorn.getSpriteRectangle(sprite, x, y, jx=0.5, jy=1.0)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::GoldenBerryPlayerRespawnPoint) = Ahorn.drawSprite(ctx, sprite, 0, 0, jx=0.5, jy=1.0, tint=tint)

end
