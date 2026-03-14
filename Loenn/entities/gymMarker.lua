local gymMarker = {}

gymMarker.name = "CollabUtils2/GymMarker"
gymMarker.depth = 0
gymMarker.texture = "CollabUtils2/editor_gymmarker"
gymMarker.placements = {
    name = "controller",
    data = {
        name = "",
        order = 0,
        color = "",
        learnedColor = "",
        legacyRenderMode = false
    }
}

gymMarker.ignoredFields = {
    "_name", "_id", "originX", "originY",
    "legacyRenderMode"
}

gymMarker.fieldInformation = {
    order = {
        fieldType = "integer",
        minimumValue = 0
    },
    color = {
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
