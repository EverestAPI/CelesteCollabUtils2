module CollabUtils2MiniHeartDoor

using ..Ahorn, Maple

@mapdef Entity "CollabUtils2/MiniHeartDoor" MiniHeartDoor(x::Integer, y::Integer, width::Integer=40, height::Integer=Maple.defaultBlockHeight,
    requires::Integer=0, startHidden::Bool=false, levelSet::String="SpringCollab2020/1-Beginner", color::String="beginner", doorID::String="")

const placements = Ahorn.PlacementDict(
    "Mini Heart Door (Collab Utils 2 / READ DOCS)" => Ahorn.EntityPlacement(
        MiniHeartDoor,
        "rectangle"
    )
)

Ahorn.editingOptions(entity::MiniHeartDoor) = Dict{String, Any}(
  "color" => String["beginner", "intermediate", "advanced", "expert", "grandmaster"]
)

Ahorn.minimumSize(entity::MiniHeartDoor) = 40, 8
Ahorn.resizable(entity::MiniHeartDoor) = true, true

Ahorn.nodeLimits(entity::MiniHeartDoor) = 0, 1

const heartPadding = 4
const wallColor = (47, 187, 255, 255) ./ 255

function Ahorn.selection(entity::MiniHeartDoor, room::Maple.Room)
    nodes = get(entity.data, "nodes", ())

    if isempty(nodes)
        return Ahorn.getEntityRectangle(entity)
    else
        nx, ny = Int.(nodes[1])
        width = get(entity.data, "width", 40)

        return [Ahorn.getEntityRectangle(entity), Ahorn.Rectangle(nx - 8, ny, width + 16, 8)]
    end
end

function heartsWidth(heartSprite::Ahorn.Sprite, hearts::Integer)
    return hearts * (heartSprite.width + heartPadding) - heartPadding
end

function heartsPossible(width::Integer, edgeSprite::Ahorn.Sprite, heartSprite::Ahorn.Sprite, required::Integer)
    rowWidth = width - 2 * edgeSprite.width

    for i in 0:required
        if heartsWidth(heartSprite, i) > rowWidth
            return i - 1
        end
    end
    
    return required
end

function Ahorn.renderSelectedAbs(ctx::Ahorn.Cairo.CairoContext, entity::MiniHeartDoor, room::Maple.Room)
    x, y = Ahorn.position(entity)
    nodes = get(entity.data, "nodes", ())

    width = Int(get(entity.data, "width", 8))

    if !isempty(nodes)
        nx, ny = Int.(nodes[1])
        dy = ny - y

        Ahorn.drawRectangle(ctx, x, ny, width, 1, (1.0, 0.0, 0.0, 1.0), (0.0, 0.0, 0.0, 0.0))
        Ahorn.drawRectangle(ctx, x, 2 * y - ny, width, 1, (1.0, 0.0, 0.0, 1.0), (0.0, 0.0, 0.0, 0.0))

        Ahorn.drawRectangle(ctx, nx - 8, ny, width + 16, 8, (1.0, 0.0, 0.0, 1.0), (0.0, 0.0, 0.0, 0.0))
    end
end

# Not completely accurate on heart positions, but good enough
function Ahorn.renderAbs(ctx::Ahorn.Cairo.CairoContext, entity::MiniHeartDoor, room::Maple.Room)
    x, y = Ahorn.position(entity)

    width = Int(get(entity.data, "width", 40))
    height = get(entity.data, "height", Maple.defaultBlockHeight)
    hearts = Int(get(entity.data, "requires", 0))

    edgeSprite = Ahorn.getSprite("objects/heartdoor/edge", "Gameplay")
    heartSprite = Ahorn.getSprite("objects/heartdoor/icon00", "Gameplay")

    Ahorn.drawRectangle(ctx, x, y - height, width, height * 2, wallColor, (0.0, 0.0, 0.0, 0.0))

    for i in y-height:edgeSprite.height:y+height-1
        Ahorn.Cairo.save(ctx)

        Ahorn.drawImage(ctx, edgeSprite, x + width - edgeSprite.width, i)
        Ahorn.scale(ctx, -1, 1)
        Ahorn.drawImage(ctx, edgeSprite, -x - edgeSprite.width, i)

        Ahorn.Cairo.restore(ctx)
    end

    if hearts > 0
        fits = heartsPossible(width, edgeSprite, heartSprite, hearts)
        rows = ceil(Int, hearts / fits)

        for row in 1:rows
            fits = heartsPossible(width, edgeSprite, heartSprite, hearts)
            drawWidth = heartsWidth(heartSprite, fits)

            startX = x + round(Int, (width - drawWidth) / 2) + edgeSprite.width - 1
            startY = y - round(Int, rows / 2 * (heartSprite.height + heartPadding)) - heartPadding - 1
            
            for col in 1:fits
                drawX = (col - 1) * (heartSprite.width + heartPadding) - heartPadding
                drawY = row * (heartSprite.height + heartPadding) - heartPadding

                Ahorn.drawImage(ctx, heartSprite, startX + drawX, startY + drawY)
            end

            hearts -= fits
        end
    end
end

end