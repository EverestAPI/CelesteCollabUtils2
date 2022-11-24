module CollabUtils2GymMarker

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/GymMarker" GymMarker(x::Integer, y::Integer, name::String="", difficulty::String="beginner")

const placements = Ahorn.PlacementDict(
    "Gym Marker (Collab Utils 2) (READ DOCS)" => Ahorn.EntityPlacement(
        GymMarker
    )
)

Ahorn.editingOptions(entity::GymMarker) = Dict{String, Any}(
    "difficulty" => ["beginner", "intermediate", "advanced", "expert", "grandmaster"]
)

function Ahorn.selection(entity::GymMarker)
    x, y = Ahorn.position(entity)

    return Ahorn.Rectangle(x - 12, y - 12, 24, 24)
end

Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::GymMarker, room::Maple.Room) = Ahorn.drawSprite(ctx, "CollabUtils2/ahorn_gymmarker", 0, 0)

end