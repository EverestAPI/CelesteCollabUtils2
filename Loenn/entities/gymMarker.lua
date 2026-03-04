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
        options = { "", "56b3ff", "ff6d81", "ffff89", "ff9e66", "dd87ff" },
        editable = true
    },
    learnedColor = {
        fieldType = "color",
        allowEmpty = true,
        options = { "", "a7e2f9", "faa7bc", "fbf8b8", "fbd0a6", "f3bafa" },
        editable = true
    }
}

return gymMarker
