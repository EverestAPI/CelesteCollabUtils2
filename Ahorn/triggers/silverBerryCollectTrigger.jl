module CollabUtils2SilverBerryCollectTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/SilverBerryCollectTrigger" SilverBerryCollectTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight)

const placements = Ahorn.PlacementDict(
    "Silver Berry Collect (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        SilverBerryCollectTrigger,
        "rectangle",
    ),
)

end
