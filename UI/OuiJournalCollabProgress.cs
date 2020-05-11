using Microsoft.Xna.Framework;
using Monocle;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.UI {
    class OuiJournalCollabProgress : OuiJournalPage {

        private Table table;

        public static List<OuiJournalCollabProgress> GeneratePages(OuiJournal journal, string levelSet) {
            List<OuiJournalCollabProgress> pages = new List<OuiJournalCollabProgress>();
            int rowCount = 0;
            OuiJournalCollabProgress currentPage = new OuiJournalCollabProgress(journal, levelSet);
            pages.Add(currentPage);

            int totalStrawberries = 0;
            int totalDeaths = 0;
            int sumOfBestDeaths = 0;
            long totalTime = 0;
            long sumOfBestTimes = 0;

            string heartTexture = MTN.Journal.Has("CollabUtils2Hearts/" + levelSet) ? "CollabUtils2Hearts/" + levelSet : "heartgem0";

            foreach (AreaStats item in SaveData.Instance.Areas_Safe) {
                AreaData areaData = AreaData.Get(item.ID_Safe);
                if (!areaData.Interlude_Safe) {
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
                        .Add(new TextCell(Dialog.Clean(areaData.Name), new Vector2(1f, 0.5f), 0.6f, currentPage.TextColor))
                        .Add(null)
                        .Add(new IconCell(item.Modes[0].HeartGem ? heartTexture : "dot"))
                        .Add(new TextCell(strawberryText, currentPage.TextJustify, 0.5f, currentPage.TextColor));

                    if (item.TotalTimePlayed > 0) {
                        row.Add(new TextCell(Dialog.Deaths(item.Modes[0].Deaths), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    if (item.BestTotalTime > 0) {
                        AreaStats stats = SaveData.Instance.GetAreaStatsFor(areaData.ToKey());
                        if (CollabMapDataProcessor.SilverBerries.TryGetValue(areaData.GetLevelSet(), out Dictionary<string, EntityID> levelSetBerries)
                            && levelSetBerries.TryGetValue(areaData.GetSID(), out EntityID berryID)
                            && stats.Modes[0].Strawberries.Contains(berryID)) {

                            // silver berry was obtained!
                            row.Add(new IconCell("CollabUtils2/silver_strawberry"));
                        } else {
                            row.Add(new TextCell(Dialog.Deaths(item.Modes[0].BestDeaths), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                        }
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    if (item.TotalTimePlayed > 0) {
                        row.Add(new TextCell(Dialog.Time(item.TotalTimePlayed), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    if (item.BestTotalTime > 0) {
                        row.Add(new TextCell(Dialog.Time(item.BestTotalTime), currentPage.TextJustify, 0.5f, currentPage.TextColor));
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    totalStrawberries += item.TotalStrawberries;
                    totalDeaths += item.Modes[0].Deaths;
                    sumOfBestDeaths += item.Modes[0].BestDeaths;
                    totalTime += item.TotalTimePlayed;
                    sumOfBestTimes += item.BestTotalTime;

                    rowCount++;
                    if (rowCount > 11) {
                        // split the next zones into another page.
                        rowCount = 0;
                        currentPage = new OuiJournalCollabProgress(journal, levelSet);
                        pages.Add(currentPage);
                    }
                }
            }

            if (currentPage.table.Rows > 1) {
                currentPage.table.AddRow();
                Row totalsRow = currentPage.table.AddRow()
                    .Add(new TextCell(Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, currentPage.TextColor)).Add(null)
                    .Add(null)
                    .Add(new TextCell(totalStrawberries.ToString(), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Deaths(totalDeaths), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Deaths(sumOfBestDeaths), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Time(totalTime), currentPage.TextJustify, 0.6f, currentPage.TextColor))
                    .Add(new TextCell(Dialog.Time(sumOfBestTimes), currentPage.TextJustify, 0.6f, currentPage.TextColor));

                for (int l = 1; l < SaveData.Instance.UnlockedModes; l++) {
                    totalsRow.Add(null);
                }
                totalsRow.Add(new TextCell(Dialog.Time(SaveData.Instance.Time), currentPage.TextJustify, 0.6f, currentPage.TextColor));
                currentPage.table.AddRow();
            }

            return pages;
        }

        public OuiJournalCollabProgress(OuiJournal journal, string levelSet)
            : base(journal) {

            string skullTexture = MTN.Journal.Has("CollabUtils2Skulls/" + levelSet) ? "CollabUtils2Skulls/" + levelSet : "skullblue";
            string minDeathsTexture = MTN.Journal.Has("CollabUtils2MinDeaths/" + levelSet) ? "CollabUtils2MinDeaths/" + levelSet : "CollabUtils2MinDeaths/SpringCollab2020/1-Beginner";

            PageTexture = "page";
            table = new Table()
                .AddColumn(new TextCell(Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f, Color.Black * 0.7f, 450f))
                .AddColumn(new EmptyCell(20f))
                .AddColumn(new EmptyCell(64f))
                .AddColumn(new IconCell("strawberry", 150f))
                .AddColumn(new IconCell(skullTexture, 100f))
                .AddColumn(new IconCell(minDeathsTexture, 100f))
                .AddColumn(new IconCell("time", 220f))
                .AddColumn(new IconCell("time", 220f));
        }

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            Draw.SpriteBatch.Begin();
            table.Render(new Vector2(60f, 20f));
            Draw.SpriteBatch.End();
        }
    }
}
