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
        public List<VisitedPoint> VisitedPoints { get; }

        private VisitedPoint lastVisitedPoint = new VisitedPoint(Vector2.Zero);

        public const int EXPLORATION_RADIUS = 20;
        
        public string SID { get; }
        public string Room { get; }

        public string Key => string.IsNullOrWhiteSpace(Room) ? SID : $"{SID}.{Room}";
        
        public LobbyVisitManager(string sid, string room = null) {
            SID = sid;
            Room = room;
            VisitedPoints = CollabModule.Instance.SaveData.VisitedLobbyPositions.TryGetValue(Key, out var value) ? FromBase64(value) : new List<VisitedPoint>();
        }

        public void Reset() {
            VisitedPoints.Clear();
            lastVisitedPoint = new VisitedPoint(Vector2.Zero);
        }
        
        public void Save() {
            CollabModule.Instance.SaveData.VisitedLobbyPositions[Key] = ToBase64(VisitedPoints);
        }
        
        private static List<VisitedPoint> FromBase64(string str) {
            const int size = sizeof(short);

            var bytes = Convert.FromBase64String(str);
            if (bytes.Length % size * 2 != 0) return new List<VisitedPoint>();

            var list = new List<VisitedPoint>();
            for (int offset = 0; offset < bytes.Length; offset += size * 2) {
                var x = BitConverter.ToInt16(bytes, offset);
                var y = BitConverter.ToInt16(bytes, offset + size);
                list.Add(new VisitedPoint(new Vector2(x, y)));
            }

            return list;
        }

        private static string ToBase64(List<VisitedPoint> list) {
            const int size = sizeof(short);

            var bytes = new byte[list.Count * size * 2];
            int offset = 0;

            for (int i = 0; i < list.Count; i++) {
                var v = list[i];
                var b = BitConverter.GetBytes((short) v.Point.X);
                bytes[offset++] = b[0];
                bytes[offset++] = b[1];
                b = BitConverter.GetBytes((short) v.Point.Y);
                bytes[offset++] = b[0];
                bytes[offset++] = b[1];
            }

            return Convert.ToBase64String(bytes);
        }

        public void VisitPoint(Vector2 point, bool shouldSave = true) {
            const int nearby_point_count = 50;

            float generate_distance = EXPLORATION_RADIUS / 2f;
            float sort_threshold = EXPLORATION_RADIUS;
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

                // update last visited to closest of the first 20
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
