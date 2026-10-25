using System;
using System.IO;
using System.Globalization;
using System.Text.Json;

namespace MyriadMelodies
{
    internal sealed class OverlayPreferences
    {
        public OverlayPreferences() {}
        public string MonitorDeviceName {get;set;}
        public string WindowProcessName {get;set;}
        public string WindowTitle {get;set;}
        public string ThemeName {get;set;}
        public string Language {get;set;}=DefaultLanguage;
        internal static string DefaultLanguage => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName=="ru"?"ru":"en";
        public int ModeIndex {get;set;}
        public int ColorTolerance {get;set;}=20;
        public int OpacityPercent {get;set;}=96;
        public bool TopMost {get;set;}=true;
        public bool SettingsExpanded {get;set;}
        public bool PreviewVisible {get;set;}
        public int[] ZoneKeys {get;set;}
        public int? WindowLeft {get;set;}
        public int? WindowTop {get;set;}
        public void Normalize()
        {
            ModeIndex=ModeIndex==1?1:0;
            Language=Language=="en" || Language=="ru"?Language:DefaultLanguage;
            ColorTolerance=Math.Clamp(ColorTolerance,10,70);
            OpacityPercent=Math.Clamp(OpacityPercent,75,100);
            ZoneKeys=LaneBindings.Normalize(ZoneKeys);
            if(WindowLeft.HasValue && Math.Abs((long)WindowLeft.Value)>100000) WindowLeft=null;
            if(WindowTop.HasValue && Math.Abs((long)WindowTop.Value)>100000) WindowTop=null;
        }
    }
    internal sealed class OverlaySettingsStore
    {
        private readonly string path;
        private readonly SettingsStorage storage;
        public bool CanChangeLocation=>storage!=null;
        public SettingsLocation Location=>storage?.Location??SettingsLocation.LocalAppData;
        public string DirectoryPath=>storage?.DirectoryPath??Path.GetDirectoryName(Path.GetFullPath(path));
        public OverlaySettingsStore(string path=null)
        {
            this.path=path;
            if(path==null)storage=SettingsStorage.Default;
        }
        internal OverlaySettingsStore(SettingsStorage storage){this.storage=storage??throw new ArgumentNullException(nameof(storage));}
        public OverlayPreferences Load()
        {
            try
            {
                string readPath=path??storage.GetReadPath(SettingsStorage.PreferencesFile);
                var value=File.Exists(readPath)?JsonSerializer.Deserialize<OverlayPreferences>(File.ReadAllText(readPath)):null;
                value ??=new OverlayPreferences();
                if(path==null && !string.Equals(readPath,storage.GetWritePath(SettingsStorage.PreferencesFile),StringComparison.OrdinalIgnoreCase))
                {value.SettingsExpanded=false;value.PreviewVisible=false;}
                value.Normalize();return value;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {return new OverlayPreferences();}
        }
        public void Save(OverlayPreferences settings)
        {
            settings.Normalize();
            AtomicSettingsFile.Write(path??storage.GetWritePath(SettingsStorage.PreferencesFile),
                JsonSerializer.Serialize(settings,new JsonSerializerOptions{WriteIndented=true}));
        }
        public void ChangeLocation(SettingsLocation location,OverlayPreferences preferences,Action<string> saveThemes)
        {
            if(storage==null)throw new InvalidOperationException("Settings location is fixed for this store.");
            storage.ChangeLocation(location,preferences,saveThemes);
        }
    }
}
