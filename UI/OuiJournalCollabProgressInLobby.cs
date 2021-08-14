using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Celeste.Mod.CollabUtils2.UI {
    class OuiJournalCollabProgressInLobby : OuiJournalPage {

        private Table table;

        // same as OuiJournalPage.IconCell but the icon comes from the Gui atlas instead of Journal
        private class IconCellFromGui : Cell {
            private string icon;

            private float width;
            private float height;

            public IconCellFromGui(string icon, float width, float height) {
                this.icon = icon;
                this.width = width;
                this.height = height;
            }

            public override float Width() {
                return width;
            }

            public override void Render(Vector2 center, float columnWidth) {
                GFX.Gui[icon].DrawCentered(center, Color.White, scale: Math.Min(width / GFX.Gui[icon].Width, height / GFX.Gui[icon].Height));
            }
        }

        private static Color getRankColor(CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo, long pb) {
            float pbSeconds = (float) TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold) {
                return Calc.HexToColor("B07A00");
            } else if (pbSeconds < speedBerryInfo.Silver) {
                return Color.Gray;
            }
            return Calc.HexToColor("B96F11");
        }

        private static string getRankIcon(CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo, long pb) {
            float pbSeconds = (float) TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold) {
                return "CollabUtils2/speed_berry_gold";
            } else if (pbSeconds < speedBerryInfo.Silver) {
                return "CollabUtils2/speed_berry_silver";
            }
            return "CollabUtils2/speed_berry_bronze";
        }

        public static List<OuiJournalCollabProgressInLobby> GeneratePages(OuiJournal journal, string levelSet, bool showOnlyDiscovered) {
            List<OuiJournalCollabProgressInLobby> pages = new List<OuiJournalCollabProgressInLobby>();
            int rowCount = 0;

            int totalStrawberries = 0;
            int totalDeaths = 0;
            int sumOfBestDeaths = 0;
            long totalTime = 0;
            long sumOfBestTimes = 0;

            bool allMapsDone = true;

            bool allLevelsDone = true;
            bool allSpeedBerriesDone = true;

            string heartTexture = MTN.Journal.Has("CollabUtils2Hearts/" + levelSet) ? "CollabUtils2Hearts/" + levelSet : "heartgem0";

            int mapsPerPage = 12;
            int mapAmount = SaveData.Instance.Areas_Safe.Where(item => !AreaData.Get(item.ID_Safe).Interlude_Safe
                && (!showOnlyDiscovered || item.TotalTimePlayed > 0)).Count();

            // we want to display the map icons if they're not actually all the same. ^^'
            bool displayIcons = AreaData.Areas
                .Where(area => !area.Interlude_Safe)
                .Select(area => area.Icon)
                .Distinct()
                .Count() > 1;

            OuiJournalCollabProgressInLobby currentPage = new OuiJournalCollabProgressInLobby(journal, levelSet, displayIcons);
            pages.Add(currentPage);

            if (mapAmount >= mapsPerPage) {
                // we want the last page to contain at least 2 maps.
                while (mapAmount % mapsPerPage < 2) {
                    mapsPerPage--;
                }
            }

            List<AreaStats> sortedMaps = new List<AreaStats>(SaveData.Instance.Areas_Safe)
                .Where(map => !AreaData.Get(map).Interlude_Safe)
                .ToList();

            Regex startsWithNumber = new Regex(".*/[0-9]+-.*");
            if (sortedMaps.Select(map => AreaData.Get(map).Icon ?? "").All(icon => startsWithNumber.IsMatch(icon))) {
                sortedMaps.Sort((a, b) => {
                    AreaData adata = AreaData.Get(a);
                    AreaData bdata = AreaData.Get(b);

                    bool aHeartSide = LobbyHelper.IsHeartSide(a.GetSID());
                    bool bHeartSide = LobbyHelper.IsHeartSide(b.GetSID());

                    if (aHeartSide && !bHeartSide)
                        return 1;
                    if (!aHeartSide && bHeartSide)
                        return -1;

                    return adata.Icon == bdata.Icon ? adata.Name.CompareTo(bdata.Name) : adata.Icon.CompareTo(bdata.Icon);
                });
            }

            foreach (AreaStats item in sortedMaps) {
                AreaData areaData = AreaData.Get(item.ID_Safe);
                if (LobbyHelper.IsHeartSide(areaData.GetSID())) {
                    if (allMapsDone || item.TotalTimePlayed > 0) {
                        // add a separator, like the one between regular maps and Farewell
                        currentPage.table.AddRow();
                    } else {
                        // all maps weren't complete yet, and the heart side was never accessed: hide the heart side for now.
                        continue;
                    }
                }

                if (showOnlyDiscovered && item.TotalTimePlayed <= 0) {
                    // skip the map, because it was not discovered yet.
                    // since it wasn't discovered, we can already say all maps weren't done though.
                    allMapsDone = false;
                    allLevelsDone = false;
                    allSpeedBerriesDone = false;
                    continue;
                }

                string strawberryText = null;
                if (areaData.Mode[0].TotalStrawberries > 0 || item.TotalStrawberries > 0) {
                    strawberryText = item.TotalStrawberries.ToString();
                    if (item.Modes[0].Completed) {
                        strawberryText = strawberryText + "/" + areaData.Mode[0].TotalStrawberries;
                    }
                } else {
                    strawberryText = "-";
                }

                Row row = currentPage.table.AddRow()
                    .Add(new TextCell(Dialog.Clean(areaData.Name), new Vector2(1f, 0.5f), 0.6f, currentPage.TextColor));

                if (displayIcons) {
                    row.Add(null).Add(new IconCellFromGui(GFX.Gui.Has(areaData.Icon) ? areaData.Icon : "areas/null", 60f, 50f));
                }

                row.Add(null)
                    .Add(new IconCell(item.Modes[0].HeartGem ? heartTexture : "dot"))
                    .Add(new TextCell(strawberryText, currentPage.TextJustify, 0.5f, currentPage.TextColor));

                if (item.TotalTimePlayed > 0) {
                    row.Add(new TextCell(Dialog.Deaths(item.Modes[0].Deaths), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                } else {
                    row.Add(new IconCell("dot"));
                }

                AreaStats stats = SaveData.Instance.GetAreaStatsFor(areaData.ToKey());
                if (CollabMapDataProcessor.SilverBerries.TryGetValue(areaData.GetLevelSet(), out Dictionary<string, EntityID> levelSetBerries)
                    && levelSetBerries.TryGetValue(areaData.GetSID(), out EntityID berryID)
                    && stats.Modes[0].Strawberries.Contains(berryID)) {

                    // silver berry was obtained!
                    row.Add(new IconCell("CollabUtils2/silver_strawberry"));
                } else if (stats.Modes[0].Strawberries.Any(berry => areaData.Mode[0].MapData.Goldenberries.Any(golden => golden.ID == berry.ID && golden.Level.Name == berry.Level))) {
                    // golden berry was obtained!
                    row.Add(new IconCell("CollabUtils2/golden_strawberry"));
                } else if (item.Modes[0].SingleRunCompleted) {
                    row.Add(new TextCell(Dialog.Deaths(item.Modes[0].BestDeaths), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                    sumOfBestDeaths += item.Modes[0].BestDeaths;
                } else {
                    // the player didn't ever do a single run.
                    row.Add(new IconCell("dot"));
                    allLevelsDone = false;
                }

                if (item.TotalTimePlayed > 0) {
                    row.Add(new TextCell(Dialog.Time(item.TotalTimePlayed), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                } else {
                    row.Add(new IconCell("dot"));
                }

                if (CollabModule.Instance.Settings.BestTimeToDisplayInJournal == CollabSettings.BestTimeInJournal.SpeedBerry) {
                    if (CollabMapDataProcessor.SpeedBerries.TryGetValue(item.GetSID(), out CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo)
                        && CollabModule.Instance.SaveData.SpeedBerryPBs.TryGetValue(item.GetSID(), out long speedBerryPB)) {

                        row.Add(new TextCell(Dialog.Time(speedBerryPB), currentPage.TextJustify, 0.5f, getRankColor(speedBerryInfo, speedBerryPB)));
                        row.Add(new IconCell(getRankIcon(speedBerryInfo, speedBerryPB)));
                        sumOfBestTimes += speedBerryPB;
                    } else {
                        row.Add(new IconCell("dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }
                } else {
                    if (item.Modes[0].BestTime > 0f) {
                        row.Add(new TextCell(Dialog.Time(item.Modes[0].BestTime), currentPage.TextJustify, 0.5f, currentPage.TextColor)).Add(null);
                        sumOfBestTimes += item.Modes[0].BestTime;
                    } else {
                        row.Add(new IconCell("dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }
                }

                totalStrawberries += item.TotalStrawberries;
                totalDeaths += item.Modes[0].Deaths;
                totalTime += item.TotalTimePlayed;

                if (!item.Modes[0].HeartGem) {
                    allMapsDone = false;
                }

                rowCount++;
                if (rowCount >= mapsPerPage) {
                    // split the next zones into another page.
                    rowCount = 0;
                    currentPage = new OuiJournalCollabProgressInLobby(journal, levelSet, displayIcons);
                    pages.Add(currentPage);
                }
            }

            if (currentPage.table.Rows > 1) {
                currentPage.table.AddRow();
                Row totalsRow = currentPage.table.AddRow()
                    .Add(new TextCell(Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, currentPage.TextColor)).Add(null);

                if (displayIcons) {
                    totalsRow.Add(null).Add(null);
                }

                totalsRow.Add(null)
                    .Add(new TextCell(totalStrawberries.ToString(), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Deaths(totalDeaths), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(allLevelsDone ? Dialog.Deaths(sumOfBestDeaths) : "-", currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Time(totalTime), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(allSpeedBerriesDone ? Dialog.Time(sumOfBestTimes) : "-", currentPage.TextJustify, 0.6f, currentPage.TextColor)).Add(null);

                for (int l = 1; l < SaveData.Instance.UnlockedModes; l++) {
                    totalsRow.Add(null);
                }
                totalsRow.Add(new TextCell(Dialog.Time(SaveData.Instance.Time), currentPage.TextJustify, 0.6f, currentPage.TextColor));
                currentPage.table.AddRow();
            }

            return pages;
        }

        public OuiJournalCollabProgressInLobby(OuiJournal journal, string levelSet, bool displayIcons)
            : base(journal) {

            string skullTexture = MTN.Journal.Has("CollabUtils2Skulls/" + levelSet) ? "CollabUtils2Skulls/" + levelSet : "skullblue";
            string minDeathsTexture = MTN.Journal.Has("CollabUtils2MinDeaths/" + levelSet) ? "CollabUtils2MinDeaths/" + levelSet : "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner";

            PageTexture = "page";
            table = new Table()
                .AddColumn(new TextCell(Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f, Color.Black * 0.7f, displayIcons ? 360f : 420f));

            if (displayIcons) {
                table
                    .AddColumn(new EmptyCell(0f))
                    .AddColumn(new EmptyCell(64f));
            }

            table.AddColumn(new EmptyCell(0f))
                .AddColumn(new EmptyCell(64f))
                .AddColumn(new IconCell("strawberry", 150f))
                .AddColumn(new IconCell(skullTexture, 100f))
                .AddColumn(new IconCell(minDeathsTexture, 100f))
                .AddColumn(new IconCell("time", 220f))
                .AddColumn(new IconCell("CollabUtils2/speed_berry_pbs_heading", 220f))
                .AddColumn(new EmptyCell(30f));
        }

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            Draw.SpriteBatch.Begin();
            table.Render(new Vector2(60f, 20f));
            Draw.SpriteBatch.End();
        }
    }
}
