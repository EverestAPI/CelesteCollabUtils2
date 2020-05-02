module CollabUtils2MiniHeart

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/MiniHeart" MiniHeart(x::Integer, y::Integer, sprite::String="beginner")

const placements = Ahorn.PlacementDict(
    "Mini Heart (Collab Utils 2)" => Ahorn.EntityPlacement(
        MiniHeart
    )
)

Ahorn.editingOptions(entity::MiniHeart) = Dict{String, Any}(
    "sprite" => String["beginner", "intermediate", "advanced", "expert", "grandmaster"]
)

function Ahorn.selection(entity::MiniHeart)
    x, y = Ahorn.position(entity)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    return Ahorn.getSpriteRectangle(sprite, x, y)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::MiniHeart, room::Maple.Room)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    Ahorn.drawSprite(ctx, sprite, 0, 0)
end

end