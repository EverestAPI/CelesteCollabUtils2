using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.CollabUtils2.UI {
    class OuiJournalCollabProgressInOverworld : OuiJournalPage {

        private Table table;

        private static int getRankLevel(CollabMapDataProcessor.SpeedBerryInfo speedBerryInfo, long pb) {
            float pbSeconds = (float) TimeSpan.FromTicks(pb).TotalSeconds;
            if (pbSeconds < speedBerryInfo.Gold) {
                return 1;
            } else if (pbSeconds < speedBerryInfo.Silver) {
                return 2;
            }
            return 3;
        }

        private static Color getRankColor(int level) {
            if (level == 1) {
                return Calc.HexToColor("B07A00");
            } else if (level == 2) {
                return Color.Gray;
            }
            return Calc.HexToColor("B96F11");
        }

        private static string getRankIcon(int level) {
            if (level == 1) {
                return "CollabUtils2/speed_berry_gold";
            } else if (level == 2) {
                return "CollabUtils2/speed_berry_silver";
            }
            return "CollabUtils2/speed_berry_bronze";
        }

        public OuiJournalCollabProgressInOverworld(OuiJournal journal)
            : base(journal) {

            PageTexture = "page";
            table = new Table()
                .AddColumn(new TextCell(Dialog.Clean("journal_progress"), new Vector2(0f, 0.5f), 1f, Color.Black * 0.7f, 420f))
                .AddColumn(new EmptyCell(20f))
                .AddColumn(new EmptyCell(64f))
                .AddColumn(new IconCell("strawberry", 150f))
                .AddColumn(new IconCell("skullblue", 100f))
                .AddColumn(new IconCell("CollabUtils2MinDeaths/SpringCollab2020/1-Beginner", 100f))
                .AddColumn(new IconCell("time", 220f))
                .AddColumn(new IconCell("CollabUtils2/speed_berry_pbs_heading", 220f))
                .AddColumn(new EmptyCell(30f));

            int totalStrawberries = 0;
            int totalDeaths = 0;
            int sumOfBestDeaths = 0;
            long totalTime = 0;
            long sumOfBestTimes = 0;

            bool allLevelsDone = true;
            bool allSpeedBerriesDone = true;
            bool allMapsCompletedInSingleRun = true;

            foreach (AreaStats item in SaveData.Instance.Areas_Safe) {
                AreaData areaData = AreaData.Get(item.ID_Safe);
                if (areaData.GetLevelSet() == SaveData.Instance.LevelSet) {
                    string lobbyMapLevelSetName = LobbyHelper.GetLobbyLevelSet(areaData.GetSID());
                    LevelSetStats lobbyMapLevelSet = null;
                    if (lobbyMapLevelSetName != null) {
                        lobbyMapLevelSet = SaveData.Instance.GetLevelSetStatsFor(lobbyMapLevelSetName);
                    }

                    int lobbyStrawberries = item.TotalStrawberries;
                    int lobbyTotalStrawberries = areaData.Mode[0].TotalStrawberries;
                    int lobbyDeaths = item.Modes[0].Deaths;
                    int lobbySumOfBestDeaths = 0;
                    long lobbyTotalTime = item.TotalTimePlayed;
                    long lobbySumOfBestTimes = 0;
                    bool lobbyLevelsDone = true;
                    int lobbySpeedBerryLevel = 1;
                    bool lobbySilverBerriesObtained = true;
                    bool lobbyAllMapsCompletedInSingleRun = true;

                    foreach (AreaStats lobbyMap in lobbyMapLevelSet?.Areas ?? new List<AreaStats>()) {
                        AreaData lobbyAreaData = AreaData.Get(lobbyMap.ID_Safe);
                        lobbyStrawberries += lobbyMap.TotalStrawberries;
                        lobbyTotalStrawberries += lobbyAreaData.Mode[0].TotalStrawberries;
                        lobbyDeaths += lobbyMap.Modes[0].Deaths;
                        lobbyTotalTime += lobbyMap.TotalTimePlayed;
                        lobbyAllMapsCompletedInSingleRun &= lobbyMap.Modes[0].SingleRunCompleted;

                        if (CollabModule.Instance.Settings.BestTimeToDisplayInJournal == CollabSettings.BestTimeInJournal.SpeedBerry) {
                            if (CollabMapDataProcessor.SpeedBerries.TryGetValue(lobbyMap.GetSID(), out CollabMapDataProcessor.SpeedBerryInfo mapSpeedBerryInfo)
                                && CollabModule.Instance.SaveData.SpeedBerryPBs.TryGetValue(lobbyMap.GetSID(), out long mapSpeedBerryPB)) {

                                lobbySpeedBerryLevel = Math.Max(getRankLevel(mapSpeedBerryInfo, mapSpeedBerryPB), lobbySpeedBerryLevel);
                                lobbySumOfBestTimes += mapSpeedBerryPB;
                            } else {
                                lobbySpeedBerryLevel = 4;
                            }
                        } else {
                            if (lobbyMap.Modes[0].BestTime > 0f) {
                                lobbySumOfBestTimes += lobbyMap.Modes[0].BestTime;
                            } else {
                                lobbySpeedBerryLevel = 4;
                            }
                        }

                        bool goldenBerryNotObtained = !lobbyMap.Modes[0].Strawberries.Any(berry => areaData.Mode[0].MapData.Goldenberries.Any(golden => golden.ID == berry.ID && golden.Level.Name == berry.Level));
                        bool silverBerryNotObtained = !CollabMapDataProcessor.SilverBerries.TryGetValue(lobbyMap.GetLevelSet(), out Dictionary<string, EntityID> levelSetBerries)
                            || !levelSetBerries.TryGetValue(lobbyMap.GetSID(), out EntityID berryID)
                            || !lobbyMap.Modes[0].Strawberries.Contains(berryID);

                        if (goldenBerryNotObtained && silverBerryNotObtained) {
                            lobbySilverBerriesObtained = false;
                            lobbySumOfBestDeaths += lobbyMap.Modes[0].BestDeaths;
                        }

                        if (!lobbyMap.Modes[0].HeartGem) {
                            lobbyLevelsDone = false;
                        }
                    }

                    string strawberryText = null;
                    if (lobbyStrawberries > 0 || lobbyTotalStrawberries > 0) {
                        strawberryText = lobbyStrawberries.ToString();
                        if (lobbyLevelsDone) {
                            strawberryText = strawberryText + "/" + lobbyTotalStrawberries;
                        }
                    } else {
                        strawberryText = "-";
                    }

                    string heartTexture = MTN.Journal.Has("CollabUtils2Hearts/" + lobbyMapLevelSetName) ? "CollabUtils2Hearts/" + lobbyMapLevelSetName : "heartgem0";

                    string areaName = Dialog.Clean(areaData.Name);
                    if (Dialog.Has(areaData.Name + "_journal")) {
                        areaName = Dialog.Clean(areaData.Name + "_journal");
                    }

                    Row row = table.AddRow()
                        .Add(new TextCell(areaName, new Vector2(1f, 0.5f), 0.6f, TextColor))
                        .Add(null)
                        .Add(new IconCell(item.Modes[0].HeartGem ? heartTexture : "dot"))
                        .Add(new TextCell(strawberryText, TextJustify, 0.5f, TextColor));

                    if (lobbyTotalTime > 0) {
                        row.Add(new TextCell(Dialog.Deaths(lobbyDeaths), TextJustify, 0.5f, TextColor));
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    if (lobbyLevelsDone) {
                        AreaStats stats = SaveData.Instance.GetAreaStatsFor(areaData.ToKey());
                        if (lobbyMapLevelSet == null) {
                            row.Add(new TextCell(Dialog.Deaths(item.Modes[0].BestDeaths), TextJustify, 0.5f, TextColor));
                            sumOfBestDeaths += item.Modes[0].BestDeaths;
                        } else if (lobbySilverBerriesObtained) {
                            row.Add(new IconCell("CollabUtils2/golden_strawberry"));
                        } else if (lobbyAllMapsCompletedInSingleRun) {
                            row.Add(new TextCell(Dialog.Deaths(lobbySumOfBestDeaths), TextJustify, 0.5f, TextColor));
                        } else {
                            row.Add(new IconCell("dot"));
                        }
                    } else {
                        row.Add(new IconCell("dot"));
                        allLevelsDone = false;
                    }

                    if (lobbyTotalTime > 0) {
                        row.Add(new TextCell(Dialog.Time(lobbyTotalTime), TextJustify, 0.5f, TextColor));
                    } else {
                        row.Add(new IconCell("dot"));
                    }

                    if (lobbyMapLevelSet == null) {
                        row.Add(new TextCell("-", TextJustify, 0.5f, TextColor)).Add(null);
                    } else if (lobbySpeedBerryLevel < 4) {
                        if (CollabModule.Instance.Settings.BestTimeToDisplayInJournal == CollabSettings.BestTimeInJournal.SpeedBerry) {
                            row.Add(new TextCell(Dialog.Time(lobbySumOfBestTimes), TextJustify, 0.5f, getRankColor(lobbySpeedBerryLevel)));
                            row.Add(new IconCell(getRankIcon(lobbySpeedBerryLevel)));
                            sumOfBestTimes += lobbySumOfBestTimes;
                        } else {
                            row.Add(new TextCell(Dialog.Time(lobbySumOfBestTimes), TextJustify, 0.5f, TextColor)).Add(null);
                            sumOfBestTimes += lobbySumOfBestTimes;
                        }
                    } else {
                        row.Add(new IconCell("dot")).Add(null);
                        allSpeedBerriesDone = false;
                    }

                    totalStrawberries += lobbyStrawberries;
                    totalDeaths += lobbyDeaths;
                    sumOfBestDeaths += lobbySumOfBestDeaths;
                    totalTime += lobbyTotalTime;
                    allMapsCompletedInSingleRun &= lobbyAllMapsCompletedInSingleRun;

                    if (!lobbyLevelsDone) {
                        allLevelsDone = false;
                    }
                }
            }

            table.AddRow();
            Row totalsRow = table.AddRow()
                .Add(new TextCell(Dialog.Clean("journal_totals"), new Vector2(1f, 0.5f), 0.7f, TextColor)).Add(null)
                .Add(null)
                .Add(new TextCell(totalStrawberries.ToString(), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(Dialog.Deaths(totalDeaths), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(allLevelsDone && allMapsCompletedInSingleRun ? Dialog.Deaths(sumOfBestDeaths) : "-", TextJustify, 0.6f, TextColor))
                .Add(new TextCell(Dialog.Time(totalTime), TextJustify, 0.6f, TextColor))
                .Add(new TextCell(allSpeedBerriesDone ? Dialog.Time(sumOfBestTimes) : "-", TextJustify, 0.6f, TextColor)).Add(null);

            for (int l = 1; l < SaveData.Instance.UnlockedModes; l++) {
                totalsRow.Add(null);
            }
            totalsRow.Add(new TextCell(Dialog.Time(SaveData.Instance.Time), TextJustify, 0.6f, TextColor));
            table.AddRow();
        }

        public override void Redraw(VirtualRenderTarget buffer) {
            base.Redraw(buffer);
            Draw.SpriteBatch.Begin();
            table.Render(new Vector2(60f, 20f));
            Draw.SpriteBatch.End();
        }
    }
}
