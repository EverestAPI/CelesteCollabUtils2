module CollabUtils2MiniHeartDoorUnlockCutsceneTrigger

using ..Ahorn, Maple

@mapdef Trigger "CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger" MiniHeartDoorUnlockCutsceneTrigger(x::Integer, y::Integer,
    width::Integer=Maple.defaultTriggerWidth, height::Integer=Maple.defaultTriggerHeight, doorID::String="")

const placements = Ahorn.PlacementDict(
    "Mini Heart Door Unlock Cutscene (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        MiniHeartDoorUnlockCutsceneTrigger,
        "rectangle",
    ),
)

end
