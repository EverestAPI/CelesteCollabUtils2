# Collab Utils 2 Documentation

The collab utils have a few entities that come in handy when making a collab, or any map with a similar structure, with hubs giving access to maps.
But they also come with some _extra behavior_ that requires you to follow a certain structure to make everything work as intended.

This doc will explain you how to set up that structure and how the entities work. **Please read it before asking questions!** Maybe the answers are in there. :sweat_smile:

## Setting up your mod for the collab utils

_You will need a mod folder for this. Head to the [Mod Structure page on the Everest wiki](https://github.com/EverestAPI/Resources/wiki/Mod-Structure) if you don't have that yet._

As an example, we will set up the _2021 Season Collab_, with 4 lobbies: Spring, Summer, Fall and Winter.

1. Pick a unique name for your collab, preferably with just letters and numbers (no spaces or symbols). For example, _2021SeasonCollab_
2. Get an everest.yaml file (check the [Mod Structure page](https://github.com/EverestAPI/Resources/wiki/Mod-Structure) if you don't know how to do that). Give your mod the `Name` you choose:
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
