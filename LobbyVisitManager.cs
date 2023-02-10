using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    public class LobbyVisitManager {
        /// <summary>
        /// Each of the points the player has visited and thus generated circular snapshots.
        /// These points are in 8x8 pixel tile offsets from the top left of the map.
        /// </summary>
        public List<VisitedPoint> VisitedPoints { get; } = new List<VisitedPoint>();

        public List<string> ActivatedWarps { get; } = new List<string>();

        public bool VisitedAll { get; private set; }

        private VisitedPoint lastVisitedPoint = new VisitedPoint(Vector2.Zero);

        public const int EXPLORATION_RADIUS = 20;
        private const ushort currentVersion = 1;

        public string SID { get; }
        public string Room { get; }

        private static string GetKey(string sid, string room = null) =>
            string.IsNullOrWhiteSpace(room) ? sid : $"{sid}.{room}";

        public string Key => GetKey(SID, Room);

        public bool MatchesKey(string sid, string room = null) => Key == GetKey(sid, room);

        public LobbyVisitManager(string sid, string room = null) {
            SID = sid;
            Room = room;
            Load();
        }

        public void VisitAll(bool shouldSave = true) {
            VisitedPoints.Clear();
            VisitedAll = true;
            if (shouldSave) {
                Save();
            }
        }

        public void Reset(bool shouldSave = true) {
            VisitedPoints.Clear();
            ActivatedWarps.Clear();
            VisitedAll = false;

            lastVisitedPoint = new VisitedPoint(Vector2.Zero);

            if (shouldSave) {
                Save();
            }
        }

        public void Save() {
            try {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream)) {
                    // write version number
                    writer.Write(currentVersion);

                    // write whether we've visited everything
                    writer.Write(VisitedAll);

                    // write points if we haven't visited them all
                    if (!VisitedAll) {
                        writer.Write((uint) VisitedPoints.Count);
                        foreach (var v in VisitedPoints) {
                            writer.Write((short) v.Point.X);
                            writer.Write((short) v.Point.Y);
                        }
                    }

                    // write activated warps
                    writer.Write((uint) ActivatedWarps.Count);
                    foreach (var w in ActivatedWarps) {
                        writer.Write(w);
                    }

                    // store it
                    CollabModule.Instance.SaveData.VisitedLobbyPositions[Key] = Convert.ToBase64String(stream.ToArray());
                }
            } catch (Exception) {
                Logger.Log(LogLevel.Error, "CollabUtils2/LobbyVisitManager", "Save: Error trying to serialise visited points.");
            }
        }

        private void Load() {
            VisitedPoints.Clear();
            ActivatedWarps.Clear();
            VisitedAll = false;

            if (!CollabModule.Instance.SaveData.VisitedLobbyPositions.TryGetValue(Key, out var value)) {
                return;
            }

            try {
                var visitedAll = false;
                var visitedPoints = new List<VisitedPoint>();
                var activatedWarps = new List<string>();
                var bytes = Convert.FromBase64String(value);

                using (var stream = new MemoryStream(bytes))
                using (var reader = new BinaryReader(stream)) {
                    // check a version number so we can clear out bad data later
                    var version = reader.ReadUInt16();
                    if (version != currentVersion) {
                        Logger.Log(LogLevel.Warn, "CollabUtils2/LobbyVisitManager", "Load: Wrong version found, clearing stored data instead.");
                        CollabModule.Instance.SaveData.VisitedLobbyPositions.Remove(Key);
                        return;
                    }

                    // read whether we've visited everything
                    visitedAll = reader.ReadBoolean();

                    // if we haven't visited everything, read all the points
                    if (!visitedAll) {
                        var visitedCount = reader.ReadUInt32();
                        for (int i = 0; i < visitedCount; i++) {
                            var x = reader.ReadInt16();
                            var y = reader.ReadInt16();
                            visitedPoints.Add(new VisitedPoint(new Vector2(x, y)));
                        }
                    }

                    // read all the activated warps
                    var activatedCount = reader.ReadUInt32();
                    for (int i = 0; i < activatedCount; i++) {
                        var warpId = reader.ReadString();
                        activatedWarps.Add(warpId);
                    }
                }

                VisitedAll = visitedAll;
                VisitedPoints.AddRange(visitedPoints);
                ActivatedWarps.AddRange(activatedWarps);
            } catch (Exception) {
                Logger.Log(LogLevel.Error, "CollabUtils2/LobbyVisitManager", "Load: Error trying to deserialise visited points, clearing stored data instead.");
                CollabModule.Instance.SaveData.VisitedLobbyPositions.Remove(Key);
            }
        }

        public void ActivateWarp(string id, bool shouldSave = true) {
            if (ActivatedWarps.Contains(id)) return;

            ActivatedWarps.Add(id);

            if (shouldSave) {
                Save();
            }
        }

        public void VisitPoint(Vector2 point, bool shouldSave = true) {
            const int nearby_point_count = 50;
            const float generate_distance = EXPLORATION_RADIUS / 2f;
            const float sort_threshold = EXPLORATION_RADIUS;

            // don't need to do anything if we've visited everywhere
            if (VisitedAll) return;

            var lenSq = lastVisitedPoint == null ? float.MaxValue : (point - lastVisitedPoint.Point).LengthSquared();
            var shouldGenerate = !VisitedPoints.Any();
            if (!shouldGenerate && lenSq > generate_distance * generate_distance) {
                // if the distance has gone past the sort threshold, recalculate and sort the list
                if (lenSq > sort_threshold * sort_threshold) {
                    foreach (var vp in VisitedPoints) {
                        vp.DistanceSquared = (vp.Point - point).LengthSquared();
                    }

                    VisitedPoints.Sort((a, b) => Math.Sign(b.DistanceSquared - a.DistanceSquared));
                }

                // update last visited to closest of the first 50
                lastVisitedPoint = VisitedPoints.Take(nearby_point_count).FirstOrDefault(v => (v.Point - point).LengthSquared() < generate_distance * generate_distance);
                // generate if it still passes the threshold
                shouldGenerate = lastVisitedPoint == null;
            }

            if (shouldGenerate) {
                VisitedPoints.Add(lastVisitedPoint = new VisitedPoint(point, 0f));
                if (shouldSave) {
                    Save();
                }
            }
        }

        public class VisitedPoint {
            public Vector2 Point;
            public float DistanceSquared;

            public VisitedPoint(Vector2 point, float distanceSquared = float.MaxValue) {
                Point = point;
                DistanceSquared = distanceSquared;
            }
        }
    }
}
