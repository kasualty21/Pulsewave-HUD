using System.IO;
using System.Text.Json;
using Pulsewave;
namespace Pulsewave.Regression;

internal static class PersistenceChecks
{
    public static void Run()
    {
        static void Check(bool ok,string message) {if(!ok) throw new Exception(message); Console.WriteLine("PASS: "+message);}
        var root=Path.Combine(Environment.GetEnvironmentVariable("PULSEWAVE_TEST_OUTPUT_DIR")??Path.Combine(Path.GetTempPath(),"PulsewaveRegression"),"persistence-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string Fixture(string name,string json) {var file=Path.Combine(root,name+".json"); File.WriteAllText(file,json); return file;}
        var defaults=SettingsStore.LoadFile(Path.Combine(root,"new-settings.json"));
        Check(defaults.Sensors.Count==12 && defaults.ActiveTheme=="" && defaults.ShowNowPlaying,"New isolated preferences retain shipped defaults");
        var legacy=Fixture("legacy","{\"MonitorInterfaceId\":\"saved-monitor\",\"PanelOpacityPercent\":55,\"Sensors\":{\"GpuFps\":{\"RegistryValue\":\"Value29\",\"Scale\":2,\"RegistryHive\":\"LocalMachine\"}},\"FutureFlag\":17}");
        var restored=SettingsStore.LoadFile(legacy);
        Check(restored.Sensors["GpuFps"].RegistryValue=="Value29" && restored.Sensors["GpuFps"].MatchMode=="Slot" && restored.Sensors["GpuFps"].Scale==2 && restored.Sensors["GpuFps"].RegistryHive=="LocalMachine" && restored.MonitorInterfaceId=="saved-monitor","Legacy-shaped settings migration retains FPS identity, hive, scale and placement");
        restored.StartHidden=true; SettingsStore.SaveFile(legacy,restored);
        Check(File.Exists(legacy+".bak") && File.ReadAllText(legacy+".bak").Contains("FutureFlag") && File.ReadAllText(legacy).Contains("FutureFlag"),"Atomic preference save retains previous valid file and unknown properties");
        var older=Fixture("removed-features","{\"Visuals\":null,\"VisualProfiles\":{\"Old\":{\"Visuals\":null},\"Empty\":null},\"HighContrastCards\":false}");
        var normalized=SettingsStore.LoadFile(older);
        Check(normalized.Sensors.Count==12 && normalized.ActiveTheme=="","Settings from versions with since-removed features (even null values) still load");
        foreach(var damage in new[]{"{\"Sensors\":", "null", "[]", "{\"Sensors\":{\"GpuFps\":null}}"})
        {
            var file=Fixture("damaged-"+Guid.NewGuid().ToString("N"),damage);
            bool loadFailed=false,saveBlocked=false;
            try {SettingsStore.LoadFile(file);} catch(InvalidDataException) {loadFailed=true;}
            try {SettingsStore.SaveFile(file,new MonitorSettings());} catch(InvalidDataException) {saveBlocked=true;}
            Check(loadFailed && saveBlocked && File.ReadAllText(file)==damage,"Damaged settings reject load/save without erasing the original");
        }
        var interrupted=Fixture("interrupted","{\"CpuName\":\"last committed\"}");
        File.WriteAllText(interrupted+".interrupted.tmp","{\"CpuName\":");
        Check(SettingsStore.LoadFile(interrupted).CpuName=="last committed","Abandoned partial staging file never replaces committed preferences");
        var locked=Fixture("locked","{\"CpuName\":\"keep original\"}");
        string before=File.ReadAllText(locked); bool failed=false;
        using(var held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            try {SettingsStore.SaveFile(locked,new MonitorSettings());} catch(IOException) {failed=true;}
        }
        Check(failed && File.ReadAllText(locked)==before && !Directory.GetFiles(root,"locked.json.*.tmp").Any(),"Failed atomic replacement preserves original and removes only its staging file");
        File.WriteAllText(legacy,"{");
        bool corruptProtected=false; try {SettingsStore.SaveFile(legacy,new MonitorSettings());} catch(InvalidDataException) {corruptProtected=true;}
        Check(corruptProtected && File.Exists(legacy+".bak"),"Corruption does not overwrite the last-good backup");
        File.Copy(legacy+".bak",legacy,true);
        Check(SettingsStore.LoadFile(legacy).MonitorInterfaceId=="saved-monitor","Explicit restoration from last-good backup recovers saved placement");
    }
}
