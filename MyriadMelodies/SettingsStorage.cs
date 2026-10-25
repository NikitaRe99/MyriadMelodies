using System;
using System.IO;

namespace MyriadMelodies
{
    internal enum SettingsLocation { LocalAppData, ExecutableDirectory }

    internal sealed class SettingsStorage
    {
        internal const string PreferencesFile="overlay-settings.json";
        internal const string ThemesFile="custom-color-themes.json";
        internal const string PortableMarker="MyriadMelodies.portable";
        internal static SettingsStorage Default {get;}=new SettingsStorage(AppContext.BaseDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MyriadMelodies"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenCaptureTool"));

        private readonly string executableDirectory,localDirectory,legacyDirectory;
        public SettingsLocation Location {get;private set;}
        public string DirectoryPath=>Location==SettingsLocation.ExecutableDirectory?executableDirectory:localDirectory;

        public SettingsStorage(string executableDirectory,string localDirectory,string legacyDirectory=null)
        {
            this.executableDirectory=Path.GetFullPath(executableDirectory);
            this.localDirectory=Path.GetFullPath(localDirectory);
            this.legacyDirectory=legacyDirectory==null?null:Path.GetFullPath(legacyDirectory);
            Location=File.Exists(Path.Combine(this.executableDirectory,PortableMarker))
                ?SettingsLocation.ExecutableDirectory:SettingsLocation.LocalAppData;
        }
        public string GetWritePath(string name)=>Path.Combine(DirectoryPath,name);
        public string GetReadPath(string name)
        {
            string path=GetWritePath(name);
            if(Location==SettingsLocation.LocalAppData && !File.Exists(path) && legacyDirectory!=null)
            {
                string legacy=Path.Combine(legacyDirectory,name);
                if(File.Exists(legacy)) return legacy;
            }
            return path;
        }
        public void ChangeLocation(SettingsLocation location,OverlayPreferences preferences,Action<string> saveThemes)
        {
            if(location!=SettingsLocation.LocalAppData && location!=SettingsLocation.ExecutableDirectory)
                throw new ArgumentOutOfRangeException(nameof(location));
            if(location==Location)return;
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(saveThemes);
            string target=location==SettingsLocation.ExecutableDirectory?executableDirectory:localDirectory;
            // Copy the full current state before committing the location. Source files stay intact.
            new OverlaySettingsStore(Path.Combine(target,PreferencesFile)).Save(preferences);
            saveThemes(Path.Combine(target,ThemesFile));
            string marker=Path.Combine(executableDirectory,PortableMarker);
            if(location==SettingsLocation.ExecutableDirectory) AtomicSettingsFile.Write(marker,"portable\n");
            else File.Delete(marker);
            Location=location;
        }
    }

    internal static class AtomicSettingsFile
    {
        public static void Write(string path,string contents)
        {
            path=Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                File.WriteAllText(temporary,contents);
                File.Move(temporary,path,true);
            }
            finally
            {
                try {File.Delete(temporary);}
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
            }
        }
    }
}
