module CollabUtils2MiniHeart

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/MiniHeart" MiniHeart(x::Integer, y::Integer, sprite::String="beginner", refillDash::Bool=true, requireDashToBreak::Bool=true, noGhostSprite::Bool=false, particleColor::String="")
@mapdef Entity "CollabUtils2/FakeMiniHeart" FakeMiniHeart(x::Integer, y::Integer, sprite::String="beginner", refillDash::Bool=true, requireDashToBreak::Bool=true, noGhostSprite::Bool=false, particleColor::String="")

heartUnion = Union{MiniHeart, FakeMiniHeart}

const placements = Ahorn.PlacementDict(
    "Mini Heart (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        MiniHeart
    ),
    "Mini Heart (Fake) (Collab Utils 2)" => Ahorn.EntityPlacement(
        FakeMiniHeart
    ),
)

Ahorn.editingOptions(entity::heartUnion) = Dict{String, Any}(
    "sprite" => String["beginner", "intermediate", "advanced", "expert", "grandmaster"]
)

function Ahorn.selection(entity::heartUnion)
    x, y = Ahorn.position(entity)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    return Ahorn.getSpriteRectangle(sprite, x, y)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::heartUnion, room::Maple.Room)
    sprite = "CollabUtils2/miniheart/$(get(entity, "sprite", "beginner"))/00.png"

    Ahorn.drawSprite(ctx, sprite, 0, 0)
end

end