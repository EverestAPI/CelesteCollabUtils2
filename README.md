# Collab Utils 2

This mod adds some entities and features that are useful for collabs:

- **Chapter Panel and Journal Triggers**: those triggers allow you to bring up a chapter panel to teleport to a map, or a journal showing your progress in a particular level set. This is useful to make lobbies.
- **Mini Hearts**: a heart that ends a map and returns you to its lobby. (This mod also provides you a **Fake Mini Heart** in case you need that.)
- **Mini Heart Doors**: like vanilla heart doors, those can be used to block progress until the player has enough hearts... but those have customizable color and height, and can be opened from both sides! You can also choose which level set to check (which is useful for lobbies).
- **Silver Berries**: golden berries meant for collab entries. You can also drop a **Rainbow Berry** somewhere, that will be unlocked when the player collects all silver berries in a level set. In case you need an equivalent of vanilla Golden Blocks, **Silver Blocks** only appear when the player carries a silver berry with them. They can be collected before the end of the level with Golden Berry Collect Triggers, or with the dedicated **Silver Berry Collect Triggers**.
- **Speed Berries**: berries that time you against set Gold, Silver and Bronze times. If the timer goes above the Bronze time, the berry explodes and the player has to restart the chapter (like a golden death).
- **Golden Berry Respawn Points**: respawn points that are used when the chapter restarts after the player dies with a golden, silver or speed berry. Can be used to make golden runs less annoying (by respawning after a tutorial section, for example).
- A few more features that aren't visible in Ahorn, such as **hiding level sets from chapter select** and **reskinning crystal hearts on chapter panels**

You need to install it to play the [2020 Celeste Spring Community Collab](https://gamebanana.com/maps/211745).

If you intend to use the collab utils as a dependency for your own map, please read [the documentation here](https://github.com/EverestAPI/CelesteCollabUtils2/blob/master/DOCUMENTATION.md). It explains you how the entities and features work in more detail, and gives you the mod structure to follow if you want to make a collab organized like the 2020 Spring Collab.

Note that rainbow berries, speed berries and silver berries can be reskinned with Sprites.xml. For that, follow [the same procedure as vanilla reskins](https://github.com/EverestAPI/Resources/wiki/Reskinning-Entities#reskinning-entities-through-spritesxml), but taking from `Graphics/Sprites.xml` inside the mod zip instead.

## Download

This mod is available for download [on GameBanana](https://gamebanana.com/mods/53704).
