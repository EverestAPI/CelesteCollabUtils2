using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    // This class allows turning on lazy loading on a map pack by dropping a CollabUtils2LazyLoading.txt file at its root.
    // After doing that, play the maps and you should see texturecache.txt files appear in your Mods/Cache/CollabUtils2 folder;
    // you should ship those in the Maps folder. This tells the game which graphics should be loaded with each map.
    public static class LazyLoadingHandler {
        // complete list of all textures in maps that have CollabUtils2LazyLoading = true
        private static HashSet<string> lazilyLoadedTextures = new HashSet<string>();

        // map SID => list of texture paths that have to be loaded when entering that map 
        private static Dictionary<string, HashSet<string>> pathsPerMap = new Dictionary<string, HashSet<string>>();

        // map SID => list of textures that were preloaded matching those in pathsPerMap, so that we can actually load them when entering the map.
        private static Dictionary<string, HashSet<VirtualTexture>> texturesPerMap = new Dictionary<string, HashSet<VirtualTexture>>();

        // textures that were lazily loaded, and therefore should have been loaded in advance!
        private static HashSet<string> newPaths = new HashSet<string>();

        // these Gui assets won't be lazily loaded no matter what
        private static readonly List<string> guiExcludedFromLazyLoading = new List<string>() { "areas/", "emoji/", "CollabUtils2/skulls/" };

        // lazy loading config that can be filled in CollabUtils2LazyLoading.yaml
        private class LazyLoadingConfig {
            public class PrefixesExcludedFromLazyLoading {
                public List<string> Gui { get; set; } = new List<string>();
                public List<string> Gameplay { get; set; } = new List<string>();
            }

            public bool Enable { get; set; } = false;
            public PrefixesExcludedFromLazyLoading ExcludedPrefixes { get; set; } = new PrefixesExcludedFromLazyLoading();
        }

        private static string latestMapSID = null;
        private static bool preloadingTextures = false;
        private static ILHook hookOnTextureSafe;

        public static void Load() {
            IL.Celeste.Mod.Everest.Content.Crawl += registerLazyLoadingModsOnLoad;
            IL.Monocle.VirtualTexture.Preload += turnOnLazyLoadingSelectively;
            On.Celeste.LevelLoader.ctor += lazilyLoadTextures;
            On.Monocle.VirtualTexture.Reload += onTextureLazyLoad;
            Everest.Events.Level.OnExit += saveNewLazilyLoadedPaths;

            hookOnTextureSafe = new ILHook(typeof(VirtualTexture).GetMethod("get_Texture_Safe"), lazyLoadTexturesOnAccess);

            // check all mods that were registered before us.
            foreach (ModContent modContent in Everest.Content.Mods) {
                registerLazyLoadingMods(modContent);
            }
        }

        public static void Unload() {
            IL.Celeste.Mod.Everest.Content.Crawl -= registerLazyLoadingModsOnLoad;
            IL.Monocle.VirtualTexture.Preload -= turnOnLazyLoadingSelectively;
            On.Celeste.LevelLoader.ctor -= lazilyLoadTextures;
            On.Monocle.VirtualTexture.Reload -= onTextureLazyLoad;
            Everest.Events.Level.OnExit -= saveNewLazilyLoadedPaths;

            hookOnTextureSafe?.Dispose();
            hookOnTextureSafe = null;
        }

        private static void registerLazyLoadingModsOnLoad(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // this place is right after we listed the files in the mod, but right before we start loading them (if loading them after startup)
            // because that includes loading textures, and we want to have all lazily loaded textures listed before that.
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<ModContent>("_Crawl"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<ModContent>>(registerLazyLoadingMods);
        }

        private static void registerLazyLoadingMods(ModContent modContent) {
            Logger.Log("CollabUtils2/LazyLoadingHandler", "Checking mod " + modContent.Name);

            LazyLoadingConfig config = null;

            if (modContent.Map.ContainsKey("CollabUtils2LazyLoading")) {
                // parse config as yaml
                using (TextReader textReader = new StreamReader(modContent.Map["CollabUtils2LazyLoading"].Stream)) {
                    config = YamlHelper.Deserializer.Deserialize<LazyLoadingConfig>(textReader);
                }
            }

            if (config != null && config.Enable) {
                // lazy loading activated!
                foreach (KeyValuePair<string, ModAsset> asset in modContent.Map) {
                    // find out which gameplay sprites we should lazy load
                    if (asset.Value.Type == typeof(Texture2D) && asset.Key.StartsWith("Graphics/Atlases/Gameplay/")) {
                        if (!matchesPrefixInList("Graphics/Atlases/Gameplay/", asset.Key, config.ExcludedPrefixes.Gameplay)) {
                            lazilyLoadedTextures.Add(asset.Key);
                            Logger.Log("CollabUtils2/LazyLoadingHandler", asset.Key + " was registered for lazy loading");
                        }
                    }

                    // find out which GUI sprites we should lazy load
                    if (asset.Value.Type == typeof(Texture2D) && asset.Key.StartsWith("Graphics/Atlases/Gui/")) {
                        if (!matchesPrefixInList("Graphics/Atlases/Gui/", asset.Key, guiExcludedFromLazyLoading)
                            && !matchesPrefixInList("Graphics/Atlases/Gui/", asset.Key, config.ExcludedPrefixes.Gui)) {

                            lazilyLoadedTextures.Add(asset.Key);
                            Logger.Log("CollabUtils2/LazyLoadingHandler", asset.Key + " was registered for lazy loading");
                        }
                    }

                    if (asset.Value.Type == typeof(AssetTypeMap)) {
                        // we want to read the texturecache files associated with this map.
                        string mapName = asset.Key.Substring("Maps/".Length);

                        // look for a texturecache packaged along with the map
                        if (modContent.Map.TryGetValue(asset.Key + ".texturecache", out ModAsset textureCachePackaged) && textureCachePackaged.Type == typeof(AssetTypeText)) {
                            using (Stream assetStream = textureCachePackaged.Stream) {
                                Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", "Loading texture list for " + asset.Key + " from " + textureCachePackaged.PathVirtual);
                                fillInTexturesFromCache(mapName, assetStream);
                            }
                        }

                        // look for a texturecache in the Cache folder
                        string textureCachePath = Everest.Loader.PathCache + "/CollabUtils2/" + mapName + ".texturecache.txt";
                        if (File.Exists(textureCachePath)) {
                            using (FileStream stream = File.OpenRead(textureCachePath)) {
                                Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", "Loading texture list for " + asset.Key + " from " + textureCachePath);
                                fillInTexturesFromCache(mapName, stream);
                            }
                        }
                    }
                }
            }
        }

        private static bool matchesPrefixInList(string basePath, string toCheck, List<string> list) {
            foreach (string prefix in list) {
                if (toCheck.StartsWith(basePath + prefix)) {
                    return true;
                }
            }

            return false;
        }

        private static void fillInTexturesFromCache(string key, Stream input) {
            if (!pathsPerMap.TryGetValue(key, out HashSet<string> pathsForThisMap)) {
                pathsForThisMap = new HashSet<string>();
                pathsPerMap[key] = pathsForThisMap;
            }

            using (StreamReader reader = new StreamReader(input)) {
                string line;
                while ((line = reader.ReadLine()) != null) {
                    Logger.Log("CollabUtils2/LazyLoadingHandler", "Added " + line + " as a texture for " + key);
                    pathsForThisMap.Add(line);
                }
            }
        }

        // this turns on or off lazy loading based on which texture is being loaded.
        private static void turnOnLazyLoadingSelectively(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<CoreModuleSettings>("get_LazyLoading"));

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, VirtualTexture, bool>>((orig, self) => {
                // don't do anything if lazy loading is actually turned on or for textures with (somehow) no name.
                if (orig || self.Name == null)
                    return orig;

                string name = self.Name;
                if (lazilyLoadedTextures.Contains(name)) {
                    Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", name + " was skipped and will be lazily loaded later");

                    // look for maps that use this, so that we can fill out texturesPerMap as we go through all textures.
                    foreach (KeyValuePair<string, HashSet<string>> mapGraphics in pathsPerMap) {
                        if (mapGraphics.Value.Any(path => name == path)) {
                            Logger.Log("CollabUtils2/LazyLoadingHandler", name + " is associated to map " + mapGraphics.Key);

                            // associate the (non-loaded) texture to the map so that it can be loaded more easily later.
                            if (!texturesPerMap.TryGetValue(mapGraphics.Key, out HashSet<VirtualTexture> list)) {
                                list = new HashSet<VirtualTexture>();
                                texturesPerMap[mapGraphics.Key] = list;
                            }
                            list.Add(self);
                        }
                    }

                    // this triggers lazy loading: preload of the texture, but not actually load it in video RAM.
                    return true;
                }

                // this disables lazy loading, and the game will actually load the texture.
                return false;
            });
        }

        private static void lazilyLoadTextures(On.Celeste.LevelLoader.orig_ctor orig, LevelLoader self, Session session, Vector2? startPosition) {
            if (latestMapSID != session.Area.GetSID()) {
                writeNewPaths(latestMapSID);

                // get the textures specific to the map we come from (if any) and the map we go to.
                if (latestMapSID == null || !texturesPerMap.TryGetValue(latestMapSID, out HashSet<VirtualTexture> texturesInOldMap)) {
                    texturesInOldMap = new HashSet<VirtualTexture>();
                }
                if (!texturesPerMap.TryGetValue(session.Area.GetSID(), out HashSet<VirtualTexture> texturesInNewMap)) {
                    texturesInNewMap = new HashSet<VirtualTexture>();
                }

                // we are loading textures, but this is NOT Everest lazily loading them!
                preloadingTextures = true;

                // textures to unload = textures that are in the old map but not the new one.
                foreach (VirtualTexture tex in texturesInOldMap.Except(texturesInNewMap)) {
                    Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", "Unloading texture: " + tex.Name);
                    tex.Unload();
                }

                // textures to load = textures that are in the new map but not the old one.
                foreach (VirtualTexture tex in texturesInNewMap.Except(texturesInOldMap)) {
                    Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", "Loading texture: " + tex.Name);
                    tex.Reload();
                }

                preloadingTextures = false;
            }

            latestMapSID = session.Area.GetSID();
            orig(self, session, startPosition);
        }

        private static void lazyLoadTexturesOnAccess(ILContext il) {
            ILCursor cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instr => instr.MatchCallvirt<CoreModuleSettings>("get_LazyLoading"));

            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, VirtualTexture, bool>>((orig, self) => {
                // don't do anything if lazy loading is actually turned on or for textures with (somehow) no name.
                if (orig || self.Name == null)
                    return orig;

                // texture is lazily loaded if it is in our list.
                // this will make Everest actually check if the texture is loaded, and call Reload() if it is not.
                string name = self.Name;
                return lazilyLoadedTextures.Contains(name);
            });
        }

        private static void onTextureLazyLoad(On.Monocle.VirtualTexture.orig_Reload orig, VirtualTexture self) {
            // this is actually called on every texture load, so we need to check if this is a lazy load or not
            string name = self.Name;
            if (!preloadingTextures && lazilyLoadedTextures.Contains(name)) {
                string currentMap = (Engine.Scene as Level)?.Session?.Area.GetSID();

                Logger.Log(LogLevel.Debug, "CollabUtils2/LazyLoadingHandler", name + " was lazily loaded by Everest! It will be associated to map " + currentMap + ".");
                newPaths.Add(name);

                if (currentMap != null) {
                    // add the texture to the lists associated to this map
                    if (!pathsPerMap.TryGetValue(currentMap, out HashSet<string> pathsForThisMap)) {
                        pathsForThisMap = new HashSet<string>();
                        pathsPerMap[currentMap] = pathsForThisMap;
                    }
                    pathsForThisMap.Add(name);

                    if (!texturesPerMap.TryGetValue(currentMap, out HashSet<VirtualTexture> texturesForThisMap)) {
                        texturesForThisMap = new HashSet<VirtualTexture>();
                        texturesPerMap[currentMap] = texturesForThisMap;
                    }
                    texturesForThisMap.Add(self);
                }
            }

            orig(self);
        }

        private static void saveNewLazilyLoadedPaths(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            writeNewPaths(session.Area.GetSID());
        }

        private static void writeNewPaths(string levelSID) {
            if (newPaths.Count > 0) {
                // write the new paths on disk, creating the file or appending to it.
                // we do that now because writing to disk while playing the map would only make the lazy load stuttering worse.
                string textureCachePath = Everest.Loader.PathCache + "/CollabUtils2/" + levelSID + ".texturecache.txt";
                Directory.CreateDirectory(textureCachePath.Substring(0, textureCachePath.LastIndexOf("/")));
                using (FileStream file = File.Open(textureCachePath, FileMode.OpenOrCreate)) {
                    Logger.Log(LogLevel.Warn, "CollabUtils2/LazyLoadingHandler", "Found " + newPaths.Count + " lazily loaded texture(s)! Saving them at " + textureCachePath + ".");

                    file.Seek(0, SeekOrigin.End);
                    using (var stream = new StreamWriter(file)) {
                        foreach (string path in newPaths) {
                            stream.WriteLine(path);
                        }
                    }
                }

                newPaths.Clear();
            }
        }
    }
}
