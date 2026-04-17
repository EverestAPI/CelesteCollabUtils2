local gymMarker = {}

gymMarker.name = "CollabUtils2/GymMarker"
gymMarker.depth = 0
gymMarker.texture = "CollabUtils2/editor_gymmarker"
gymMarker.placements = {
    name = "controller",
    data = {
        name = "",
        order = 0,
        color = "f2e0cb",
        learnedColor = "abf797",
        legacyRenderMode = false
    }
}

gymMarker.fieldInformation = {
    order = {
        fieldType = "integer",
        minimumValue = 0
    },
    color = {
        fieldType = "color",
        options = { "f2e0cb", "56b3ff", "ff6d81", "ffff89", "ff9e66", "dd87ff" },
        editable = true
    },
    learnedColor = {
        fieldType = "color",
        options = { "abf797", "a7e2f9", "faa7bc", "fbf8b8", "fbd0a6", "f3bafa" },
        editable = true
    }
}

return gymMarker
