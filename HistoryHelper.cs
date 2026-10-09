﻿﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;

namespace DramalordSecrets;

public class PartnerDisplayInfo
{
    public Hero Partner;
    public int IntimateCount = 0;
    public string LastDate = string.Empty;
    public double LastDays = 0;
    public bool IsSpouse = false;
    public bool IsLover = false;
    public bool HadAffair = false;
    public bool IsCurrentPregnancyPartner = false;
    public RelationshipOrientation Orientation = RelationshipOrientation.Hetero;
    public List<ChildDisplayInfo> Children = new List<ChildDisplayInfo>();

    public PartnerDisplayInfo(Hero partner)
    {
        Partner = partner;
    }
}

public class ChildDisplayInfo
{
    public Hero Child;
    public bool IsOrphan;
    public bool IsDead;

    public ChildDisplayInfo(Hero child, bool isOrphan, bool isDead)
    {
        Child = child;
        IsOrphan = isOrphan;
        IsDead = isDead;
    }
}

public static class HistoryHelper
{
    public const string HeaderSeparator = "--------------------------------------------------";
    public const string SummaryHeaderPrefix = "СВОДКА СВЯЗЕЙ (ЧИТ";
    public const string JournalHeader = "ЖУРНАЛ ПОСЛЕДНИХ ВСТРЕЧ:";

    public static string FormatAge(int age)
    {
        if (age < 1) return "до 1 года";
        int lastTwo = age % 100;
        int last = age % 10;
        if (lastTwo >= 11 && lastTwo <= 19) return $"{age} лет";
        if (last == 1) return $"{age} год";
        if (last >= 2 && last <= 4) return $"{age} года";
        return $"{age} лет";
    }

    private static PartnerDisplayInfo GetOrCreate(Dictionary<Hero, PartnerDisplayInfo> dict, Hero hero)
    {
        if (!dict.TryGetValue(hero, out PartnerDisplayInfo info))
        {
            info = new PartnerDisplayInfo(hero);
            dict[hero] = info;
        }
        return info;
    }

    public static Dictionary<Hero, PartnerDisplayInfo> CollectPartners(Hero subject)
    {
        var partners = new Dictionary<Hero, PartnerDisplayInfo>();
        if (subject == null) return partners;

        try
        {
            // 1. Current Husband / Wife (Native)
            if (subject.Spouse != null && subject.Spouse != subject)
            {
                GetOrCreate(partners, subject.Spouse).IsSpouse = true;
            }

            // 2. Current Pregnancy Partner
            if (subject.IsPregnant)
            {
                Hero? pregFather = DramalordHelper.GetPregnancyFather(subject);
                if (pregFather != null && pregFather != subject)
                {
                    GetOrCreate(partners, pregFather).IsCurrentPregnancyPartner = true;
                }
            }
            else
            {
                // Check if any alive hero is currently pregnant from this subject
                foreach (Hero woman in Hero.AllAliveHeroes)
                {
                    if (woman != null && woman.IsFemale && woman.IsPregnant)
                    {
                        Hero? f = DramalordHelper.GetPregnancyFather(woman);
                        if (f == subject)
                        {
                            GetOrCreate(partners, woman).IsCurrentPregnancyPartner = true;
                        }
                    }
                }
            }

            // 3. Scan Dramalord relations
            var dramRelations = DramalordHelper.GetAllDramalordRelations(subject);
            if (dramRelations != null)
            {
                foreach (DictionaryEntry de in dramRelations)
                {
                    if (de.Key is Hero other && other != subject)
                    {
                        int love = DramalordHelper.GetLove(subject, other);
                        bool isLover = DramalordHelper.IsLoverOf(subject, other);
                        bool isSpouse = DramalordHelper.IsSpouseOf(subject, other);

                        if (isLover || isSpouse || love >= 10)
                        {
                            var info = GetOrCreate(partners, other);
                            if (isLover) info.IsLover = true;
                            if (isSpouse) info.IsSpouse = true;
                        }
                    }
                }
            }

            // 4. Scan Persistent History Tracker
            var heroHistory = PersistentHistoryTracker.GetHeroHistory(subject);
            var encounters = heroHistory != null ? heroHistory.Encounters : new List<StoredEncounter>();

            foreach (var enc in encounters)
            {
                Hero? p = Hero.Find(enc.PartnerStringId);
                if (p != null && p != subject)
                {
                    var info = GetOrCreate(partners, p);
                    info.IntimateCount++;
                    info.Orientation = enc.Orientation;
                    if (enc.WasAffair) info.HadAffair = true;
                    if (enc.Days > info.LastDays)
                    {
                        info.LastDays = enc.Days;
                        info.LastDate = PersistentHistoryTracker.FormatDate(enc.Days);
                    }
                }
            }

            // 5. Gather All Orphans
            var orphans = DramalordHelper.GetOrphans();
            var orphanSet = new HashSet<Hero>(orphans);

            // 6. Gather ALL Children of subject (from Children, Orphans, and AllHeroes)
            var allChildren = new HashSet<Hero>();

            if (subject.Children != null)
            {
                foreach (Hero c in subject.Children)
                {
                    if (c != null) allChildren.Add(c);
                }
            }

            foreach (Hero orph in orphans)
            {
                if (orph != null)
                {
                    if (subject.IsFemale && orph.Mother != null && (orph.Mother == subject || orph.Mother.StringId == subject.StringId))
                        allChildren.Add(orph);
                    else if (!subject.IsFemale && orph.Father != null && (orph.Father == subject || orph.Father.StringId == subject.StringId))
                        allChildren.Add(orph);
                }
            }

            if (Campaign.Current != null)
            {
                if (Campaign.Current.AliveHeroes != null)
                {
                    foreach (Hero h in Campaign.Current.AliveHeroes)
                    {
                        if (h != null)
                        {
                            if (subject.IsFemale && h.Mother != null && (h.Mother == subject || h.Mother.StringId == subject.StringId))
                                allChildren.Add(h);
                            else if (!subject.IsFemale && h.Father != null && (h.Father == subject || h.Father.StringId == subject.StringId))
                                allChildren.Add(h);
                        }
                    }
                }

                if (Campaign.Current.DeadOrDisabledHeroes != null)
                {
                    foreach (Hero h in Campaign.Current.DeadOrDisabledHeroes)
                    {
                        if (h != null)
                        {
                            if (subject.IsFemale && h.Mother != null && (h.Mother == subject || h.Mother.StringId == subject.StringId))
                                allChildren.Add(h);
                            else if (!subject.IsFemale && h.Father != null && (h.Father == subject || h.Father.StringId == subject.StringId))
                                allChildren.Add(h);
                        }
                    }
                }
            }

            // Distribute children by partner
            foreach (Hero child in allChildren)
            {
                Hero? otherParent = subject.IsFemale ? child.Father : child.Mother;
                if (otherParent == null || otherParent == subject)
                {
                    otherParent = subject.Spouse;
                }

                if (otherParent != null && otherParent != subject)
                {
                    var pInfo = GetOrCreate(partners, otherParent);
                    if (!pInfo.Children.Any(cd => cd.Child == child || cd.Child.StringId == child.StringId))
                    {
                        bool isOrphan = orphanSet.Contains(child);
                        bool isDead = !child.IsAlive;
                        pInfo.Children.Add(new ChildDisplayInfo(child, isOrphan, isDead));
                    }
                }
            }

            // Determine orientation
            foreach (var pInfo in partners.Values)
            {
                if (!subject.IsFemale && !pInfo.Partner.IsFemale) pInfo.Orientation = RelationshipOrientation.Gay;
                else if (subject.IsFemale && pInfo.Partner.IsFemale) pInfo.Orientation = RelationshipOrientation.Lesbian;
                else pInfo.Orientation = RelationshipOrientation.Hetero;
            }
        }
        catch { }

        return partners;
    }

    public static string GetHeroSummaryText(Hero subject)
    {
        if (subject == null) return string.Empty;

        var partners = CollectPartners(subject);
        var settings = DramalordSecretsSettings.Instance;

        var sb = new StringBuilder();

        // =====================================================================
        // СЕКЦИЯ 1: СВОДКА ПО ПАРТНЁРАМ (ГРУППИРОВКА)
        // =====================================================================
        if (settings == null || settings.ShowGroupedSummary)
        {
            sb.Append("\n--------------------------------------------------");
            sb.Append($"\nСВОДКА СВЯЗЕЙ (ЧИТ: партнёров за жизнь: {partners.Count})");

            if (partners.Count == 0)
            {
                sb.Append("\n- Партнёров не зафиксировано.");
            }
            else
            {
                var sortedPartners = partners.Values
                    .OrderByDescending(p => p.IsCurrentPregnancyPartner)
                    .ThenByDescending(p => p.IsSpouse)
                    .ThenByDescending(p => p.IsLover)
                    .ThenByDescending(p => p.Children.Count)
                    .ThenByDescending(p => p.IntimateCount)
                    .ToList();

                int counter = 1;
                foreach (var info in sortedPartners)
                {
                    Hero partner = info.Partner;
                    string partnerName = partner.Name.ToString();

                    // Короткое обозначение типа связи пары: МЖ, ЖЖ, ММ
                    string orientationTag = (!subject.IsFemale && !partner.IsFemale) ? "ММ"
                                          : (subject.IsFemale && partner.IsFemale) ? "ЖЖ"
                                          : "МЖ";

                    var statuses = new List<string>();
                    if (info.IsSpouse) statuses.Add(partner.IsFemale ? "Супруга" : "Супруг");
                    if (info.IsLover) statuses.Add(partner.IsFemale ? "Любовница" : "Любовник");
                    if (info.IsCurrentPregnancyPartner)
                    {
                        statuses.Add(subject.IsFemale ? "Отец ребёнка" : "Беременна от него");
                    }
                    if (info.HadAffair) statuses.Add("измена");
                    if (statuses.Count == 0) statuses.Add("интим");

                    string statusStr = string.Join(", ", statuses);

                    // 1. Имя, статус и короткий тип связи
                    sb.Append($"\n\n{counter}. {partnerName} ({statusStr}) {orientationTag}");

                    // 2. Количество встреч (отдельной строкой)
                    if (info.IntimateCount > 0)
                    {
                        sb.Append($"\n   Встреч: {info.IntimateCount}");
                    }

                    // 3. Последняя дата (отдельной строкой)
                    if (!string.IsNullOrEmpty(info.LastDate))
                    {
                        sb.Append($"\n   Последняя: {info.LastDate}");
                    }

                    // 4. Дети (по одному на строку с возрастом, без избыточных скобок)
                    if (info.Children.Count > 0)
                    {
                        sb.Append($"\n   Дети ({info.Children.Count}):");
                        foreach (var cInfo in info.Children)
                        {
                            string cName = cInfo.Child.Name.ToString();
                            string ageStr = FormatAge((int)cInfo.Child.Age);

                            if (cInfo.IsOrphan) cName += $" — {ageStr}, в приюте";
                            else if (cInfo.IsDead) cName += $" — {ageStr}, погиб(ла)";
                            else cName += $" — {ageStr}";

                            sb.Append($"\n     - {cName}");
                        }
                    }
                    else
                    {
                        sb.Append("\n   Дети: нет");
                    }

                    counter++;
                }
            }
            sb.Append("\n--------------------------------------------------");
        }

        // =====================================================================
        // СЕКЦИЯ 2: ХРОНОЛОГИЧЕСКИЙ ЖУРНАЛ ПО ДАТАМ (СКРЫТ ПО УМОЛЧАНИЮ)
        // =====================================================================
        if (settings != null && settings.ShowChronologicalLog)
        {
            var heroHistory = PersistentHistoryTracker.GetHeroHistory(subject);
            var encounters = heroHistory != null ? heroHistory.Encounters : new List<StoredEncounter>();

            if (encounters.Count > 0)
            {
                var sortedEncounters = encounters.OrderByDescending(e => e.Days).ToList();
                int maxToShow = Math.Min(settings.MaxLogEntries, sortedEncounters.Count);

                sb.Append("\n\nЖУРНАЛ ПОСЛЕДНИХ ВСТРЕЧ:");
                for (int i = 0; i < maxToShow; i++)
                {
                    var enc = sortedEncounters[i];
                    string pName = enc.PartnerName;
                    Hero? p = Hero.Find(enc.PartnerStringId);
                    if (p != null) pName = p.Name.ToString();

                    string dateStr = PersistentHistoryTracker.FormatDate(enc.Days);
                    string tag = enc.Orientation switch
                    {
                        RelationshipOrientation.Gay => "ММ",
                        RelationshipOrientation.Lesbian => "ЖЖ",
                        _ => "МЖ"
                    };

                    string detail = enc.WasAffair ? "измена" : enc.EncounterType.ToLower();
                    sb.Append($"\n- {dateStr}: {pName} {tag} ({detail})");
                }
                sb.Append("\n--------------------------------------------------");
            }
        }

        // Защита: удаляем любые непреднамеренные символы '<' и '>', чтобы RichTextParser
        // движка Gauntlet никогда не переключался в режим разметки нераспознанных тегов,
        // который повреждает токенизацию и превращает последующие '\n' в символ '[?]'.
        // Также очищаем от '\r' для чистоты переносов.
        return sb.ToString().Replace("<", "").Replace(">", "").Replace("\r", "").TrimEnd();
    }
}
