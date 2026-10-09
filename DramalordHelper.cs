using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace DramalordSecrets;

public static class DramalordHelper
{
    private static Type? _pregnanciesType;
    private static PropertyInfo? _pregnanciesInstanceProp;
    private static FieldInfo? _pregnanciesDictField;
    private static PropertyInfo? _fatherProp;

    private static Type? _relationsType;
    private static PropertyInfo? _relationsInstanceProp;
    private static MethodInfo? _getRelationMethod;
    private static MethodInfo? _getAllRelationsMethod;
    private static PropertyInfo? _relationshipProp;
    private static PropertyInfo? _loveProp;
    private static object? _loverEnumValue;
    private static object? _spouseEnumValue;

    private static Type? _eventsType;
    private static PropertyInfo? _eventsInstanceProp;
    private static FieldInfo? _eventsListField;

    private static Type? _orphansType;
    private static PropertyInfo? _orphansInstanceProp;
    private static FieldInfo? _orphansListField;

    private static FieldInfo? _vanillaPregnanciesField;
    private static FieldInfo? _vanillaMotherField;
    private static FieldInfo? _vanillaFatherField;

    static DramalordHelper()
    {
        try
        {
            // Vanilla PregnancyCampaignBehavior reflection
            Type? pcbType = typeof(PregnancyCampaignBehavior);
            _vanillaPregnanciesField = pcbType.GetField("_heroPregnancies", BindingFlags.NonPublic | BindingFlags.Instance);
            Type? pregType = pcbType.Assembly.GetType("TaleWorlds.CampaignSystem.CampaignBehaviors.PregnancyCampaignBehavior+Pregnancy");
            if (pregType != null)
            {
                _vanillaMotherField = pregType.GetField("Mother", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _vanillaFatherField = pregType.GetField("Father", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }

            // Dramalord assembly reflection
            Assembly? dramalordAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Dramalord");

            if (dramalordAsm == null)
            {
                try
                {
                    dramalordAsm = Assembly.Load("Dramalord");
                }
                catch
                {
                    // Ignore
                }
            }

            if (dramalordAsm != null)
            {
                _pregnanciesType = dramalordAsm.GetType("Dramalord.Data.DramalordPregnancies");
                if (_pregnanciesType != null)
                {
                    _pregnanciesInstanceProp = _pregnanciesType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _pregnanciesDictField = _pregnanciesType.GetField("_pregnancies", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                Type? heroPregnancyType = dramalordAsm.GetType("Dramalord.Data.HeroPregnancy");
                if (heroPregnancyType != null)
                {
                    _fatherProp = heroPregnancyType.GetProperty("Father", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                _relationsType = dramalordAsm.GetType("Dramalord.Data.DramalordRelations");
                if (_relationsType != null)
                {
                    _relationsInstanceProp = _relationsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _getRelationMethod = _relationsType.GetMethod("GetRelation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Hero), typeof(Hero) }, null);
                    _getAllRelationsMethod = _relationsType.GetMethod("GetAllRelations", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Hero) }, null);
                }

                Type? heroRelationType = dramalordAsm.GetType("Dramalord.Data.HeroRelation");
                if (heroRelationType != null)
                {
                    _relationshipProp = heroRelationType.GetProperty("Relationship", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _loveProp = heroRelationType.GetProperty("Love", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                Type? relEnumType = dramalordAsm.GetType("Dramalord.Data.RelationshipType");
                if (relEnumType != null)
                {
                    _loverEnumValue = Enum.Parse(relEnumType, "Lover");
                    _spouseEnumValue = Enum.Parse(relEnumType, "Spouse");
                }

                _eventsType = dramalordAsm.GetType("Dramalord.Data.DramalordEvents");
                if (_eventsType != null)
                {
                    _eventsInstanceProp = _eventsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _eventsListField = _eventsType.GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                _orphansType = dramalordAsm.GetType("Dramalord.Data.DramalordOrphans");
                if (_orphansType != null)
                {
                    _orphansInstanceProp = _orphansType.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    _orphansListField = _orphansType.GetField("_orphans", BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }
        }
        catch (Exception)
        {
            // Silently ignore static initialization errors
        }
    }

    public static Hero? GetPregnancyFather(Hero mother)
    {
        if (mother == null) return null;

        // 1. Try Dramalord _pregnancies dictionary
        try
        {
            if (_pregnanciesInstanceProp != null && _pregnanciesDictField != null)
            {
                object? inst = _pregnanciesInstanceProp.GetValue(null);
                if (inst != null)
                {
                    var dict = _pregnanciesDictField.GetValue(inst) as IDictionary;
                    if (dict != null)
                    {
                        foreach (DictionaryEntry entry in dict)
                        {
                            if (entry.Key is Hero h && (h == mother || h.StringId == mother.StringId))
                            {
                                if (entry.Value != null && _fatherProp != null)
                                {
                                    Hero? father = _fatherProp.GetValue(entry.Value) as Hero;
                                    if (father != null) return father;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Try Vanilla PregnancyCampaignBehavior._heroPregnancies
        try
        {
            var campaign = Campaign.Current;
            if (campaign != null && _vanillaPregnanciesField != null)
            {
                var pcb = campaign.GetCampaignBehavior<PregnancyCampaignBehavior>();
                if (pcb != null)
                {
                    var list = _vanillaPregnanciesField.GetValue(pcb) as IEnumerable;
                    if (list != null)
                    {
                        foreach (object item in list)
                        {
                            if (item != null)
                            {
                                Hero? m = _vanillaMotherField?.GetValue(item) as Hero;
                                if (m != null && (m == mother || m.StringId == mother.StringId))
                                {
                                    Hero? f = _vanillaFatherField?.GetValue(item) as Hero;
                                    if (f != null) return f;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 3. Try Dramalord ConceiveEvent
        try
        {
            var events = GetRecordedEvents();
            if (events != null)
            {
                foreach (object ev in events)
                {
                    if (ev != null && ev.GetType().Name == "ConceiveEvent")
                    {
                        Hero? actor = ev.GetType().GetProperty("Actor")?.GetValue(ev) as Hero;
                        Hero? target = ev.GetType().GetProperty("Target")?.GetValue(ev) as Hero;
                        if (actor != null && (actor == mother || actor.StringId == mother.StringId) && target != null)
                        {
                            return target;
                        }
                        if (target != null && (target == mother || target.StringId == mother.StringId) && actor != null)
                        {
                            return actor;
                        }
                    }
                }
            }
        }
        catch { }

        // 4. Try Dramalord latest SexEvent / PrisonSexEvent
        try
        {
            var events = GetRecordedEvents();
            if (events != null)
            {
                foreach (object ev in events)
                {
                    if (ev != null && (ev.GetType().Name == "SexEvent" || ev.GetType().Name == "PrisonSexEvent"))
                    {
                        Hero? actor = ev.GetType().GetProperty("Actor")?.GetValue(ev) as Hero;
                        Hero? target = ev.GetType().GetProperty("Target")?.GetValue(ev) as Hero;
                        if (actor != null && (actor == mother || actor.StringId == mother.StringId) && target != null && target != mother)
                        {
                            return target;
                        }
                        if (target != null && (target == mother || target.StringId == mother.StringId) && actor != null && actor != mother)
                        {
                            return actor;
                        }
                    }
                }
            }
        }
        catch { }

        // 5. If she is spouse of player (Native or Dramalord polygamy)
        if (mother.Spouse == Hero.MainHero || IsSpouseOf(mother, Hero.MainHero))
        {
            return Hero.MainHero;
        }

        // 6. Native spouse
        if (mother.Spouse != null && mother.Spouse != mother)
        {
            return mother.Spouse;
        }

        // 7. Dramalord spouse
        foreach (Hero other in Hero.AllAliveHeroes)
        {
            if (other != null && other != mother && IsSpouseOf(mother, other))
            {
                return other;
            }
        }

        // 8. Lover of MainHero
        if (IsLoverOf(mother, Hero.MainHero))
        {
            return Hero.MainHero;
        }

        // 9. Any lover in Dramalord
        foreach (Hero other in Hero.AllAliveHeroes)
        {
            if (other != null && other != mother && IsLoverOf(mother, other))
            {
                return other;
            }
        }

        // 10. Fallback for player's clan/party
        if (mother.Clan == Clan.PlayerClan || mother.PartyBelongedTo == MobileParty.MainParty)
        {
            return Hero.MainHero;
        }

        return null;
    }

    public static int GetLove(Hero hero, Hero other)
    {
        if (hero == null || other == null) return 0;
        try
        {
            if (_relationsInstanceProp != null && _getRelationMethod != null && _loveProp != null)
            {
                object? instance = _relationsInstanceProp.GetValue(null);
                if (instance != null)
                {
                    object? rel = _getRelationMethod.Invoke(instance, new object[] { hero, other });
                    if (rel != null)
                    {
                        object? val = _loveProp.GetValue(rel);
                        if (val != null) return Convert.ToInt32(val);
                    }
                }
            }
        }
        catch { }
        return 0;
    }

    public static bool IsLoverOf(Hero hero, Hero other)
    {
        if (hero == null || other == null || _loverEnumValue == null) return false;
        try
        {
            if (_relationsInstanceProp != null && _getRelationMethod != null && _relationshipProp != null)
            {
                object? instance = _relationsInstanceProp.GetValue(null);
                if (instance != null)
                {
                    object? rel = _getRelationMethod.Invoke(instance, new object[] { hero, other });
                    if (rel != null)
                    {
                        object? val = _relationshipProp.GetValue(rel);
                        if (val != null) return val.Equals(_loverEnumValue);
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static bool IsSpouseOf(Hero hero, Hero other)
    {
        if (hero == null || other == null) return false;
        if (hero.Spouse == other) return true;
        try
        {
            if (_relationsInstanceProp != null && _getRelationMethod != null && _relationshipProp != null)
            {
                object? instance = _relationsInstanceProp.GetValue(null);
                if (instance != null)
                {
                    object? rel = _getRelationMethod.Invoke(instance, new object[] { hero, other });
                    if (rel != null)
                    {
                        object? val = _relationshipProp.GetValue(rel);
                        if (val != null && _spouseEnumValue != null) return val.Equals(_spouseEnumValue);
                    }
                }
            }
        }
        catch { }
        return false;
    }

    public static void SetAsLovers(Hero hero1, Hero hero2)
    {
        if (hero1 == null || hero2 == null || _loverEnumValue == null) return;
        try
        {
            if (_relationsInstanceProp != null && _getRelationMethod != null && _relationshipProp != null)
            {
                object? instance = _relationsInstanceProp.GetValue(null);
                if (instance != null)
                {
                    object? rel1 = _getRelationMethod.Invoke(instance, new object[] { hero1, hero2 });
                    if (rel1 != null) _relationshipProp.SetValue(rel1, _loverEnumValue);

                    object? rel2 = _getRelationMethod.Invoke(instance, new object[] { hero2, hero1 });
                    if (rel2 != null) _relationshipProp.SetValue(rel2, _loverEnumValue);
                }
            }
        }
        catch { }
    }

    public static IDictionary? GetAllDramalordRelations(Hero hero)
    {
        if (hero == null) return null;
        try
        {
            if (_relationsInstanceProp != null && _getAllRelationsMethod != null)
            {
                object? instance = _relationsInstanceProp.GetValue(null);
                if (instance != null)
                {
                    return _getAllRelationsMethod.Invoke(instance, new object[] { hero }) as IDictionary;
                }
            }
        }
        catch { }
        return null;
    }

    public static IEnumerable? GetRecordedEvents()
    {
        try
        {
            if (_eventsInstanceProp != null && _eventsListField != null)
            {
                object? inst = _eventsInstanceProp.GetValue(null);
                if (inst != null)
                {
                    return _eventsListField.GetValue(inst) as IEnumerable;
                }
            }
        }
        catch { }
        return null;
    }

    public static List<Hero> GetOrphans()
    {
        var result = new List<Hero>();
        try
        {
            if (_orphansInstanceProp != null && _orphansListField != null)
            {
                object? inst = _orphansInstanceProp.GetValue(null);
                if (inst != null)
                {
                    var list = _orphansListField.GetValue(inst) as IEnumerable<Hero>;
                    if (list != null)
                    {
                        result.AddRange(list);
                    }
                }
            }
        }
        catch { }
        return result;
    }

    public static void ChangeTrust(Hero hero, Hero other, int delta)
    {
        if (hero == null || other == null || delta == 0) return;
        try
        {
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(other, hero, delta, true);
        }
        catch { }
    }
}
