local gymMarker = {}

gymMarker.name = "CollabUtils2/GymMarker"
gymMarker.depth = 0
gymMarker.texture = "CollabUtils2/ahorn_gymmarker"
gymMarker.placements = {
    name = "controller",
    data = {
        name = "",
        difficulty = "beginner"
    }
}

gymMarker.fieldInformation = {
    difficulty = {
        options = { "beginner", "intermediate", "advanced", "expert", "grandmaster" },
        editable = false
    }
}

return gymMarker
