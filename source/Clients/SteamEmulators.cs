﻿using CommonPluginsShared;
using CommonPluginsShared.Extensions;
using CommonPluginsShared.Models;
using CommonPluginsStores;
using CommonPluginsStores.Steam;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using SuccessStory.Converters;
using SuccessStory.Models;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Playnite.SDK;
using System.Collections.ObjectModel;
using CommonPluginsStores.Models;
using CommonPluginsStores.Steam.Models.SteamKit;
using System.Dynamic;
namespace SuccessStory.Clients
{
    class SteamEmulators : GenericAchievements
    {
        protected static SteamApi _steamApi;
        internal static SteamApi steamApi
        {
            get
            {
                if (_steamApi == null)
                {
                    _steamApi = new SteamApi(PluginDatabase.PluginName, CommonPluginsShared.PlayniteTools.ExternalPlugin.SuccessStory);
                }
                return _steamApi;
            }

            set => _steamApi = value;
        }

        private List<string> AchievementsDirectories { get; set; } = new List<string>();
        //private int SteamId { get; set; } = 0;
        private uint AppId { get; set; } = 0;



        private string Hyphenate(string str, int pos) => string.Join("-", Regex.Split(str, @"(?<=\G.{" + pos + "})(?!$)"));


        public SteamEmulators(List<Folder> LocalFolders) : base("SteamEmulators")
        {
            AchievementsDirectories.Add("%PUBLIC%\\Documents\\Steam\\CODEX");
            AchievementsDirectories.Add("%appdata%\\Steam\\CODEX");

            AchievementsDirectories.Add("%PUBLIC%\\Documents\\Steam\\RUNE"); //eFMann    
            AchievementsDirectories.Add("%appdata%\\Steam\\RUNE");           //eFMann

            AchievementsDirectories.Add("%PUBLIC%\\Documents\\EMPRESS"); //eFMann    
            AchievementsDirectories.Add("%appdata%\\EMPRESS");           //eFMann

            AchievementsDirectories.Add("%PUBLIC%\\Documents\\OnlineFix"); //eFMann 

            AchievementsDirectories.Add("%DOCUMENTS%\\VALVE");

            AchievementsDirectories.Add("%appdata%\\Goldberg SteamEmu Saves");
            AchievementsDirectories.Add("%appdata%\\GSE Saves"); //eFMann

            AchievementsDirectories.Add("%appdata%\\SmartSteamEmu");
            AchievementsDirectories.Add("%DOCUMENTS%\\DARKSiDERS");

            AchievementsDirectories.Add("%ProgramData%\\Steam");
            AchievementsDirectories.Add("%localappdata%\\SKIDROW");
            AchievementsDirectories.Add("%DOCUMENTS%\\SKIDROW");

            foreach (Folder folder in LocalFolders)
            {
                AchievementsDirectories.Add(folder.FolderPath);
            }
        }


        public override GameAchievements GetAchievements(Game game)
        {
            throw new NotImplementedException();
        }


        #region Configuration
        public override bool ValidateConfiguration()
        {
            // Just check for API key existence
            return !string.IsNullOrEmpty(steamApi?.CurrentAccountInfos?.ApiKey);
        }

        public override bool EnabledInSettings()
        {
            // No necessary activation
            return true;
        }
        #endregion


        //public int GetSteamId()
        //{
        //return SteamId;
        //}

        public uint GetAppId()
        {
            return AppId;
        }


        #region SteamEmulator
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public GameAchievements GetAchievementsLocal(Game game, string apiKey, uint AppIdd = 0, bool IsManual = false)
        {
            GameAchievements gameAchievements = SuccessStory.PluginDatabase.GetDefault(game);
            GameAchievements gameAchievementsCached = SuccessStory.PluginDatabase.Get(game, true);

            // Check for forced AppId first
            if (SuccessStory.PluginDatabase.PluginSettings.Settings.ForcedSteamAppIds != null &&
                SuccessStory.PluginDatabase.PluginSettings.Settings.ForcedSteamAppIds.TryGetValue(game.Id, out int forcedAppId))
            {
                this.AppId = (uint)forcedAppId;
            }
            else
            {
                this.AppId = AppId != 0 ? AppId : steamApi.GetAppId(game.GameId);
            }

            SteamEmulatorData data = Get(game, this.AppId, apiKey, IsManual);

            if (gameAchievementsCached == null)
            {
                gameAchievements.Items = data.Achievements;
                gameAchievements.ItemsStats = data.Stats;
                gameAchievements.SetRaretyIndicator();
                return gameAchievements;
            }
            else
            {
                if (gameAchievementsCached.Items.Count != data.Achievements.Count)
                {
                    gameAchievements.Items = data.Achievements;
                    gameAchievements.ItemsStats = data.Stats;
                    gameAchievements.SetRaretyIndicator();
                    return gameAchievements;
                }
                gameAchievementsCached.Items.ForEach(x =>
                {
                    Achievement finded = data.Achievements.Find(y => x.ApiName == y.ApiName);
                    if (finded != null)
                    {
                        x.Name = finded.Name;
                        if (x.DateUnlocked == null || x.DateUnlocked == default(DateTime))
                        {
                            x.DateUnlocked = finded.DateUnlocked;
                        }
                    }
                });
                gameAchievementsCached.ItemsStats = data.Stats;
                gameAchievementsCached.SetRaretyIndicator();
                return gameAchievementsCached;
            }
        }



        private List<GameStats> ReadStatsINI(string pathFile, List<GameStats> gameStats)
        {
            try
            {
                string line;
                string Name = string.Empty;
                double Value = 0;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    // Achievement name
                    if (!line.IsEqual("[Stats]"))
                    {
                        var data = line.Split('=');
                        if (data.Count() > 1 && !data[0].IsNullOrEmpty() && !data[0].IsEqual("STACount"))
                        {
                            Name = data[0];
                            try
                            {
                                Value = BitConverter.ToInt32(StringToByteArray(data[1]), 0);
                            }
                            catch
                            {
                                double.TryParse(data[1], out Value);
                            }

                            gameStats.Add(new GameStats
                            {
                                Name = Name,
                                Value = Value
                            });

                            Name = string.Empty;
                            Value = 0;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return gameStats;
        }

        private List<Achievement> ReadAchievementsINI(string pathFile, List<Achievement> ReturnAchievements)
        {
            bool isType2 = false;
            bool isType3 = false;

            try
            {
                string line;
                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.IsEqual("[Time]"))
                    {
                        isType2 = true;
                        break;
                    }
                    if (line.IsEqual("achieved=true"))
                    {
                        isType3 = true;
                        break;
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            if (!isType2 && !isType3)
            {
                return ReadAchievementsINI_type1(pathFile, ReturnAchievements);
            }
            else if (isType3)
            {
                return ReadAchievementsINI_type3(pathFile, ReturnAchievements);
            }
            else
            {
                return ReadAchievementsINI_type2(pathFile, ReturnAchievements);
            }
        }

        private List<Achievement> ReadAchievementsINI_type1(string pathFile, List<Achievement> ReturnAchievements)
        {
            try
            {
                string line;

                string Name = string.Empty;
                bool State = false;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    // Achievement name
                    if (line.IndexOf("[") > -1)
                    {
                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                        State = false;
                        timeUnlock = 0;
                        DateUnlocked = null;
                    }

                    if (Name != "Steam")
                    {
                        // State
                        if (line.IndexOf("State") > -1 && line.ToLower() != "state = 0000000000")
                        {
                            State = true;
                        }

                        // Unlock
                        if (line.IndexOf("Time") > -1 && line.ToLower() != "time = 0000000000")
                        {
                            if (line.Contains("Time = "))
                            {
                                sTimeUnlock = line.Replace("Time = ", string.Empty);
                                timeUnlock = BitConverter.ToInt32(StringToByteArray(line.Replace("Time = ", string.Empty)), 0);
                            }
                            if (line.Contains("Time="))
                            {
                                sTimeUnlock = line.Replace("Time=", string.Empty);
                                sTimeUnlock = sTimeUnlock.Substring(0, sTimeUnlock.Length - 2);

                                char[] ca = sTimeUnlock.ToCharArray();
                                StringBuilder sb = new StringBuilder(sTimeUnlock.Length);
                                for (int i = 0; i < sTimeUnlock.Length; i += 2)
                                {
                                    sb.Insert(0, ca, i, 2);
                                }
                                sTimeUnlock = sb.ToString();

                                timeUnlock = int.Parse(sTimeUnlock, System.Globalization.NumberStyles.HexNumber);
                            }
                        }
                        if (line.IndexOf("CurProgress") > -1 && line.ToLower() != "curprogress = 0000000000")
                        {
                            sTimeUnlock = line.Replace("CurProgress = ", string.Empty);
                            timeUnlock = BitConverter.ToInt32(StringToByteArray(line.Replace("CurProgress = ", string.Empty)), 0);
                        }

                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                        // End Achievement
                        if (timeUnlock != 0 && State)
                        {
                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked,
                                NoRarety = false  // Add this line
                            });
                            Name = string.Empty;
                            State = false;
                            timeUnlock = 0;
                            DateUnlocked = null;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return ReturnAchievements;
        }

        private List<Achievement> ReadAchievementsINI_type3(string pathFile, List<Achievement> ReturnAchievements)
        {
            try
            {
                string line;

                string Name = string.Empty;
                bool State = false;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    // Achievement name
                    if (line.IndexOf("[") > -1)
                    {
                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                        State = true;
                        timeUnlock = 0;
                        DateUnlocked = null;
                    }

                    // Unlock
                    if (line.IndexOf("timestamp") > -1)
                    {
                        sTimeUnlock = line.Replace("timestamp=", string.Empty);
                        timeUnlock = int.Parse(sTimeUnlock);
                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();
                    }

                    if (line == string.Empty)
                    {
                        // End Achievement
                        if (timeUnlock != 0 && State)
                        {
                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });

                            Name = string.Empty;
                            State = false;
                            timeUnlock = 0;
                            DateUnlocked = null;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return ReturnAchievements;
        }

        private List<Achievement> ReadAchievementsStatsINI(string pathFile, List<Achievement> ReturnAchievements)
        {
            try
            {
                string line;
                bool startAchievement = false;

                string Name = string.Empty;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.IsEqual("[ACHIEVEMENTS]"))
                    {
                        startAchievement = true;
                    }
                    else if (startAchievement)
                    {
                        if (!line.Trim().IsNullOrEmpty())
                        {
                            string[] data = line.Split('=');
                            Name = data[0].Trim();
                            sTimeUnlock = data.Last().Trim();
                            timeUnlock = int.Parse(sTimeUnlock.Replace("{unlocked = true, time = ", string.Empty).Replace("}", string.Empty));
                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                            if (timeUnlock != 0)
                            {
                                ReturnAchievements.Add(new Achievement
                                {
                                    ApiName = Name,
                                    Name = string.Empty,
                                    Description = string.Empty,
                                    UrlUnlocked = string.Empty,
                                    UrlLocked = string.Empty,
                                    DateUnlocked = DateUnlocked
                                });

                                Name = string.Empty;
                                timeUnlock = 0;
                                DateUnlocked = null;
                            }
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return ReturnAchievements;
        }

        private List<Achievement> ReadAchievementsINI_type2(string pathFile, List<Achievement> ReturnAchievements)
        {
            try
            {
                string line;
                bool startAchievement = false;

                string Name = string.Empty;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.IsEqual("[Time]"))
                    {
                        startAchievement = true;
                    }
                    else if (startAchievement)
                    {
                        var data = line.Split('=');
                        Name = data[0];
                        sTimeUnlock = data[1];
                        timeUnlock = BitConverter.ToInt32(StringToByteArray(sTimeUnlock), 0);
                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                        if (timeUnlock != 0)
                        {
                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });

                            Name = string.Empty;
                            timeUnlock = 0;
                            DateUnlocked = null;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return ReturnAchievements;
        }

        //eFMann - New OnlineFix Achievments.ini type
        private List<Achievement> ReadOnlineFixAchievementsINI(string pathFile, List<Achievement> ReturnAchievements)
        {
            try
            {
                string line;
                string currentAchievement = string.Empty;
                bool achieved = false;
                DateTime? dateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    line = line.Trim();

                    // Achievement name
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        // If we have a previous achievement, add it to the list
                        if (!string.IsNullOrEmpty(currentAchievement) && achieved && dateUnlocked.HasValue)
                        {
                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = currentAchievement,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = dateUnlocked
                            });
                        }

                        // Start new achievement
                        currentAchievement = line.Substring(1, line.Length - 2);
                        achieved = false;
                        dateUnlocked = null;
                    }
                    else if (line.StartsWith("achieved="))
                    {
                        achieved = line.Equals("achieved=true", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("timestamp="))
                    {
                        long timestamp;
                        if (long.TryParse(line.Substring("timestamp=".Length), out timestamp))
                        {
                            dateUnlocked = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
                        }
                    }
                }

                // Add the last achievement if it exists
                if (!string.IsNullOrEmpty(currentAchievement) && achieved && dateUnlocked.HasValue)
                {
                    ReturnAchievements.Add(new Achievement
                    {
                        ApiName = currentAchievement,
                        Name = string.Empty,
                        Description = string.Empty,
                        UrlUnlocked = string.Empty,
                        UrlLocked = string.Empty,
                        DateUnlocked = dateUnlocked
                    });
                }

                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            return ReturnAchievements;
        }


        private SteamEmulatorData Get(Game game, uint appId, string apiKey, bool IsManual)
        {
            List<Achievement> ReturnAchievements = new List<Achievement>();
            List<GameStats> ReturnStats = new List<GameStats>();

            if (!IsManual)
            {

                // Search data local
                foreach (string DirAchivements in AchievementsDirectories)
                {
                    switch (DirAchivements.ToLower())
                    {
                        case "%public%\\documents\\steam\\codex":
                        case "%appdata%\\steam\\codex":
                        case "%public%\\documents\\steam\\rune": // eFMann - added Rune path
                        case "%appdata%\\steam\\rune":           // eFMann - added Rune path
                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\achievements.ini"))
                            {
                                string line;

                                string Name = string.Empty;
                                DateTime? DateUnlocked = null;

                                StreamReader file = new StreamReader(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\achievements.ini");
                                while ((line = file.ReadLine()) != null)
                                {
                                    // Achievement name
                                    if (line.IndexOf("[") > -1)
                                    {
                                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                                    }

                                    // Achievement UnlockTime
                                    if (line.IndexOf("UnlockTime") > -1 && line.ToLower() != "unlocktime=0")
                                    {
                                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(int.Parse(line.Replace("UnlockTime=", string.Empty))).ToLocalTime();
                                    }

                                    // End Achievement
                                    if (line.Trim() == string.Empty && DateUnlocked != null)
                                    {
                                        ReturnAchievements.Add(new Achievement
                                        {
                                            ApiName = Name,
                                            Name = string.Empty,
                                            Description = string.Empty,
                                            UrlUnlocked = string.Empty,
                                            UrlLocked = string.Empty,
                                            DateUnlocked = DateUnlocked,
                                            NoRarety = false
                                        });

                                        Name = string.Empty;
                                        DateUnlocked = null;
                                    }
                                }
                                file.Close();
                            }

                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats.ini"))
                            {
                                ReturnStats = ReadStatsINI(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats.ini", ReturnStats);
                            }

                            break;

                        case "%public%\\documents\\onlinefix": // eFMann - added OnlineFix case
                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats\\achievements.ini"))
                            {
                                ReturnAchievements = ReadOnlineFixAchievementsINI(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats\\achievements.ini", ReturnAchievements);
                            }

                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats\\stats.ini"))
                            {
                                ReturnStats = ReadStatsINI(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats\\stats.ini", ReturnStats);
                            }
                            break;

                        case "%documents%\\valve":
                            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"\\VALVE\\{AppId}\\ALI213\\Stats\\Achievements.Bin"))
                            {
                                string line;
                                string Name = string.Empty;
                                bool State = false;
                                string sTimeUnlock = string.Empty;
                                int timeUnlock = 0;
                                DateTime? DateUnlocked = null;

                                string pathFile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"\\VALVE\\{AppId}\\ALI213\\Stats\\Achievements.Bin";
                                StreamReader file = new StreamReader(pathFile);

                                while ((line = file.ReadLine()) != null)
                                {
                                    // Achievement name
                                    if (line.IndexOf("[") > -1)
                                    {
                                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                                        State = false;
                                        timeUnlock = 0;
                                        DateUnlocked = null;
                                    }

                                    if (Name != "Steam")
                                    {
                                        // State
                                        if (line.ToLower() == "haveachieved=1")
                                        {
                                            State = true;
                                        }

                                        // Unlock
                                        if (line.IndexOf("HaveAchievedTime") > -1 && line.ToLower() != "haveachievedtime=0000000000")
                                        {
                                            if (line.Contains("HaveAchievedTime="))
                                            {
                                                sTimeUnlock = line.Replace("HaveAchievedTime=", string.Empty);
                                                timeUnlock = Int32.Parse(sTimeUnlock);
                                            }
                                        }

                                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                                        // End Achievement
                                        if (timeUnlock != 0 && State)
                                        {
                                            ReturnAchievements.Add(new Achievement
                                            {
                                                ApiName = Name,
                                                Name = string.Empty,
                                                Description = string.Empty,
                                                UrlUnlocked = string.Empty,
                                                UrlLocked = string.Empty,
                                                DateUnlocked = DateUnlocked,
                                                NoRarety = false
                                            });

                                            Name = string.Empty;
                                            State = false;
                                            timeUnlock = 0;
                                            DateUnlocked = null;
                                        }
                                    }
                                }
                            }

                            break;

                        case "%appdata%\\goldberg steamemu saves":
                        case "%appdata%\\gse saves": // eFMann - added GSE case


                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\achievements.json"))
                            {
                                string Name = string.Empty;
                                DateTime? DateUnlocked = null;

                                string jsonText = File.ReadAllText(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\achievements.json");
                                foreach (dynamic achievement in Serialization.FromJson<dynamic>(jsonText))
                                {
                                    // eFMann - added an exlusions to remove [''] from APIName for games like Forza Horizon 4
                                    Name = achievement.Name?.ToString() ?? ((DynamicObject)achievement).GetDynamicMemberNames().First();
                                    /* 
                                     // Pottentially added/replace it in if later some achievements don't work
                                     Name = achievement.Name?.ToString() ?? 
       ((DynamicObject)achievement).GetDynamicMemberNames().FirstOrDefault() ?? 
       achievement.Path; // Fallback to original behavior if everything else fails
                                    */


                                    dynamic elements = achievement.First;
                                    dynamic unlockedTimeToken = elements.SelectToken("earned_time").Value;

                                    if (unlockedTimeToken is string)
                                    {
                                        unlockedTimeToken = UInt64.Parse(unlockedTimeToken);
                                    }

                                    if (unlockedTimeToken > 0)
                                    {
                                        DateUnlocked = new DateTime(1970, 1, 1).AddSeconds(unlockedTimeToken);
                                    }

                                    if (Name != string.Empty && DateUnlocked != null)
                                    {
                                        ReturnAchievements.Add(new Achievement
                                        {
                                            ApiName = Name,
                                            Name = string.Empty,
                                            Description = string.Empty,
                                            UrlUnlocked = string.Empty,
                                            UrlLocked = string.Empty,
                                            DateUnlocked = DateUnlocked,
                                            NoRarety = false
                                        });

                                        Name = string.Empty;
                                        DateUnlocked = null;
                                    }
                                }
                            }

                            break;

                        case "%public%\\documents\\empress": // eFMann - added EMPRESS case    
                        case "%appdata%\\empress": // eFMann - added EMPRESS case
                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\remote\\{AppId}\\achievements.json"))
                            {
                                string Name = string.Empty;
                                DateTime? DateUnlocked = null;

                                string jsonText = File.ReadAllText(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\remote\\{AppId}\\achievements.json");
                                foreach (dynamic achievement in Serialization.FromJson<dynamic>(jsonText))
                                {
                                    // eFMann - added an exlusions to remove [''] from APIName for games like Forza Horizon 4
                                    Name = achievement.Name?.ToString() ?? ((DynamicObject)achievement).GetDynamicMemberNames().First();

                                    dynamic elements = achievement.First;
                                    dynamic unlockedTimeToken = elements.SelectToken("earned_time");

                                    if (unlockedTimeToken.Value > 0)
                                    {
                                        DateUnlocked = new DateTime(1970, 1, 1).AddSeconds(unlockedTimeToken.Value);
                                    }

                                    if (Name != string.Empty && DateUnlocked != null)
                                    {
                                        ReturnAchievements.Add(new Achievement
                                        {
                                            ApiName = Name,
                                            Name = string.Empty,
                                            Description = string.Empty,
                                            UrlUnlocked = string.Empty,
                                            UrlLocked = string.Empty,
                                            DateUnlocked = DateUnlocked,
                                            NoRarety = false
                                        });

                                        Name = string.Empty;
                                        DateUnlocked = null;
                                    }
                                }
                            }

                            break;

                        case "%appdata%\\smartsteamemu":
                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats.bin"))
                            {
                                string Name = string.Empty;
                                int header = 0;
                                byte[] headerbyte = new byte[4];
                                byte[] statbyte = new byte[24];
                                byte[] namebyte = new byte[4];
                                byte[] datebyte = new byte[4];
                                Dictionary<string, string> achnames = new Dictionary<string, string>();
                                List<byte[]> stats = new List<byte[]>();
                                DateTime? DateUnlocked = null;
                                int statcount = 0;
                                Crc32 crc = new Crc32();

                                byte[] allData = File.ReadAllBytes(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{AppId}\\stats.bin");
                                statcount = (allData.Length - 4) / 24;

                                //logger.Warn($"Count of achievements unlocked is {statcount}.");
                                Buffer.BlockCopy(allData, 0, headerbyte, 0, 4);
                                //Array.Reverse(headerbyte);
                                header = BitConverter.ToInt32(headerbyte, 0);
                                //logger.Warn($"header was found as {header}");
                                allData = allData.Skip(4).Take(allData.Length - 4).ToArray();

                                for (int c = 24, j = 0; j < statcount; j++)
                                {
                                    //Buffer.BlockCopy(allData, i, statbyte, 0, 24);
                                    stats.Add(allData.Take(c).ToArray());
                                    allData = allData.Skip(c).Take(allData.Length - c).ToArray();
                                }

                                if (stats.Count != header)
                                {
                                    Common.LogError(new Exception("Invalid File"), false, "Invalid File", true, PluginDatabase.PluginName);
                                }
                                string language = CodeLang.GetSteamLang(API.Instance.ApplicationSettings.Language);
                                string site = string.Format(@"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={0}&appid={1}&l={2}", apiKey, AppId, language);

                                string Results = string.Empty;
                                try
                                {
                                    Results = Web.DownloadStringData(site).GetAwaiter().GetResult();
                                }
                                catch (WebException ex)
                                {
                                    if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                                    {
                                        var resp = (HttpWebResponse)ex.Response;
                                        switch (resp.StatusCode)
                                        {
                                            case HttpStatusCode.BadRequest: // HTTP 400
                                                break;
                                            case HttpStatusCode.ServiceUnavailable: // HTTP 503
                                                break;
                                            default:
                                                Common.LogError(ex, false, $"Failed to load from {site}", true, PluginDatabase.PluginName);
                                                break;
                                        }
                                    }
                                }

                                if (Results != string.Empty && Results.Length > 50)
                                {
                                    dynamic resultObj = Serialization.FromJson<dynamic>(Results);
                                    dynamic resultItems = null;
                                    try
                                    {
                                        resultItems = resultObj["game"]?["availableGameStats"]?["achievements"];
                                        for (int i = 0; i < resultItems?.Count; i++)
                                        {
                                            string achname = resultItems[i]["name"];
                                            byte[] bn = Encoding.ASCII.GetBytes(achname);
                                            string hash = string.Empty;
                                            foreach (byte b in crc.ComputeHash(bn)) hash += b.ToString("x2").ToUpper();
                                            hash = Hyphenate(hash, 2);
                                            achnames.Add(hash, achname);
                                        }
                                    }
                                    catch
                                    {
                                        Common.LogError(new Exception("Error getting achievement names"), false, "Error getting achievement names", true, PluginDatabase.PluginName);
                                    }
                                }

                                for (int i = 0; i < stats.Count; i++)
                                {
                                    try
                                    {
                                        Buffer.BlockCopy(stats[i], 0, namebyte, 0, 4);
                                        Array.Reverse(namebyte);
                                        Buffer.BlockCopy(stats[i], 8, datebyte, 0, 4);
                                        Name = BitConverter.ToString(namebyte);

                                        if (achnames.ContainsKey(Name))
                                        {
                                            Name = achnames[Name];
                                            int Date = BitConverter.ToInt32(datebyte, 0);
                                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(Date).ToLocalTime();
                                            if (Name != string.Empty && DateUnlocked != null)
                                            {
                                                ReturnAchievements.Add(new Achievement
                                                {
                                                    ApiName = Name,
                                                    Name = string.Empty,
                                                    Description = string.Empty,
                                                    UrlUnlocked = string.Empty,
                                                    UrlLocked = string.Empty,
                                                    DateUnlocked = DateUnlocked,
                                                    NoRarety = false
                                                });
                                            }
                                            Name = string.Empty;
                                            DateUnlocked = null;
                                        }
                                        else
                                        {
                                            Common.LogDebug(true, $"No matches found for crc in stats.bin.");
                                        }
                                    }
                                    catch
                                    {
                                        Common.LogError(new Exception("Stats.bin file format incorrect for SSE"), false, "Stats.bin file format incorrect for SSE", true, PluginDatabase.PluginName);
                                    }

                                    Array.Clear(namebyte, 0, namebyte.Length);
                                    Array.Clear(datebyte, 0, datebyte.Length);
                                }
                            }
                            break;

                        case "%documents%\\skidrow":
                        case "%documents%\\darksiders":
                            string skidrowfile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"\\SKIDROW\\{AppId}\\SteamEmu\\UserStats\\achiev.ini";
                            string darksidersfile = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + $"\\DARKSiDERS\\{AppId}\\SteamEmu\\UserStats\\achiev.ini";
                            string emu = "";

                            if (File.Exists(skidrowfile))
                            {
                                emu = skidrowfile;
                            }
                            else if (File.Exists(darksidersfile))
                            {
                                emu = darksidersfile;
                            }

                            if (!(emu == ""))
                            {
                                Common.LogDebug(true, $"File found at {emu}");
                                string line;
                                string Name = string.Empty;
                                DateTime? DateUnlocked = null;
                                List<List<string>> achlist = new List<List<string>>();
                                StreamReader r = new StreamReader(emu);

                                while ((line = r.ReadLine()) != null)
                                {
                                    // Achievement Name
                                    if (line.IndexOf("[AchievementsUnlockTimes]") > -1)
                                    {
                                        string nextline = r.ReadLine();
                                        while (nextline.IndexOf("[") == -1)
                                        {
                                            achlist.Add(new List<string>(nextline.Split('=')));
                                            nextline = r.ReadLine();
                                        }

                                        foreach (List<string> l in achlist)
                                        {
                                            Name = l[0];
                                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(int.Parse(l[1])).ToLocalTime();
                                            if (Name != string.Empty && DateUnlocked != null)
                                            {
                                                ReturnAchievements.Add(new Achievement
                                                {
                                                    ApiName = Name,
                                                    Name = string.Empty,
                                                    Description = string.Empty,
                                                    UrlUnlocked = string.Empty,
                                                    UrlLocked = string.Empty,
                                                    DateUnlocked = DateUnlocked,
                                                    NoRarety = false
                                                });

                                                Name = string.Empty;
                                                DateUnlocked = null;
                                            }
                                        }
                                    }
                                }
                                r.Close();
                            }

                            break;

                        case "%programdata%\\steam":
                            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramData%\\Steam")))
                            {
                                string[] dirsUsers = Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%ProgramData%\\Steam"));
                                foreach (string dirUser in dirsUsers)
                                {
                                    if (File.Exists(dirUser + $"\\{AppId}\\stats\\achievements.ini"))
                                    {
                                        ReturnAchievements = ReadAchievementsINI(dirUser + $"\\{AppId}\\stats\\achievements.ini", ReturnAchievements);
                                    }

                                    if (File.Exists(dirUser + $"\\{AppId}\\stats\\stats.ini"))
                                    {
                                        ReturnStats = ReadStatsINI(dirUser + $"\\{AppId}\\stats\\stats.ini", ReturnStats);
                                    }
                                }
                            }

                            break;

                        case "%localappdata%\\skidrow":
                            Common.LogDebug(true, $"No treatment for {DirAchivements}");
                            break;

                        default: // eFMann - added Custom Folder Paths case
                            if (ReturnAchievements.Count == 0)
                            {
                                Folder finded = PluginDatabase.PluginSettings.Settings.LocalPath.Find(x => x.FolderPath.IsEqual(DirAchivements));
                                Guid.TryParse(finded?.GameId, out Guid GameId);

                                // Check for Goldberg format (achievements.json)
                                if (File.Exists(DirAchivements + $"\\{AppId}\\achievements.json"))
                                {
                                    string Name = string.Empty;
                                    DateTime? DateUnlocked = null;

                                    string jsonText = File.ReadAllText(DirAchivements + $"\\{AppId}\\achievements.json");
                                    foreach (dynamic achievement in Serialization.FromJson<dynamic>(jsonText))
                                    {
                                        Name = achievement.Path;

                                        dynamic elements = achievement.First;
                                        dynamic unlockedTimeToken = elements.SelectToken("earned_time");

                                        if (unlockedTimeToken.Value > 0)
                                        {
                                            DateUnlocked = new DateTime(1970, 1, 1).AddSeconds(unlockedTimeToken.Value);
                                        }

                                        if (Name != string.Empty && DateUnlocked != null)
                                        {
                                            ReturnAchievements.Add(new Achievement
                                            {
                                                ApiName = Name,
                                                Name = string.Empty,
                                                Description = string.Empty,
                                                UrlUnlocked = string.Empty,
                                                UrlLocked = string.Empty,
                                                DateUnlocked = DateUnlocked,
                                                NoRarety = false
                                            });

                                            Name = string.Empty;
                                            DateUnlocked = null;
                                        }
                                    }
                                }

                                // Also check EMPRESS format which uses similar structure but different path
                                if (File.Exists(DirAchivements + $"\\{AppId}\\remote\\{AppId}\\achievements.json"))
                                {
                                    string Name = string.Empty;
                                    DateTime? DateUnlocked = null;

                                    string jsonText = File.ReadAllText(DirAchivements + $"\\{AppId}\\remote\\{AppId}\\achievements.json");
                                    foreach (dynamic achievement in Serialization.FromJson<dynamic>(jsonText))
                                    {
                                        // eFMann - added an exlusions to remove [''] from APIName for games like Forza Horizon 4
                                        Name = achievement.Name?.ToString() ?? ((DynamicObject)achievement).GetDynamicMemberNames().First();

                                        dynamic elements = achievement.First;
                                        dynamic unlockedTimeToken = elements.SelectToken("earned_time");

                                        if (unlockedTimeToken.Value > 0)
                                        {
                                            DateUnlocked = new DateTime(1970, 1, 1).AddSeconds(unlockedTimeToken.Value);
                                        }

                                        if (Name != string.Empty && DateUnlocked != null)
                                        {
                                            ReturnAchievements.Add(new Achievement
                                            {
                                                ApiName = Name,
                                                Name = string.Empty,
                                                Description = string.Empty,
                                                UrlUnlocked = string.Empty,
                                                UrlLocked = string.Empty,
                                                DateUnlocked = DateUnlocked,
                                                NoRarety = false
                                            });

                                            Name = string.Empty;
                                            DateUnlocked = null;
                                        }
                                    }
                                }

                                // Try all other formats
                                if (File.Exists(DirAchivements + "\\user_stats.ini"))
                                {
                                    ReturnAchievements = ReadAchievementsStatsINI(DirAchivements + "\\user_stats.ini", ReturnAchievements);
                                }

                                if (File.Exists(DirAchivements + $"\\{AppId}\\stats\\achievements.ini"))
                                {
                                    ReturnAchievements = ReadAchievementsINI(DirAchivements + $"\\{AppId}\\stats\\achievements.ini", ReturnAchievements);
                                    if (File.Exists(DirAchivements + $"\\{AppId}\\stats\\stats.ini"))
                                    {
                                        ReturnStats = ReadStatsINI(DirAchivements + $"\\{AppId}\\stats\\stats.ini", ReturnStats);
                                    }
                                }

                                if (File.Exists(DirAchivements + $"\\{AppId}\\stats\\achievements.ini"))
                                {
                                    ReturnAchievements = ReadOnlineFixAchievementsINI(DirAchivements + $"\\{AppId}\\stats\\achievements.ini", ReturnAchievements);
                                }

                                if (File.Exists(DirAchivements + $"\\achievements.ini"))
                                {
                                    ReturnAchievements = ReadAchievementsINI(DirAchivements + $"\\achievements.ini", ReturnAchievements);
                                    if (File.Exists(DirAchivements + $"\\stats.ini"))
                                    {
                                        ReturnStats = ReadStatsINI(DirAchivements + $"\\stats.ini", ReturnStats);
                                    }
                                }

                                if (ReturnAchievements.Count == 0)
                                {
                                    ReturnAchievements = GetSteamEmu(DirAchivements + $"\\{AppId}\\SteamEmu");
                                    if (ReturnAchievements.Count == 0)
                                    {
                                        ReturnAchievements = GetSteamEmu(DirAchivements);
                                    }
                                }
                            }
                            break;
                    }
                }

                Common.LogDebug(true, $"{Serialization.ToJson(ReturnAchievements)}");

                if (ReturnAchievements == new List<Achievement>())
                {
                    Common.LogDebug(true, $"No data for {AppId}");
                    return new SteamEmulatorData { Achievements = new List<Achievement>(), Stats = new List<GameStats>() };
                }
            }

            #region Get details achievements & stats
            // List details acheviements
            string lang = CodeLang.GetSteamLang(API.Instance.ApplicationSettings.Language);
            string url = string.Format(@"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={0}&appid={1}&l={2}", apiKey, AppId, lang);

            string ResultWeb = string.Empty;
            try
            {
                ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    HttpWebResponse resp = (HttpWebResponse)ex.Response;
                    switch (resp.StatusCode)
                    {
                        case HttpStatusCode.BadRequest: // HTTP 400
                            break;
                        case HttpStatusCode.ServiceUnavailable: // HTTP 503
                            break;
                        default:
                            Common.LogError(ex, false, $"Failed to load from {url}", true, PluginDatabase.PluginName);
                            break;
                    }
                    return new SteamEmulatorData { Achievements = new List<Achievement>(), Stats = new List<GameStats>() };
                }
            }

            if (ResultWeb != string.Empty && ResultWeb.Length > 50)
            {
                dynamic resultObj = Serialization.FromJson<dynamic>(ResultWeb);
                dynamic resultItems = null;
                dynamic resultItemsStats = null;

                try
                {
                    resultItems = resultObj["game"]?["availableGameStats"]?["achievements"];
                    resultItemsStats = resultObj["game"]?["availableGameStats"]?["stats"];

                    for (int i = 0; i < resultItems?.Count; i++)
                    {
                        bool isFind = false;
                        for (int j = 0; j < ReturnAchievements.Count; j++)
                        {
                            if (ReturnAchievements[j].ApiName.IsEqual(((string)resultItems[i]["name"])))
                            {
                                Achievement temp = new Achievement
                                {
                                    ApiName = (string)resultItems[i]["name"],
                                    Name = (string)resultItems[i]["displayName"],
                                    Description = (string)resultItems[i]["description"],
                                    UrlUnlocked = (string)resultItems[i]["icon"],
                                    UrlLocked = (string)resultItems[i]["icongray"],
                                    DateUnlocked = ReturnAchievements[j].DateUnlocked
                                };

                                isFind = true;
                                ReturnAchievements[j] = temp;
                                j = ReturnAchievements.Count;
                            }
                        }

                        if (!isFind)
                        {
                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = (string)resultItems[i]["name"],
                                Name = (string)resultItems[i]["displayName"],
                                Description = (string)resultItems[i]["description"],
                                UrlUnlocked = (string)resultItems[i]["icon"],
                                UrlLocked = (string)resultItems[i]["icongray"],
                                DateUnlocked = default(DateTime)
                            });
                        }
                    }

                    if (ReturnStats.Count > 0)
                    {
                        for (int i = 0; i < resultItemsStats?.Count; i++)
                        {
                            bool isFind = false;
                            for (int j = 0; j < ReturnStats.Count; j++)
                            {
                                if (ReturnStats[j].Name.IsEqual(((string)resultItemsStats[i]["name"])))
                                {
                                    GameStats temp = new GameStats
                                    {
                                        Name = (string)resultItemsStats[i]["name"],
                                        Value = ReturnStats[j].Value
                                    };

                                    isFind = true;
                                    ReturnStats[j] = temp;
                                    j = ReturnStats.Count;
                                }
                            }

                            if (!isFind)
                            {
                                ReturnStats.Add(new GameStats
                                {
                                    Name = (string)resultItemsStats[i]["name"],
                                    Value = 0
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true, $"Failed to parse");
                    return new SteamEmulatorData { Achievements = new List<Achievement>(), Stats = new List<GameStats>() };
                }
            }
            #endregion

            // Delete empty (SteamEmu)
            ReturnAchievements = ReturnAchievements.Select(x => x).Where(x => !string.IsNullOrEmpty(x.UrlLocked)).ToList();

            // Make sure achievements are marked to show rarity
            foreach (var achievement in ReturnAchievements)
            {
                achievement.NoRarety = false;  // Explicitly ensure NoRarety is false
            }

            return new SteamEmulatorData
            {
                Achievements = ReturnAchievements,
                Stats = ReturnStats
            };
        }


        private List<Achievement> GetSteamEmu(string DirAchivements)
        {
            List<Achievement> ReturnAchievements = new List<Achievement>();

            if (File.Exists(DirAchivements + $"\\stats.ini"))
            {
                bool IsGoodSection = false;
                string line;

                string Name = string.Empty;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(DirAchivements + $"\\stats.ini");
                while ((line = file.ReadLine()) != null)
                {
                    if (IsGoodSection)
                    {
                        // End list achievements unlocked
                        if (line.IndexOf("[Achievements]") > -1)
                        {
                            IsGoodSection = false;
                        }
                        else
                        {
                            string[] data = line.Split('=');

                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(int.Parse(data[1])).ToLocalTime();
                            Name = data[0];

                            ReturnAchievements.Add(new Achievement
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });
                        }
                    }

                    // Start list achievements unlocked
                    if (line.IndexOf("[AchievementsUnlockTimes]") > -1)
                    {
                        IsGoodSection = true;
                    }
                }
                file.Close();
            }

            return ReturnAchievements;
        }
        #endregion
    }


    public class SteamEmulatorData
    {
        public List<Achievement> Achievements { get; set; }
        public List<GameStats> Stats { get; set; }
    }
}
