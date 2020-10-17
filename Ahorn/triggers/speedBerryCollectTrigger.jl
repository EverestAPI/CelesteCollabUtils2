module CollabUtils2SpeedBerryCollectTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/SpeedBerryCollectTrigger" SpeedBerryCollectTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight)

const placements = Ahorn.PlacementDict(
    "Speed Berry Collect (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        SpeedBerryCollectTrigger,
        "rectangle",
    ),
)

end
