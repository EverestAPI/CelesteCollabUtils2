local miniHeart = {}
miniHeart.name = "CollabUtils2/MiniHeart"
miniHeart.depth = 0
miniHeart.placements = {
    {
        name = "default",
        data = {
            sprite = "beginner",
            refillDash = true,
            requireDashToBreak = true,
            noGhostSprite = false,
            particleColor = "",
            playPulseSound = true,
            flash = false
        }
    }
}

miniHeart.fieldInformation = {
    sprite = {
        options = { "beginner", "intermediate", "advanced", "expert", "grandmaster" }
    }
}

function miniHeart.texture(room, entity)
    return "CollabUtils2/miniheart/" .. entity.sprite .. "/00"
end

return miniHeart
