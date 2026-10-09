using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
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
    public long EventId { get; set; }
    public List<string> ParticipantIds { get; set; } = new List<string>();
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
    private const double SameEventTimeTolerance = 0.000001;

    private static Dictionary<string, HeroHistoryData> _data = new Dictionary<string, HeroHistoryData>();
    private static bool _isLoaded = false;
    private static bool _recoveredFromBackup = false;
    private static string? _activeCampaignId;
    private static string? _activeDirectory;
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

    private static string CurrentCampaignId()
    {
        if (Campaign.Current != null && !string.IsNullOrEmpty(Campaign.Current.UniqueGameId))
        {
            return Campaign.Current.UniqueGameId;
        }
        return "default";
    }

    private static string HistoryPath(string directory, string campaignId)
    {
        return Path.Combine(directory, $"history_{campaignId}.json");
    }

    public static void Load()
    {
        LoadCampaign(CurrentCampaignId(), GetStorageDirectory());
        if (!_isLoaded) return;

        try
        {
            SyncFromGame();
            Save();
        }
        catch { }
    }

    public static void LoadCampaign(string campaignId, string storageDirectory)
    {
        lock (_fileLock)
        {
            _isLoaded = false;
            _recoveredFromBackup = false;
            _activeCampaignId = null;
            _activeDirectory = null;
            _data = new Dictionary<string, HeroHistoryData>();

            if (string.IsNullOrEmpty(campaignId) || string.IsNullOrEmpty(storageDirectory)) return;

            try
            {
                Directory.CreateDirectory(storageDirectory);
                string path = HistoryPath(storageDirectory, campaignId);
                Dictionary<string, HeroHistoryData>? loaded = ReadHistory(path, out bool recoveredFromBackup);
                if (loaded == null) return;

                _data = loaded;
                _recoveredFromBackup = recoveredFromBackup;
                _activeCampaignId = campaignId;
                _activeDirectory = storageDirectory;
                _isLoaded = true;
            }
            catch
            {
                _isLoaded = false;
                _activeCampaignId = null;
                _activeDirectory = null;
                _data = new Dictionary<string, HeroHistoryData>();
            }
        }
    }

    public static Dictionary<string, HeroHistoryData>? ReadHistory(string path, out bool recoveredFromBackup)
    {
        recoveredFromBackup = false;
        if (!File.Exists(path)) return new Dictionary<string, HeroHistoryData>();

        Dictionary<string, HeroHistoryData>? parsed = TryParseHistory(path);
        if (parsed != null) return parsed;

        string backupPath = path + ".bak";
        if (File.Exists(backupPath))
        {
            Dictionary<string, HeroHistoryData>? fromBackup = TryParseHistory(backupPath);
            if (fromBackup != null)
            {
                recoveredFromBackup = true;
                return fromBackup;
            }
        }

        return null;
    }

    private static Dictionary<string, HeroHistoryData>? TryParseHistory(string path)
    {
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonConvert.DeserializeObject<Dictionary<string, HeroHistoryData>>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save()
    {
        lock (_fileLock)
        {
            try
            {
                if (!_isLoaded || _data == null || _activeCampaignId == null || _activeDirectory == null) return;
                if (Campaign.Current != null && CurrentCampaignId() != _activeCampaignId) return;

                string path = HistoryPath(_activeDirectory, _activeCampaignId);
                if (_data.Count == 0 && File.Exists(path))
                {
                    var info = new FileInfo(path);
                    if (info.Length > 50) return;
                }

                WriteHistory(path, _data, _recoveredFromBackup);
                _recoveredFromBackup = false;
            }
            catch { }
        }
    }

    public static void WriteHistory(string path, IDictionary<string, HeroHistoryData> data, bool preserveBackup)
    {
        if (data == null) return;
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(json);
            writer.Flush();
            stream.Flush(true);
        }

        if (!File.Exists(path))
        {
            File.Move(tempPath, path);
            return;
        }

        // File.Replace меняет основной файл и резервную копию одним системным вызовом.
        // Если основной файл повреждён и данные восстановлены из .bak, хорошую копию не подменяем.
        string backupPath = preserveBackup ? path + ".corrupt" : path + ".bak";
        File.Replace(tempPath, path, backupPath, true);
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

    public static bool IsIntimateEvent(string typeName)
    {
        return typeName == "SexEvent" || typeName == "PrisonSexEvent" || typeName == "ThreesomeEvent";
    }

    /// <summary>
    /// Добавляет одну строку встречи конкретному герою.
    /// Повтор того же EventId не увеличивает число встреч.
    /// Старая запись без EventId с тем же партнёром и тем же временем получает номер события и тоже не дублируется.
    /// </summary>
    public static bool TryAddEncounter(HeroHistoryData owner, StoredEncounter incoming)
    {
        if (owner == null || incoming == null || string.IsNullOrEmpty(incoming.PartnerStringId)) return false;
        if (owner.Encounters == null) owner.Encounters = new List<StoredEncounter>();
        if (incoming.ParticipantIds == null) incoming.ParticipantIds = new List<string>();

        foreach (var existing in owner.Encounters)
        {
            if (existing == null) continue;
            if (incoming.EventId != 0 && existing.EventId == incoming.EventId && existing.PartnerStringId == incoming.PartnerStringId)
            {
                return false;
            }

            if (existing.EventId == 0 && incoming.EventId != 0
                && existing.PartnerStringId == incoming.PartnerStringId
                && existing.EncounterType == incoming.EncounterType
                && Math.Abs(existing.Days - incoming.Days) < SameEventTimeTolerance)
            {
                existing.EventId = incoming.EventId;
                if (existing.ParticipantIds == null || existing.ParticipantIds.Count == 0)
                {
                    existing.ParticipantIds = new List<string>(incoming.ParticipantIds);
                }
                return false;
            }
        }

        owner.Encounters.Add(incoming);
        return true;
    }

    public static int RecordGroup(Dictionary<string, HeroHistoryData> data, long eventId, IList<Hero> participants, double days, string type, bool wasAffair)
    {
        if (data == null || participants == null || participants.Count < 2) return 0;

        var people = participants.Where(h => h != null && !string.IsNullOrEmpty(h.StringId))
            .GroupBy(h => h.StringId)
            .Select(g => g.First())
            .ToList();
        if (people.Count < 2) return 0;

        var participantIds = people.Select(h => h.StringId).OrderBy(id => id).ToList();
        int added = 0;

        foreach (var owner in people)
        {
            if (!data.TryGetValue(owner.StringId, out HeroHistoryData ownerData))
            {
                ownerData = new HeroHistoryData
                {
                    HeroStringId = owner.StringId,
                    HeroName = owner.Name.ToString(),
                    IsFemale = owner.IsFemale
                };
                data[owner.StringId] = ownerData;
            }
            ownerData.HeroName = owner.Name.ToString();
            ownerData.IsFemale = owner.IsFemale;

            foreach (var partner in people)
            {
                if (partner.StringId == owner.StringId) continue;
                RelationshipOrientation orientation;
                if (!owner.IsFemale && !partner.IsFemale) orientation = RelationshipOrientation.Gay;
                else if (owner.IsFemale && partner.IsFemale) orientation = RelationshipOrientation.Lesbian;
                else orientation = RelationshipOrientation.Hetero;

                bool created = TryAddEncounter(ownerData, new StoredEncounter
                {
                    PartnerStringId = partner.StringId,
                    PartnerName = partner.Name.ToString(),
                    PartnerIsFemale = partner.IsFemale,
                    Days = days,
                    EncounterType = type,
                    WasAffair = wasAffair,
                    Orientation = orientation,
                    EventId = eventId,
                    ParticipantIds = new List<string>(participantIds)
                });
                if (created) added++;
            }
        }

        return added;
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

    public static bool CaptureLog(LogEntry log, bool save)
    {
        if (!_isLoaded || log == null || Campaign.Current == null || _activeCampaignId == null) return false;
        if (CurrentCampaignId() != _activeCampaignId) return false;

        bool added;
        lock (_fileLock)
        {
            added = CaptureLogUnlocked(log);
        }

        if (added && save) Save();
        return added;
    }

    public static void SyncFromGame()
    {
        try
        {
            if (Campaign.Current == null || Campaign.Current.LogEntryHistory == null) return;
            var logs = Campaign.Current.LogEntryHistory.GameActionLogs;
            if (logs == null) return;

            foreach (LogEntry log in logs)
            {
                CaptureLogUnlocked(log);
            }
        }
        catch { }
    }

    private static bool CaptureLogUnlocked(LogEntry log)
    {
        if (log == null) return false;
        string typeName = log.GetType().Name;
        string fullName = log.GetType().FullName ?? string.Empty;
        if (!fullName.StartsWith("Dramalord.Data.Events.", StringComparison.Ordinal)) return false;

        if (typeName == "BirthEvent")
        {
            Hero? motherOrActor = ReadHero(log, "Actor");
            Hero? other = ReadHero(log, "Target");
            Hero? child = ReadHero(log, "Offspring");
            if (motherOrActor != null && other != null && child != null)
            {
                Hero woman = motherOrActor.IsFemale ? motherOrActor : other;
                Hero man = motherOrActor.IsFemale ? other : motherOrActor;
                int before = ChildCount(woman, man);
                RecordChild(woman, man, child);
                return ChildCount(woman, man) > before;
            }
            return false;
        }

        if (!IsIntimateEvent(typeName)) return false;

        var participants = new List<Hero>();
        AddParticipant(participants, ReadHero(log, "Actor"));
        AddParticipant(participants, ReadHero(log, "Target"));
        AddParticipant(participants, ReadHero(log, "Third"));
        if (participants.Count < 2) return false;

        bool wasAffair = participants.Any(person =>
            person.Spouse != null && participants.All(other => other.StringId != person.Spouse.StringId));

        string type = typeName switch
        {
            "PrisonSexEvent" => "В плену",
            "ThreesomeEvent" => "Втроём",
            _ => "Близость"
        };

        return RecordGroup(_data, log.Id, participants, log.GameTime.ToDays, type, wasAffair) > 0;
    }

    private static int ChildCount(Hero mother, Hero father)
    {
        if (mother == null || father == null) return 0;
        if (!_data.TryGetValue(mother.StringId, out HeroHistoryData data)) return 0;
        if (data.ChildrenByPartner == null || !data.ChildrenByPartner.TryGetValue(father.StringId, out List<string> children) || children == null)
        {
            return 0;
        }
        return children.Count;
    }

    private static void AddParticipant(List<Hero> participants, Hero? hero)
    {
        if (hero == null || string.IsNullOrEmpty(hero.StringId)) return;
        if (participants.Any(existing => existing.StringId == hero.StringId)) return;
        participants.Add(hero);
    }

    private static Hero? ReadHero(object source, string propertyName)
    {
        try
        {
            PropertyInfo? property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(source) as Hero;
        }
        catch
        {
            return null;
        }
    }
}

[HarmonyPatch(typeof(LogEntryHistory), "AddActionLog")]
public static class DramalordLogCapture
{
    public static void Postfix(LogEntry actionLog)
    {
        try
        {
            PersistentHistoryTracker.CaptureLog(actionLog, true);
        }
        catch
        {
            // Запись истории не должна прерывать игровое событие.
        }
    }
}
