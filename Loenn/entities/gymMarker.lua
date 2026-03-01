local gymMarker = {}

gymMarker.name = "CollabUtils2/GymMarker"
gymMarker.depth = 0
gymMarker.texture = "CollabUtils2/editor_gymmarker"
gymMarker.placements = {
    name = "controller",
    data = {
        name = "",
        difficulty = "beginner",
        difficultyColor = "",
        learnedColor = ""
    }
}

gymMarker.fieldInformation = {
    difficulty = {
        options = { "beginner", "intermediate", "advanced", "expert", "grandmaster" },
        editable = true
    },
    difficultyColor = {
        fieldType = "color",
        allowEmpty = true,
        options = { "", "56B3FF", "FF6D81", "FFFF89", "FF9E66", "DD87FF" },
        editable = true
    },
    learnedColor = {
        fieldType = "color",
        allowEmpty = true
    }
}

return gymMarker
