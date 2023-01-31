local lobbyMapMarker = {}
lobbyMapMarker.name = "CollabUtils2/LobbyMapMarker"
lobbyMapMarker.depth = -100
lobbyMapMarker.placements = {
    {
        name = "default",
        data = {
            icon = "CollabUtils2/lobbies/memorial",
            dialogKey = "",
        }
    }
}

function lobbyMapMarker.texture(room, entity)
    return entity.icon or "CollabUtils2/lobbies/memorial"
end

return lobbyMapMarker


