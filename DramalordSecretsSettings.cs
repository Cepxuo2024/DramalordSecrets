using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace DramalordSecrets;

public class DramalordSecretsSettings : AttributeGlobalSettings<DramalordSecretsSettings>
{
    private static readonly string _assemblyName = typeof(DramalordSecretsSettings).Assembly.GetName().Name;
    private static readonly bool _isDev = _assemblyName.EndsWith("Dev", StringComparison.OrdinalIgnoreCase);

    public override string Id => _assemblyName;
    public override string DisplayName => _isDev ? "Dramalord Secrets Dev" : "Dramalord Secrets";
    public override string FolderName => _assemblyName;
    public override string FormatType => "json2";

    [SettingPropertyBool("Включить инспектор интимной истории (Cheat)", Order = 0, RequireRestart = false, HintText = "Главный переключатель. Отображает в описании энциклопедии (для мужчин и женщин) историю связей, партнёров и детей.")]
    [SettingPropertyGroup("Чит / Аналитика")]
    public bool ShowPartnerHistory { get; set; } = false;

    [SettingPropertyBool("Показывать список партнёров", Order = 1, RequireRestart = false, HintText = "Отображать компактный список всех партнёров: статус связи, количество встреч, последняя дата и общие дети с возрастом.")]
    [SettingPropertyGroup("Чит / Аналитика")]
    public bool ShowGroupedSummary { get; set; } = true;

    [SettingPropertyBool("Показывать журнал последних встреч", Order = 2, RequireRestart = false, HintText = "Отображать хронологический список последних интимных встреч по датам.")]
    [SettingPropertyGroup("Чит / Аналитика")]
    public bool ShowChronologicalLog { get; set; } = false;

    [SettingPropertyInteger("Максимум записей в журнале", 5, 50, "0 записей", Order = 3, RequireRestart = false, HintText = "Количество выводимых последних встреч в журнале.")]
    [SettingPropertyGroup("Чит / Аналитика")]
    public int MaxLogEntries { get; set; } = 15;
}
