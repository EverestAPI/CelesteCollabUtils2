module CollabUtils2JournalTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/JournalTrigger" JournalTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight,
    levelset::String="Celeste", showOnlyDiscovered::Bool=false)

const placements = Ahorn.PlacementDict(
    "Journal Trigger (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        JournalTrigger,
        "rectangle",
    ),
)

function Ahorn.editingOptions(trigger::JournalTrigger)
    return Dict{String, Any}(
        "positionMode" => Maple.trigger_position_modes
    )
end

function Ahorn.nodeLimits(trigger::JournalTrigger)
    return 0, 1
end

end