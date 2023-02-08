using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2 {
    public class LobbyVisitManager {
        /// <summary>
        /// Each of the points the player has visited and thus generated circular snapshots.
        /// These points are in 8x8 pixel tile offsets from the top left of the map.
        /// </summary>
        public List<VisitedPoint> VisitedPoints { get; } = new List<VisitedPoint>();

        public bool VisitedAll { get; private set; }

        private VisitedPoint lastVisitedPoint = new VisitedPoint(Vector2.Zero);

        public const int EXPLORATION_RADIUS = 20;

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
            lastVisitedPoint = new VisitedPoint(Vector2.Zero);
            VisitedAll = false;
            if (shouldSave) {
                Save();
            }
        }

        public void Save() {
            byte[] bytes;
            // if we've visited everything, save a single 0xFF byte
            if (VisitedAll) {
                bytes = new[] { byte.MaxValue };
            } else {
                const int size = sizeof(short);
                bytes = new byte[VisitedPoints.Count * size * 2];
                int offset = 0;

                for (int i = 0; i < VisitedPoints.Count; i++) {
                    var v = VisitedPoints[i];
                    var b = BitConverter.GetBytes((short) v.Point.X);
                    bytes[offset++] = b[0];
                    bytes[offset++] = b[1];
                    b = BitConverter.GetBytes((short) v.Point.Y);
                    bytes[offset++] = b[0];
                    bytes[offset++] = b[1];
                }
            }

            CollabModule.Instance.SaveData.VisitedLobbyPositions[Key] = Convert.ToBase64String(bytes);
        }

        private void Load() {
            VisitedPoints.Clear();
            VisitedAll = false;

            if (!CollabModule.Instance.SaveData.VisitedLobbyPositions.TryGetValue(Key, out var value)) {
                return;
            }

            const int size = sizeof(short);
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length == 1 && bytes[0] == byte.MaxValue) {
                VisitedAll = true;
            } else if (bytes.Length % size * 2 == 0) {
                for (int offset = 0; offset < bytes.Length; offset += size * 2) {
                    var x = BitConverter.ToInt16(bytes, offset);
                    var y = BitConverter.ToInt16(bytes, offset + size);
                    VisitedPoints.Add(new VisitedPoint(new Vector2(x, y)));
                }
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
