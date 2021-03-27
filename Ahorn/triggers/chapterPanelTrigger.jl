module CollabUtils2ChapterPanelTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/ChapterPanelTrigger" ChapterPanelTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight,
    map::String="Celeste/1-ForsakenCity", returnToLobbyMode::String="SetReturnToHere", allowSaving::Bool=true)

const placements = Ahorn.PlacementDict(
    "Chapter Panel Trigger (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        ChapterPanelTrigger,
        "rectangle",
    ),
)

function Ahorn.editingOptions(trigger::ChapterPanelTrigger)
    return Dict{String, Any}(
        "returnToLobbyMode" => String["SetReturnToHere", "RemoveReturn", "DoNotChangeReturn"]
    )
end

function Ahorn.nodeLimits(trigger::ChapterPanelTrigger)
    return 0, 1
end

end