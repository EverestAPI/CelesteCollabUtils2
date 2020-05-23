module CollabUtils2MapSwapTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/MapSwapTrigger" MapSwapTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight,
    map::String="Celeste/1-ForsakenCity", side::String="Normal", room::String="1")

const placements = Ahorn.PlacementDict(
    "Map Swap (Collab Utils 2)" => Ahorn.EntityPlacement(
        MapSwapTrigger,
        "rectangle",
    ),
)

function Ahorn.editingOptions(trigger::MapSwapTrigger)
    return Dict{String, Any}(
        "side" => String["Normal", "BSide", "CSide"]
    )
end

end
