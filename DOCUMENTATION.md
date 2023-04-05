# Collab Utils 2 Documentation

The collab utils have a few entities that come in handy when making a collab, or any map with a similar structure, with hubs giving access to maps.
But they also come with some _extra behavior_ that requires you to follow a certain structure to make everything work as intended.

This doc will explain you how to set up that structure and how the entities work. **Please read it before asking questions!** Maybe the answers are in there. :sweat_smile:

If anything is wrong or unclear, yell at Maddie (maddie480#4596 on [the Celeste Discord](https://discord.gg/celeste)) about that.

## Table of Contents

- [Setting up your mod as a collab](#setting-up-your-mod-as-a-collab)
- [Entities](#entities)
  - [Crystal Hearts (Return to Lobby)](#crystal-hearts-return-to-lobby)
  - [Golden Berry Respawn Points](#golden-berry-respawn-points)
  - [Silver Berries](#silver-berries)
  - [Silver Blocks](#silver-blocks)
  - [Mini Hearts](#mini-hearts)
  - [Mini Heart Doors](#mini-heart-doors)
  - [Rainbow Berries](#rainbow-berries)
  - [Speed Berries](#speed-berries)
- [Triggers](#triggers)
  - [Chapter Panel Trigger](#chapter-panel-trigger)
  - [Journal Trigger](#journal-trigger)
- [Map Metadata](#map-metadata)
  - [Randomized session flags ("weather")](#randomized-session-flags-weather)
  - [Stickers on the journal](#stickers-on-the-journal)
- [Lazy Loading](#lazy-loading)
- ["Learn" tab in the chapter panel with gym teleports](learn-tab-in-the-chapter-panel-with-gym-teleports)

## Setting up your mod as a collab

_You will need a mod folder for this. Head to the [Mod Structure page on the Everest wiki](https://github.com/EverestAPI/Resources/wiki/Mod-Structure) if you don't have that yet._

For the collab utils to fully function, you should organize your map .bins for it to be recognized as a collab, and to associate the lobbies to the maps that are in it.

_Some features like speed berries don't require this, but if you plan to structure your mod like the Spring Collab (using mini hearts, lobbies, silver berries etc), you should definitely follow this._

As an example, we will set up the _2021 Season Collab_, with 4 lobbies: Spring, Summer, Fall and Winter.

1. Pick a unique name for your collab, preferably with just letters and numbers (no spaces or symbols). For example, _2021SeasonCollab_
2. Get an everest.yaml file (check the [Mod Structure page](https://github.com/EverestAPI/Resources/wiki/Mod-Structure) if you don't know how to do that). Give your mod the `Name` you chose:
```yaml
- Name: 2021SeasonCollab
  Version: 1.0.0
  Dependencies:
    - Name: Everest
      Version: 1.1375.0
```
3. Next to that everest.yaml, create a new file named `CollabUtils2CollabID.txt` containing just your collab name. This will tell the collab utils that this is a collab, so that it gets treated as such:
```
2021SeasonCollab
```
4. Create a Maps folder, then a folder named like your collab. So, `Maps/2021SeasonCollab`
5. This folder should have a folder named `0-Lobbies` containing the lobbies, and one folder per lobby containing all maps that can be accessed from that lobby. For example:
```
Maps/
    2021SeasonCollab/
        0-Lobbies/
            1-Spring.bin
            2-Summer.bin
            3-Fall.bin
            4-Winter.bin
        1-Spring/
            map1.bin
        2-Summer/
            map2.bin
            map3.bin
        3-Fall/
            map4.bin
            map5.bin
        4-Winter/
            map6.bin
```
:arrow_up: Here, map 1 is in the Spring lobby, maps 2 and 3 in the Summer lobby, 4 and 5 in the Fall lobby, and 6 in the Winter lobby.
Note that **the lobby bins and the corresponding folders are named the same**, and that's how the collab utils know they match.

6. In your English.txt, define your mod name:
```
modname_2021SeasonCollab= 2021 Season Collab
endscreen_collabname_2021SeasonCollab= 2021SC
```
:arrow_up: The first line defines your mod name in the updater (it's actually an Everest feature).
The second line defines the collab name that will appear on the endscreen, along with the collab version, when the speedrun timer is enabled.

**Your collab is now set up!** When starting up the game, you should notice that all the lobbies are unlocked right away, and that only them are visible in chapter select.

### Some extra features depending on folder structure

- If you want a **prologue**, put it in the `0-Lobbies` folder and name it `0-Prologue.bin`. By doing so, players will have to complete it before unlocking lobbies.
- If you want something similar to gyms (a map associated to a lobby that doesn't show up in that lobby's journal), you can create an extra `0-Gyms` folder, and put bins that are named the same as the lobbies in it:
```
Maps/
    2021SeasonCollab/
        0-Gyms/
            1-Spring.bin
            2-Summer.bin
            3-Fall.bin
            4-Winter.bin
        0-Lobbies/
            1-Spring.bin
            2-Summer.bin
            3-Fall.bin
            4-Winter.bin
```

- When in a lobby, **session flags will be set for every map the player has beaten in this lobby**. These flags are named `CollabUtils2_MapCompleted_{binname}`. In the example above, if you beat map3, the session flag `CollabUtils2_MapCompleted_map3` will be set when you enter the `2-Summer` lobby. For example, you can use this to make stylegrounds (dis)appear, or to trigger flag-controlled entities like flag switch gates (from Maddie's Helping Hand) or flag temple gates (from Pandora's Box), allowing you to open/close paths.

## Entities

### Crystal Hearts (Return to Lobby)

Work just like regular Crystal Hearts, except they return to lobby instead of returning to map once collected.

If "Display End Screen For All Maps" is enabled in Mod Options, the endscreen information (total time, map name and version information) wiill appear at the same time as the poem text.

### Golden Berry Respawn Points

If the player dies with a golden berry and the level restarts, they will respawn at the golden berry respawn point instead of the default spawn point in the room.

This one works with everything triggering a "golden berry restart" (this includes silver berries and speed berries).

### Silver Berries

Those work pretty much like golden berries, and can be collected by crossing a Golden Berry Collect Trigger or when hitting a mini heart.

They are intended for collab entries, and count towards unlocking the rainbow berry.

### Silver Blocks

Same as Golden Blocks, except appearing only when the player carries a silver berry. (Golden Blocks appear when the player carries a golden, silver or speed berry, for technical reasons.)

### Mini Hearts

They are collected like regular hearts, except doing so will not display any heart message, and will send you back to the lobby instead of the overworld. So, this is meant to end collab entries.

Collecting it will mark the crystal heart on the current map as collected.

You can have custom sprites for them, by placing them in `Graphics/Atlases/Gameplay/CollabUtils2/miniheart/someUniqueName` and using `someUniqueName` as a "sprite" in Ahorn.

To make the heart on the chapter panel mini as well, and customize it if you want, you will need to create a sprite XML: create a file in `Graphics/CollabUtils2/CrystalHeartSwaps_YourCollabName.xml` and fill it like this.
```xml
<?xml version="1.0" encoding="utf-8" ?>
<Sprites>
  <!-- The name should be: crystalHeart_CollabName_LobbyName -->
  <crystalHeart_2021SeasonCollab_1_Spring path="CollabUtils2/miniHeart/bgr/" start="idle">
    <Center />
    <Loop id="idle" path="" frames="0" />
    <Loop id="spin" path="" frames="0*10,1-10" delay="0.08"/>
    <Loop id="fastspin" path="" frames="1-10" delay="0.08"/>
  </crystalHeart_2021SeasonCollab_1_Spring>
</Sprites>
```

You can use the hearts from the 2020 Spring Collab by replacing "bgr" in the path with "imd", "adv", "exp" or "gdm".
You can also use custom ones by dropping them somewhere in `Graphics/Atlases/Gui/YourCollabName` and changing the `path` (it works the same way as Sprites.xml).

**Note that you can also use this file to reskin the crystal hearts of any level, on the chapter panel and on the poem screen**. For that, use the full path to the map, like you would for naming the map in English.txt:

```xml
  <crystalHeart_2021SeasonCollab_0_Lobbies_2_Summer path="collectables/heartgem/2/" start="idle">
    <Center />
    <Loop id="idle" path="spin" frames="0" />
    <Loop id="spin" path="spin" frames="0*10,1-10" delay="0.08"/>
    <Loop id="fastspin" path="spin" frames="1-10" delay="0.08"/>
  </crystalHeart_2021SeasonCollab_0_Lobbies_2_Summer>
```

You can pick which heart to use by changing the `path`: you can use the vanilla hearts (`collectables/heartgem/0/` with 0 = blue, 1 = red, 2 = yellow, 3 = grey/ghost), ones from the collab (`CollabUtils2/crystalHeart/expert/` for orange and `CollabUtils2/crystalHeart/grandmaster/` for purple), or custom ones by dropping them somewhere in `Graphics/Atlases/Gui/YourCollabName`.

If you use one of the vanilla crystal heart sprites (`collectables/heartgem/1/`, `collectables/heartgem/2/`) or the "expert" or "grandmaster" sprites that ship with the collab utils (`CollabUtils2/crystalHeart/expert/`, `CollabUtils2/crystalHeart/grandmaster/`), the text color on the poem screen will be changed accordingly to red, yellow, orange or purple.

If you want a custom text color on the poem screen, include `poemtextcolor_[hexcode]` somewhere in the texture path: for example `path="MyMod/customcrystalheart_poemtextcolor_ff0000/"`.

For B-side and C-side hearts, add `_B` or `_C` at the end of the sprite name. For example, for the B-side of 2021SeasonCollab_0_Lobbies_2_Summer: `crystalHeart_2021SeasonCollab_0_Lobbies_2_Summer_B`

### Fake Mini Hearts

Those look exactly like Mini Hearts, but disappear for 3 seconds when dashed into, like vanilla fake hearts.

### Mini Heart Doors

They're pretty much heart gates, but you can customize their height, and make them count the hearts on the level set you want. They also open with a cutscene, instead of Maddy having to stand next to them.

If you followed the setup at the beginning of this document, the level set should look like `CollabName/LobbyName`. For example, `2021SeasonCollab/1-Spring`

To trigger the unlock cutscene when the player comes back to the lobby with enough crystal hearts to open the gate, drop a **Mini Heart Door Unlock Cutscene Trigger** around the door.
The cutscene will trigger as soon as the player enters it if the conditions are met, and it will only happen once per save file.

If you have multiple heart doors in your lobby, and don't want them to open all at the same time (for example because they are referring to different level sets), give your doors different _IDs_ (with the "Door Id" field). You should then use the same ID in the unlock cutscene trigger lo link the door to its trigger.

**Tip:** If you use these doors without an unlock cutscene trigger, they behave like vanilla mini heart doors, but approachable from both sides, with resizable height, and all using their own flags (so opening one won't open all heart gates with the same amount of hearts). You shouldn't use a mix of vanilla heart doors and mini heart doors in the same map though.

### Rainbow Berries

This is a special berry that will appear as a hologram until the player got all silver berries in the level set they're associated to. **Only silver berries count**, golden berries are excluded from this counter.

If you followed the setup at the beginning of this document, the level set should look like `CollabName/LobbyName`. For example, `2021SeasonCollab/1-Spring`

To trigger the unlock cutscene when the player comes back to the lobby with enough silver berries to unlock the rainbow berry, drop a **Rainbow Berry Unlock Cutscene Trigger** around the berry.
The cutscene will trigger as soon as the player enters it if the conditions are met, and it will only happen once per save file.

### Speed Berries

When grabbed by the player, a timer will appear, and it will start as soon as the next screen transition ends. You can set 3 times, Gold, Silver and Bronze, and the player dies and restarts the level if the timer goes over the bronze time.

The best time appears on the chapter panel, and on the lobby and overworld journal. _Note that it won't appear in the journal if you didn't follow the collab setup at the start of this document, but it will still appear on the chapter panel._

To stop the timer and collect the berry, you need to place a **Speed Berry Collect Trigger**. The berry won't count in the strawberry counter.

:warning: If the player crosses a Golden Berry Collect Trigger, the speed berry will collect but the timer will not stop. This is an odd interaction, and you should make sure to have the player cross a speed berry collect trigger first.

## Triggers

### Chapter Panel Trigger

This trigger will bring up the chapter panel, like in chapter select, but within a map. This is intended for lobbies.

The trigger is the zone in which the player will be able to bring up the chapter panel by pressing Talk. The node is where the speech bubble will be.

The "Map" setting should be the path to your bin, without the .bin at the end. If you followed the collab structure, it should look like `CollabName/LobbyName/binname` (for example `2021SeasonCollab/2-Summer/map3`).

The chapter panel works in the same way as the ones in chapter select (for chapter naming, the icon and colors). The only differences are:
- Instead of showing a chapter number, it will display **a map author**. To define that, use English.txt and define a new dialog with the same ID as the map name + an `_author` suffix:
```
2021SeasonCollab_1_Spring_map1_author= by Matt Makes Games
```

- Instead of showing checkpoints, it will display **credits**. To define them, use English.txt and define a new dialog with the same ID as the map name + a `_collabcredits` suffix:
```
2021SeasonCollab_1_Spring_map1_collabcredits=
  Map by Maddy Thorson
  Code by Noel Berry
  Art by Pedro Medeiros
```

If you don't define this dialog ID, the credits page will be skipped.

- You can also add **tags** to the credits: those will be displayed in smaller text under credits, each in its own rectangle. To define them, use English.txt and define a new dialog with the same ID as the map name + a `_collabcreditstags` suffix, and write 1 tag per line:
```
2021SeasonCollab_1_Spring_map1_collabcreditstags=
  Tag 1
  Tag 2
  Tag 3
```

#### The "Return to Lobby" option

When using a Chapter Panel Trigger, you can make a "Return to Lobby" button appear in the pause menu. This depends on the "return to lobby mode" you set on the chapter panel trigger:
- "Set Return to Here": when the player will hit Return to Lobby, they will be returned to the current map and room, on the spawn point that is closest to the chapter panel trigger. Useful for lobby > map teleports.
- "Remove Return": the "Return to Lobby" button will be removed from the pause menu. Useful for lobby > lobby or map > lobby teleports.
- "Do Not Change Return": the "Return to Lobby" button won't be changed. Useful for teleports within maps.

If you followed the collab structure described at the beginning of this document, you'll also get a fallback measure: if someone uses the `load` command to teleport straight into a map, the collab utils will automatically add a "Return to Lobby" button to the corresponding lobby.
Since this is a fallback, using it will bring the player back to the starting point of the lobby.

#### Customizing the skull (death counter) for a lobby

If you want to have custom skulls for a lobby, drop the skull on the following paths:
- `Graphics/Atlases/Gui/CollabUtils2/skulls/CollabName/LobbyName.png`: for the chapter panel
- `Graphics/Atlases/Journal/CollabUtils2Skulls/CollabName/LobbyName.png`: for the "deaths" column in the journal
- `Graphics/Atlases/Journal/CollabUtils2MinDeaths/CollabName/LobbyName.png`: for the "minimum deaths" column in the journal

If you don't define those images, they will use A-side skulls instead.

You can take the skulls that ship with the collab utils as a reference.

You can change the chapter panel skull icon for **any** campaign using this feature: if the maps are in `Maps/foldername`, put the skull icon at `Graphics/Atlases/Gui/CollabUtils2/skulls/foldername`.

### Journal Trigger

This trigger allows you to bring up a journal similar to the overworld, but showing your progress on a particular level set. This journal will also include speed berry PBs.

The trigger is the zone in which the player will be able to bring up the journal by pressing Talk. The node is where the speech bubble will be.

If you followed the setup at the beginning of this document, the level set should look like `CollabName/LobbyName`. For example, `2021SeasonCollab/1-Spring`

**If you have a heart side** or similar (a level that only unlocks after you beat all other maps in the lobby), name the bin `ZZ-HeartSide.bin` and place it along with the other maps in the lobby; it will be hidden from the journal until it's unlocked (all mini hearts in the lobby have been collected), and will be displayed separately (much like Farewell in the vanilla journal).

**If your collab uses chapter icons to represent difficulties and you want them to be ordered in the journal**, make all your icons start with numbers. For example, `1-easy.png`, `2-medium.png` and `3-hard.png`.

#### More customization for the journal

**If you want the hearts to have a custom color or graphic** in the journal for a lobby, you can put the image for it in `Graphics/Atlases/Journal/CollabUtils2Hearts/CollabName/LobbyName.png`. If the line you want to customize does not match a lobby (for example for a Prologue), put it in `Graphics/Atlases/Journal/CollabUtils2Hearts/CollabName/0-Lobbies/BinName.png` instead.

If you don't define this, the blue heart will be used by default.

You can also **define a custom display name for a lobby** in the overworld journal, by adding an entry in English.txt. Define a new dialog with the same ID as the map name + a `_journal` suffix:
```
2021SeasonCollab_0_Lobbies_2_Summer_journal= Summer
```

This is useful to make the journal say "Beginner Difficulty" instead of "Beginner Lobby" for example. This is optional; if you don't define it, the map name will be displayed instead.

**To put an image in the last page of a lobby journal**, put it in `Graphics/Atlases/Journal/collabLobbyMaps/CollabName/LobbyName.png`. The Spring Collab uses this to display a rough map of the lobby.

## Map Metadata

### Randomized session flags ("weather")

Collab Utils allow you to set randomized session flags in your map meta.yaml:
```yaml
CollabUtilsRandomizedFlags:
    flag1: 0.2
    flag2: 0.5
```
With that setup, when entering the map, flag1 will have a 50% chance to be set, and flag2 will have a 20% chance to be set. **Both cannot be set at the same time**: in that example, that means there is a 30% chance no flag will be set.

### Stickers on the journal

You can add stickers on the journal front page by adding the following to your **lobby's** meta.yaml:
```yaml
Stickers:
  - Path: some/unique/path
    FinishedMaps:
      - foldername/mapname1
      - foldername/mapname2
    X: 150
    Y: 150
    Scale: 1.5 # optional
    Rotation: -20 # optional
```

This configuration will make the sticker at `Graphics/Atlases/Stickers/some/unique/path.png` appear on the journal front page at (150, 150) with 1.5x scale and -20 degrees rotation, if you finished foldername/mapname1 **and** foldername/mapname2.

## Lazy Loading

Lazy Loading is useful for large collabs, to prevent the game from loading all of your mod's graphics, and instead only load what is required for the map you are playing.

You can set up Lazy Loading for your mod by creating a `CollabUtils2LazyLoading.yaml` file at the root of your mod, next to `everest.yaml`, that should look like this:

```yaml
Enable: true
ExcludedPrefixes:
    Gui:
        - MyCollab/DoNotLazyLoad/
    Gameplay:
        - decals/DoNotLazyLoad/
```

The prefixes you list (relative to `Graphics/Atlases/Gui` and `Graphics/Atlases/Gameplay`) are sprites that should **not** be lazily loaded (for example, because they are used in the overworld).

By default, **all** Gameplay sprites are lazily loaded, and **all** Gui sprites **except** those located in `areas/`, `emoji/` and `CollabUtils2/skulls/` are lazily loaded (those are commonly used outside of maps). If you don't need to exclude any more sprites than that, you can omit `ExcludedPrefixes` entirely!

By doing that and restarting the game, the sprites that are set to be lazily loaded **will not be loaded on startup anymore**, speeding up the startup by a fair bit. Instead, textures will be loaded **when you run into them in-game**. This has a drawback though: loading the texture takes a bit of time, which can cause stutters during gameplay.

To help mitigate this, Collab Utils collects the list of textures it lazily loaded and saves them, so that next time you enter the map, **they are loaded when you enter the map instead**. No more stutter during gameplay! That list of textures is saved when you **leave** the map, in a file ending with `.texturecache.txt` in `Mods/Cache/CollabUtils2`.

But you probably don't want gameplay to be stuttery the first time players play each map! To prevent that, **you should ship the .texturecache.txt files with your maps**. You just need to move the files out of your Cache folder and to put them next to your map bins. You should play through the map in order to see every texture (including secrets) before doing that.

Note that if some textures are missing from the `.texturecache.txt` file you shipped with the map, Collab Utils will create the `.texturecache.txt` file in `Mods/Cache/CollabUtils2` again, and you will find a warning in log.txt saying: `Found X lazily loaded texture(s)! Saving them at [path].` This file only contains missing textures, so in order to add them to the `.texturecache.txt` file that ships with your mod, you should merge the files, rather than replacing the one shipping with your mod.

## "Learn" tab in the chapter panel with gym teleports

You can add a second tab to your collab maps in the lobby, that lists the tech used in a map and allows the player to teleport to a room teaching it:

![image](https://cdn.discordapp.com/attachments/445236692136230943/1045411274881761390/image.png)

Here is how you can do this:

### Making the gym

- Make a separate bin for the gyms, putting it in the `0-Gyms` folder: refer to [above](#some-extra-features-depending-on-folder-structure) for more details.
- Make one room per tech you want to teach, and put an **Exit From Gym Trigger** at the end of them. This brings up the chapter panel of the map the player was originally entering, similar to the Chapter Panel Trigger, and this allows them to either pick another tech, enter the map, or return to the lobby.
- Put a **Gym Marker** in the room (this is an invisible entity), and change its settings to designate a difficulty and give the tech a name (only use letters, numbers and _ in that name!).

### Setting up the chapter panel visuals

- Put the **image to display for the tech in the chapter panel** in `Graphics/Atlases/Checkpoints/{CollabName}/Gyms/{TechName}.png`.
- Add the **name of the tech** in your `English.txt`: `{CollabName}_gym_{TechName}_name= The Tech Name`

### Listing tech used in each map

In order to make the "Learn" tab with the tech list show up in the chapter panel, just fill out the "Tech" option of the Chapter Panel Trigger. Separate the tech names with commas, and don't put spaces: `wavedash,wallbounce`.
