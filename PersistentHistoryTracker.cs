﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;

namespace DramalordSecrets;

public enum RelationshipOrientation
{
    Hetero,
    Gay,
    Lesbian
}

public class StoredEncounter
{
    public string PartnerStringId { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public bool PartnerIsFemale { get; set; }
    public double Days { get; set; }
    public string EncounterType { get; set; } = "Близость";
    public bool WasAffair { get; set; }
    public RelationshipOrientation Orientation { get; set; }
}

public class HeroHistoryData
{
    public string HeroStringId { get; set; } = string.Empty;
    public string HeroName { get; set; } = string.Empty;
    public bool IsFemale { get; set; }
    public List<StoredEncounter> Encounters { get; set; } = new List<StoredEncounter>();
    public Dictionary<string, List<string>> ChildrenByPartner { get; set; } = new Dictionary<string, List<string>>();
}

public static class PersistentHistoryTracker
{
    private static Dictionary<string, HeroHistoryData> _data = new Dictionary<string, HeroHistoryData>();
    private static bool _isLoaded = false;
    private static readonly object _fileLock = new object();

    public static string FormatDate(double days)
    {
        try
        {
            var time = CampaignTime.Days((float)days);
            int year = (int)time.GetYear;
            string season = time.GetSeasonOfYear switch
            {
                CampaignTime.Seasons.Spring => "Весна",
                CampaignTime.Seasons.Summer => "Лето",
                CampaignTime.Seasons.Autumn => "Осень",
                _ => "Зима"
            };
            int day = time.GetDayOfSeason + 1;
            return $"{season}, {day} день, {year} г.";
        }
        catch
        {
            return $"День {(int)days}";
        }
    }

    public static string GetStorageDirectory()
    {
        string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string folder = Path.Combine(myDocs, "Mount and Blade II Bannerlord", "DramalordSecrets");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return folder;
    }

    private static string GetDataFilePath()
    {
        string campaignId = Campaign.Current != null && !string.IsNullOrEmpty(Campaign.Current.UniqueGameId)
            ? Campaign.Current.UniqueGameId
            : "default";

        return Path.Combine(GetStorageDirectory(), $"history_{campaignId}.json");
    }

    public static void Load()
    {
        lock (_fileLock)
        {
            try
            {
                string path = GetDataFilePath();
                string bakPath = path + ".bak";

                Dictionary<string, HeroHistoryData>? loaded = null;

                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            loaded = JsonConvert.DeserializeObject<Dictionary<string, HeroHistoryData>>(json);
                        }
                    }
                    catch
                    {
                        loaded = null;
                    }
                }

                // If primary file failed or was empty, attempt recovery from backup
                if ((loaded == null || loaded.Count == 0) && File.Exists(bakPath))
                {
                    try
                    {
                        string bakJson = File.ReadAllText(bakPath, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(bakJson))
                        {
                            var bakLoaded = JsonConvert.DeserializeObject<Dictionary<string, HeroHistoryData>>(bakJson);
                            if (bakLoaded != null && bakLoaded.Count > 0)
                            {
                                loaded = bakLoaded;
                            }
                        }
                    }
                    catch { }
                }

                if (loaded != null)
                {
                    _data = loaded;
                    _isLoaded = true;
                }
                else if (!File.Exists(path))
                {
                    // Brand new campaign: initialize clean state
                    _data = new Dictionary<string, HeroHistoryData>();
                    _isLoaded = true;
                }
                else
                {
                    // Existing file could not be parsed and no backup available.
                    // Keep _isLoaded = false to NEVER overwrite existing data with empty data.
                }

                if (_isLoaded)
                {
                    SyncFromGame();
                }
            }
            catch { }
        }
    }

    public static void Save()
    {
        lock (_fileLock)
        {
            try
            {
                if (!_isLoaded || _data == null) return;

                string path = GetDataFilePath();
                string bakPath = path + ".bak";
                string tempPath = path + ".tmp";

                // Safety guard: never overwrite populated file with empty dictionary
                if (_data.Count == 0 && File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (fi.Length > 50) return;
                }

                string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(tempPath, json, Encoding.UTF8);

                // Atomic replacement with persistent backup
                if (File.Exists(path))
                {
                    try
                    {
                        File.Copy(path, bakPath, true);
                    }
                    catch { }

                    File.Copy(tempPath, path, true);
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch { }
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch { }
        }
    }

    public static HeroHistoryData GetOrCreateHero(Hero hero)
    {
        if (hero == null) return new HeroHistoryData();

        if (!_data.TryGetValue(hero.StringId, out HeroHistoryData heroData))
        {
            heroData = new HeroHistoryData
            {
                HeroStringId = hero.StringId,
                HeroName = hero.Name.ToString(),
                IsFemale = hero.IsFemale
            };
            _data[hero.StringId] = heroData;
        }
        heroData.HeroName = hero.Name.ToString();
        heroData.IsFemale = hero.IsFemale;
        return heroData;
    }

    public static void RecordEncounter(Hero heroA, Hero heroB, CampaignTime time, string type = "Близость", bool wasAffair = false)
    {
        if (heroA == null || heroB == null || heroA == heroB) return;

        RelationshipOrientation orientation;
        if (!heroA.IsFemale && !heroB.IsFemale) orientation = RelationshipOrientation.Gay;
        else if (heroA.IsFemale && heroB.IsFemale) orientation = RelationshipOrientation.Lesbian;
        else orientation = RelationshipOrientation.Hetero;

        double days = time.ToDays;

        // Record for Hero A
        var dataA = GetOrCreateHero(heroA);
        bool existsA = dataA.Encounters.Any(e => e.PartnerStringId == heroB.StringId && Math.Abs(e.Days - days) < 0.2);
        if (!existsA)
        {
            dataA.Encounters.Add(new StoredEncounter
            {
                PartnerStringId = heroB.StringId,
                PartnerName = heroB.Name.ToString(),
                PartnerIsFemale = heroB.IsFemale,
                Days = days,
                EncounterType = type,
                WasAffair = wasAffair,
                Orientation = orientation
            });
        }

        // Record for Hero B
        var dataB = GetOrCreateHero(heroB);
        bool existsB = dataB.Encounters.Any(e => e.PartnerStringId == heroA.StringId && Math.Abs(e.Days - days) < 0.2);
        if (!existsB)
        {
            dataB.Encounters.Add(new StoredEncounter
            {
                PartnerStringId = heroA.StringId,
                PartnerName = heroA.Name.ToString(),
                PartnerIsFemale = heroA.IsFemale,
                Days = days,
                EncounterType = type,
                WasAffair = wasAffair,
                Orientation = orientation
            });
        }
    }

    public static void RecordChild(Hero mother, Hero father, Hero child)
    {
        if (mother == null || father == null || child == null) return;

        var mData = GetOrCreateHero(mother);
        if (!mData.ChildrenByPartner.TryGetValue(father.StringId, out List<string> mChildren))
        {
            mChildren = new List<string>();
            mData.ChildrenByPartner[father.StringId] = mChildren;
        }
        if (!mChildren.Contains(child.StringId)) mChildren.Add(child.StringId);

        var fData = GetOrCreateHero(father);
        if (!fData.ChildrenByPartner.TryGetValue(mother.StringId, out List<string> fChildren))
        {
            fChildren = new List<string>();
            fData.ChildrenByPartner[mother.StringId] = fChildren;
        }
        if (!fChildren.Contains(child.StringId)) fChildren.Add(child.StringId);
    }

    public static HeroHistoryData? GetHeroHistory(Hero hero)
    {
        if (hero == null) return null;
        _data.TryGetValue(hero.StringId, out HeroHistoryData data);
        return data;
    }

    public static void SyncFromGame()
    {
        try
        {
            // 1. Scan Dramalord recorded events
            var dramEvents = DramalordHelper.GetRecordedEvents();
            if (dramEvents != null)
            {
                foreach (object ev in dramEvents)
                {
                    if (ev == null) continue;
                    string typeName = ev.GetType().Name;

                    if (typeName == "SexEvent" || typeName == "PrisonSexEvent")
                    {
                        Hero? actor = ev.GetType().GetProperty("Actor")?.GetValue(ev) as Hero;
                        Hero? target = ev.GetType().GetProperty("Target")?.GetValue(ev) as Hero;
                        object? gtObj = ev.GetType().GetProperty("GameTime")?.GetValue(ev);
                        CampaignTime gt = gtObj is CampaignTime time ? time : CampaignTime.Now;

                        if (actor != null && target != null)
                        {
                            bool affair = (actor.Spouse != null && actor.Spouse != target) ||
                                          (target.Spouse != null && target.Spouse != actor);
                            string type = typeName == "PrisonSexEvent" ? "В плену" : "Близость";
                            RecordEncounter(actor, target, gt, type, affair);
                        }
                    }
                    else if (typeName == "ThreesomeEvent")
                    {
                        Hero? actor = ev.GetType().GetProperty("Actor")?.GetValue(ev) as Hero;
                        Hero? target = ev.GetType().GetProperty("Target")?.GetValue(ev) as Hero;
                        Hero? third = ev.GetType().GetProperty("Third")?.GetValue(ev) as Hero;
                        object? gtObj = ev.GetType().GetProperty("GameTime")?.GetValue(ev);
                        CampaignTime gt = gtObj is CampaignTime time ? time : CampaignTime.Now;

                        var trio = new[] { actor, target, third }.Where(h => h != null).ToList();
                        for (int i = 0; i < trio.Count; i++)
                        {
                            for (int j = i + 1; j < trio.Count; j++)
                            {
                                RecordEncounter(trio[i]!, trio[j]!, gt, "Втроём", true);
                            }
                        }
                    }
                    else if (typeName == "BirthEvent")
                    {
                        Hero? actor = ev.GetType().GetProperty("Actor")?.GetValue(ev) as Hero;
                        Hero? target = ev.GetType().GetProperty("Target")?.GetValue(ev) as Hero;
                        Hero? offspring = ev.GetType().GetProperty("Offspring")?.GetValue(ev) as Hero;
                        if (actor != null && target != null && offspring != null)
                        {
                            Hero woman = actor.IsFemale ? actor : target;
                            Hero man = actor.IsFemale ? target : actor;
                            RecordChild(woman, man, offspring);
                        }
                    }
                }
            }

            // 2. Scan Campaign LogEntryHistory
            if (Campaign.Current != null && Campaign.Current.LogEntryHistory != null)
            {
                var logs = Campaign.Current.LogEntryHistory.GameActionLogs;
                if (logs != null)
                {
                    foreach (LogEntry log in logs)
                    {
                        if (log == null) continue;
                        string typeName = log.GetType().Name;

                        if (typeName == "SexEvent" || typeName == "PrisonSexEvent")
                        {
                            Hero? actor = log.GetType().GetProperty("Actor")?.GetValue(log) as Hero;
                            Hero? target = log.GetType().GetProperty("Target")?.GetValue(log) as Hero;
                            if (actor != null && target != null)
                            {
                                bool affair = (actor.Spouse != null && actor.Spouse != target) ||
                                              (target.Spouse != null && target.Spouse != actor);
                                string type = typeName == "PrisonSexEvent" ? "В плену" : "Близость";
                                RecordEncounter(actor, target, log.GameTime, type, affair);
                            }
                        }
                        else if (typeName == "ThreesomeEvent")
                        {
                            Hero? actor = log.GetType().GetProperty("Actor")?.GetValue(log) as Hero;
                            Hero? target = log.GetType().GetProperty("Target")?.GetValue(log) as Hero;
                            Hero? third = log.GetType().GetProperty("Third")?.GetValue(log) as Hero;

                            var trio = new[] { actor, target, third }.Where(h => h != null).ToList();
                            for (int i = 0; i < trio.Count; i++)
                            {
                                for (int j = i + 1; j < trio.Count; j++)
                                {
                                    RecordEncounter(trio[i]!, trio[j]!, log.GameTime, "Втроём", true);
                                }
                            }
                        }
                        else if (typeName == "AffairDiscoveredLogEntry")
                        {
                            Hero? actor = log.GetType().GetProperty("Actor")?.GetValue(log) as Hero;
                            Hero? target = log.GetType().GetProperty("Target")?.GetValue(log) as Hero;
                            if (actor != null && target != null)
                            {
                                RecordEncounter(actor, target, log.GameTime, "Измена", true);
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }
}
