module CollabUtils2ExitFromGymTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/ExitFromGymTrigger" ExitFromGymTrigger(x::Integer, y::Integer, width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight)

const placements = Ahorn.PlacementDict(
    "Exit From Gym Trigger (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        ExitFromGymTrigger,
        "rectangle",
    ),
)

function Ahorn.nodeLimits(trigger::ExitFromGymTrigger)
    return 0, 1
end

end
