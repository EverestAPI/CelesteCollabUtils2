module CollabUtils2CrystalHeartShard

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/CrystalHeartShard" CrystalHeartShard(x::Integer, y::Integer, sprite::String="beginner")

const placements = Ahorn.PlacementDict(
    "Crystal Heart Shard (Collab Utils 2)" => Ahorn.EntityPlacement(
        CrystalHeartShard
    )
)

Ahorn.editingOptions(entity::CrystalHeartShard) = Dict{String, Any}(
    "sprite" => String["beginner", "intermediate", "advanced", "expert", "grandmaster"]
)

function Ahorn.selection(entity::CrystalHeartShard)
    x, y = Ahorn.position(entity)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    return Ahorn.getSpriteRectangle(sprite, x, y)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::CrystalHeartShard, room::Maple.Room)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    Ahorn.drawSprite(ctx, sprite, 0, 0)
end

end