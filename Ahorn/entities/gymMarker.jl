module CollabUtils2GymMarker

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/GymMarker" GymMarker(x::Integer, y::Integer, name::String="",
        order::Integer=0, color::String="f2e0cb", learnedColor::String="abf797", legacyRenderMode::Bool=false)

const placements = Ahorn.PlacementDict(
    "Gym Marker (Collab Utils 2) (READ DOCS)" => Ahorn.EntityPlacement(
        GymMarker
    )
)

Ahorn.editingOptions(entity::GymMarker) = Dict{String, Any}(
    "color" => ["f2e0cb", "56b3ff", "ff6d81", "ffff89", "ff9e66", "dd87ff"],
    "learnedColor" => ["abf797", "a7e2f9", "faa7bc", "fbf8b8", "fbd0a6", "f3bafa"]
)

function Ahorn.selection(entity::GymMarker)
    x, y = Ahorn.position(entity)

    return Ahorn.Rectangle(x - 12, y - 12, 24, 24)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::GymMarker, room::Maple.Room) = Ahorn.drawSprite(ctx, "CollabUtils2/editor_gymmarker", 0, 0)

end