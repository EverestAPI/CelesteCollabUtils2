local fakeMiniHeart = {}
fakeMiniHeart.name = "CollabUtils2/FakeMiniHeart"
fakeMiniHeart.depth = 0
fakeMiniHeart.placements = {
    {
        name = "default",
        data = {
            sprite = "beginner",
            refillDash = true,
            requireDashToBreak = true,
            noGhostSprite = false,
            particleColor = "",
        }
    }
}

fakeMiniHeart.fieldInformation = {
    sprite = {
        options = { "beginner", "intermediate", "advanced", "expert", "grandmaster" }
    }
}

function fakeMiniHeart.texture(room, entity)
    return "CollabUtils2/miniHeart/" .. entity.sprite .. "/00"
end

return fakeMiniHeart
