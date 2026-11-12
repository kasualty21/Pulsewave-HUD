using Pulsewave;
using System.Reflection;
using System.Text.Json;

if(args.Contains("--refresh-templates"))
{
    // Writes the feature-refreshed copies of built-in templates 01-25 to a folder (see Themes/TemplateRefresh.cs).
    string outDir=args[Array.IndexOf(args,"--refresh-templates")+1];
    System.IO.Directory.CreateDirectory(outDir);
    foreach(var name in Pulsewave.Themes.BuiltInTemplates.LegacyNames.Where(n => int.TryParse(n[..2],out int k) && k<=25))
        System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,name+".json"),Pulsewave.Regression.TemplateRefresh.Apply(Pulsewave.Themes.BuiltInTemplates.Read(name)));
    return;
}
if(args.Contains("--template-shots"))
{
    // Renders every built-in template with sample values and lists quality-gate problems.
    string shots=args[Array.IndexOf(args,"--template-shots")+1];
    System.IO.Directory.CreateDirectory(shots);
    var shotRunner=new Thread(() => {
        foreach(var name in Pulsewave.Themes.BuiltInTemplates.Names.Concat(Pulsewave.Themes.BuiltInTemplates.LegacyNames))
        {
            string dir=Pulsewave.Themes.BuiltInTemplates.Ensure(Pulsewave.Themes.BuiltInTemplates.Prefix+name,System.IO.Path.Combine(shots,"folders"),legacy:true)!;
            var problems=Pulsewave.Regression.TemplateChecks.Problems(Pulsewave.Themes.ThemeStore.Load(dir),dir,System.IO.Path.Combine(shots,name+".png"));
            if(args.Contains("--stress")) problems.AddRange(Pulsewave.Regression.TemplateChecks.StressProblems(Pulsewave.Themes.ThemeStore.Load(dir),dir));
            Console.WriteLine($"{name}: {(problems.Count==0 ? "OK" : string.Join("; ",problems))}");
        }
    });
    shotRunner.SetApartmentState(ApartmentState.STA); shotRunner.Start(); shotRunner.Join();
    return;
}
if(args.Contains("--detail-shots"))
{
    // Draws the details page at three screen shapes (off-screen, sample telemetry) to PNGs.
    string shots=args[Array.IndexOf(args,"--detail-shots")+1];
    Pulsewave.Regression.TouchChecks.Run(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-detail-shots"),shots);
    Pulsewave.Regression.LauncherChecks.Run(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-detail-shots"),shots);
    return;
}
if(args.Contains("--game-probe"))
{
    // Read-only: builds the game catalog from Steam and this PC's shortcuts, and says which game is running now.
    var gameSettings=Pulsewave.SettingsStore.LoadFile(Pulsewave.SettingsStore.FilePath);
    var watch=System.Diagnostics.Stopwatch.StartNew();
    var games=Pulsewave.GameCatalog.Build(gameSettings.Shortcuts, gameSettings.Gaming);
    Console.WriteLine($"catalog {games.Count} games in {watch.ElapsedMilliseconds} ms");
    foreach(var source in games.GroupBy(g=>g.Source)) Console.WriteLine($"  {source.Key}: {source.Count()}");
    foreach(var g in games.Where(g=>g.Source!="Steam")) Console.WriteLine($"  [{g.Source}] {g.Name} | folder {(g.Folder.Length>0?g.Folder:"(program only)")} | {System.IO.Path.GetFileName(g.Exe ?? "")}");
    watch.Restart();
    var running=Pulsewave.GameDetector.Running(games);
    Console.WriteLine($"detect in {watch.ElapsedMilliseconds} ms: " + (running is { } r ? $"{r.Game.Name} ({r.Program})" : "no game running"));
    return;
}
if(args.Contains("--shortcut-shot"))
{
    // Read-only: renders a template with this PC's own shortcuts (icons and Steam covers) and sample readings to a PNG.
    string shot=args[Array.IndexOf(args,"--shortcut-shot")+1], template=args[Array.IndexOf(args,"--shortcut-shot")+2];
    Exception? shotFailure=null;
    var shotThread=new Thread(() => { try {
        var settings=Pulsewave.SettingsStore.LoadFile(Pulsewave.SettingsStore.FilePath);
        var views=settings.Shortcuts.Select(Pulsewave.Themes.ShortcutView.From).ToList();
        string dir=Pulsewave.Themes.BuiltInTemplates.Ensure(Pulsewave.Themes.BuiltInTemplates.Prefix+template, System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-shortcut-shot"),legacy:true)!;
        var theme=Pulsewave.Themes.ThemeStore.Load(dir);
        var view=new Pulsewave.Themes.ThemeView(theme,dir,playVideo:false);
        var data=Pulsewave.Themes.ThemeSampleData.Context();
        // The gaming card shows the sample session with the first Steam shortcut's own cover.
        var steamTile=views.FirstOrDefault(v=>v?.Cover!=null);
        var gaming=data.Gaming! with { Game=steamTile?.Name ?? data.Gaming!.Game, Tile=steamTile };
        view.Update(new Pulsewave.Themes.ThemeDataContext{Sensors=data.Sensors,Now=data.Now,Media=data.Media,Memory=data.Memory,Shortcuts=views,Volume=data.Volume,MediaPosition=data.MediaPosition,Weather=data.Weather,CpuName=data.CpuName,GpuName=data.GpuName,Gaming=gaming});
        view.Measure(new System.Windows.Size(theme.Width,theme.Height)); view.Arrange(new System.Windows.Rect(0,0,theme.Width,theme.Height)); view.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(theme.Width,theme.Height,96,96,System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(view);
        var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output=System.IO.File.Create(shot); png.Save(output);
        foreach(var (v,i) in views.Select((v,i)=>(v,i)).Take(4)) Console.WriteLine($"slot {i+1}: {v?.Name} icon {(v?.Icon as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth} solid {v?.IconSolid} cover {v?.Cover!=null}");
    } catch(Exception ex){ shotFailure=ex; } });
    shotThread.SetApartmentState(ApartmentState.STA); shotThread.Start(); shotThread.Join();
    if(shotFailure!=null) throw shotFailure;
    return;
}
if(args.Contains("--glyphs"))
{
    // Draws candidate Segoe Fluent Icons glyphs with their codes, to pick icons by eye.
    string file=args[Array.IndexOf(args,"--glyphs")+1];
    var codes=args.Skip(Array.IndexOf(args,"--glyphs")+2).Select(s=>Convert.ToInt32(s,16)).ToArray();
    var glyphThread=new Thread(() => {
        var panel=new System.Windows.Controls.WrapPanel{Width=1200,Background=System.Windows.Media.Brushes.Black};
        foreach(int code in codes){
            var cell=new System.Windows.Controls.StackPanel{Width=140,Margin=new System.Windows.Thickness(4)};
            cell.Children.Add(new System.Windows.Controls.TextBlock{Text=char.ConvertFromUtf32(code),FontFamily=new System.Windows.Media.FontFamily("Segoe Fluent Icons"),FontSize=48,Foreground=System.Windows.Media.Brushes.White,HorizontalAlignment=System.Windows.HorizontalAlignment.Center});
            cell.Children.Add(new System.Windows.Controls.TextBlock{Text=code.ToString("X4"),FontSize=16,Foreground=System.Windows.Media.Brushes.Gray,HorizontalAlignment=System.Windows.HorizontalAlignment.Center});
            panel.Children.Add(cell);
        }
        panel.Measure(new System.Windows.Size(1200,double.PositiveInfinity)); panel.Arrange(new System.Windows.Rect(panel.DesiredSize)); panel.UpdateLayout();
        var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap((int)panel.ActualWidth,(int)panel.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32); bmp.Render(panel);
        Pulsewave.Themes.ThemeMedia.SavePng(bmp,file);
    });
    glyphThread.SetApartmentState(ApartmentState.STA); glyphThread.Start(); glyphThread.Join();
    return;
}
if(args.Contains("--form-shots"))
{
    // Draws every tray window (app-owned, off-screen, sample data, no-op callbacks; nothing saved) to PNGs.
    string folder=args[Array.IndexOf(args,"--form-shots")+1];
    System.IO.Directory.CreateDirectory(folder);
    Exception? formFailure=null;
    var formThread=new Thread(() => { try {
        var settings=new MonitorSettings{ThemePlaylist=["builtin:28-halo"]};
        settings.Shortcuts=[new AppShortcut{Name="Notepad",Target=@"C:\Windows\notepad.exe"},new AppShortcut{Name="Pulsewave site",Target="https://example.com"}];
        settings.Gaming.Games.Add(new UserGame{Name="RetroArch",Exe=@"C:\RetroArch-Win64\retroarch.exe"});settings.Gaming.Theme="builtin:30-halo-gaming";settings.Gaming.Themes["gog:2"]="builtin:38-noir";
        var telemetry=new TelemetryEngine();
        string themes=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-form-shots-themes");
        var games=new List<Pulsewave.GameEntry>{new("steam:1","Stellar Blade",@"C:\Games\StellarBlade","1"),new("gog:2","SILENT HILL f",@"E:\GOG Games\SILENT HILL f",null,null,null,"GOG")};
        var forms=new (string Name, System.Windows.Forms.Form Form)[]
        {
            ("themes", new ThemesForm(settings, themes, _ => { }, displaySize: () => (682, 2560))),
            ("playlist", new PlaylistForm(settings, Pulsewave.Themes.ThemeCycle.Available(themes), _ => [], displaySize: () => (682, 2560))),
            ("shortcuts", new ShortcutsForm(settings.Shortcuts, settings.ShortcutLists, (_, _) => null)),
            ("hotkeys", new HotkeysForm(HotkeyBindings.Normalize(null, settings).Append(new HotkeyBinding{Keys="Ctrl+Alt+M",Action="lightsToggle"}).Append(new HotkeyBinding{Keys="F13",Action="launch:1"}),
                [("builtin:30-halo-gaming","Halo Gaming")], settings.Shortcuts, _ => [])),
            ("shortcuts-gaming", new ShortcutsForm([..settings.Shortcuts, new AppShortcut{Name="Steam",Target="steam://open/main"}, new AppShortcut{Name="Discord mute",Kind=AppShortcut.KeysKind,Target="Ctrl+Shift+M"}],
                new Dictionary<string, List<int>>{["Gaming"]=[3,4]}, (_, _) => null, ShortcutLists.Gaming)),
            ("lights", new LightsForm(new LightsSettings(), _ => { }, () => { }, () => null)),
            ("schedule", new ScheduleForm([new ScheduleRule{Screen=true,Theme="builtin:36-paper"},new ScheduleRule{Off="09:00",On="17:00",Lights=false,Days=62,Theme="builtin:32-halo-productivity"}], _ => null, "", themes: Pulsewave.Themes.ThemeCycle.Available(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-form-shots-themes")).Select(x => (x.Id, x.Name)).ToList())),
            ("weather", new WeatherForm(new WeatherSettings{City="San Francisco",Latitude=37.8,Longitude=-122.4}, () => null)),
            ("gaming", new GamingForm(settings.Gaming, () => games, () => [@"D:\Emu\emu.exe"], () => null, (_, _) => { }, _ => { }, Pulsewave.Themes.ThemeCycle.Available(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-form-shots-themes")).Select(x => (x.Id, x.Name)).ToList())),
            ("plex-token", new PlexTokenForm(_ => { })),
            ("sensor-manager", new SensorManagerForm(settings)),
            ("telemetry", new TelemetryForm(telemetry, settings, () => { })),
            ("telemetry-settings", new TelemetrySettingsForm(settings)),
        };
        foreach(var (name, form) in forms)
        {
            form.StartPosition=System.Windows.Forms.FormStartPosition.Manual; form.Left=-20000; form.Top=-20000; form.ShowInTaskbar=false;
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            using var bitmap=new System.Drawing.Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
            bitmap.Save(System.IO.Path.Combine(folder, name + ".png"));
            form.Close();
            Console.WriteLine("shot " + name);
        }
    } catch(Exception ex){ formFailure=ex; } });
    formThread.SetApartmentState(ApartmentState.STA); formThread.Start(); formThread.Join();
    if(formFailure!=null) throw formFailure;
    return;
}
if(args.Contains("--mono-mockups"))
{
    // Renders the monochrome templates (Tests/Themes/MonoTemplates.cs) with sample data and runs the quality gate.
    string monoFolder=args[Array.IndexOf(args,"--mono-mockups")+1];
    Exception? monoFailure=null;
    var monoThread=new Thread(() => { try { Pulsewave.Regression.MonoTemplates.Run(monoFolder); } catch(Exception ex){ monoFailure=ex; } });
    monoThread.SetApartmentState(ApartmentState.STA); monoThread.Start(); monoThread.Join();
    if(monoFailure!=null) throw monoFailure;
    return;
}
if(args.Contains("--style-mockups"))
{
    // Renders the style directions (Tests/Themes/StyleMockups.cs) with sample data and runs the quality gate.
    string folder=args[Array.IndexOf(args,"--style-mockups")+1];
    Exception? mockFailure=null;
    var mockThread=new Thread(() => { try { Pulsewave.Regression.StyleMockups.Run(folder); } catch(Exception ex){ mockFailure=ex; } });
    mockThread.SetApartmentState(ApartmentState.STA); mockThread.Start(); mockThread.Join();
    if(mockFailure!=null) throw mockFailure;
    return;
}
if(args.Contains("--plexamp-live"))
{
    // Live, user-approved: each control is sent to Plexamp, checked through its timeline, and put back.
    using var reader=new Pulsewave.PlexampReader{Token=Pulsewave.PlexTokenStore.Load()};
    async Task<Pulsewave.PlexampTimeline?> Poll(){ await Task.Delay(1500); return await reader.PollAsync(CancellationToken.None); }
    var start=await reader.PollAsync(CancellationToken.None);
    if(start is null){Console.WriteLine("Plexamp not playing");return;}
    Console.WriteLine($"start time {start.TimeMs} volume {start.Volume} shuffle {start.Shuffle} repeat {start.Repeat} item {start.PlayQueueItemId}");
    bool ok=await reader.ControlAsync(Pulsewave.PlexampControl.Seek,start.TimeMs+15000); var t=await Poll();
    Console.WriteLine($"seek +15s sent {ok}: time {t?.TimeMs} (expected about {start.TimeMs+16500})");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Seek,start.TimeMs); t=await Poll(); Console.WriteLine($"seek back sent {ok}: time {t?.TimeMs}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Volume,(start.Volume??0)+2); t=await Poll(); Console.WriteLine($"volume +2 sent {ok}: {t?.Volume}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Volume,start.Volume??0); t=await Poll(); Console.WriteLine($"volume back sent {ok}: {t?.Volume}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Repeat,((start.Repeat??0)+1)%3); t=await Poll(); Console.WriteLine($"repeat change sent {ok}: {t?.Repeat}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Repeat,start.Repeat??0); t=await Poll(); Console.WriteLine($"repeat back sent {ok}: {t?.Repeat}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Shuffle,start.Shuffle==true?0:1); t=await Poll(); Console.WriteLine($"shuffle toggle sent {ok}: {t?.Shuffle}");
    ok=await reader.ControlAsync(Pulsewave.PlexampControl.Shuffle,start.Shuffle==true?1:0); t=await Poll(); Console.WriteLine($"shuffle back sent {ok}: {t?.Shuffle} item {t?.PlayQueueItemId}");
    var queue=await reader.QueueAsync(t??start);
    int current=queue?.ToList().FindIndex(q=>q.Current) ?? -1;
    if(queue is not null && current>=0 && current+1<queue.Count)
    {
        var position=(t??start).TimeMs;
        ok=await reader.ControlAsync(Pulsewave.PlexampControl.SkipTo,long.Parse(queue[current+1].ItemId)); t=await Poll();
        Console.WriteLine($"skip to next item {queue[current+1].ItemId} sent {ok}: now {t?.PlayQueueItemId}");
        ok=await reader.ControlAsync(Pulsewave.PlexampControl.SkipTo,long.Parse(queue[current].ItemId)); t=await Poll();
        Console.WriteLine($"skip back to {queue[current].ItemId} sent {ok}: now {t?.PlayQueueItemId}");
        ok=await reader.ControlAsync(Pulsewave.PlexampControl.Seek,position+3000); t=await Poll(); Console.WriteLine($"seek to where it was sent {ok}: time {t?.TimeMs}");
    }
    else Console.WriteLine("queue skip test skipped: queue " + (queue is null ? "unreadable" : $"current {current} of {queue.Count}"));
    var end=await reader.PollAsync(CancellationToken.None);
    Console.WriteLine($"end volume {end?.Volume} shuffle {end?.Shuffle} repeat {end?.Repeat} item {end?.PlayQueueItemId} state {end?.State}");
    return;
}
if(args.Contains("--plexamp-probe"))
{
    // Read-only: Plexamp's local timeline and its play queue (the stored token goes only to the Plex server Plexamp names).
    using var reader=new Pulsewave.PlexampReader{Token=Pulsewave.PlexTokenStore.Load()};
    var timeline=await reader.PollAsync(CancellationToken.None);
    if(timeline is null){Console.WriteLine("Plexamp not playing or not answering");return;}
    Console.WriteLine($"state {timeline.State} time {timeline.TimeMs} / {timeline.DurationMs} volume {timeline.Volume} shuffle {timeline.Shuffle} repeat {timeline.Repeat} queue {timeline.PlayQueueId} item {timeline.PlayQueueItemId}");
    Console.WriteLine("controllable " + string.Join(",", timeline.Controllable.Order()));
    Console.WriteLine("token " + (reader.Token is null ? "missing" : "present"));
    var queue=await reader.QueueAsync(timeline);
    if(queue is null){Console.WriteLine("queue unreadable");return;}
    int current=queue.ToList().FindIndex(q=>q.Current);
    Console.WriteLine($"queue {queue.Count} items, current at {current}");
    foreach(var item in queue.Skip(Math.Max(0,current-1)).Take(4)) Console.WriteLine($"  {(item.Current?">":" ")} {item.ItemId} {item.Title} · {item.Artist} {item.DurationMs/1000}s");
    return;
}
if(args.Contains("--weather-probe"))
{
    // Asks Open-Meteo for a city by name and its forecast; sends only the given name and the found coordinates.
    string city=args[Array.IndexOf(args,"--weather-probe")+1];
    var found=await Pulsewave.WeatherClient.FindAsync(city);
    foreach(var place in found.Take(3)) Console.WriteLine($"place {place.Label} {place.Latitude},{place.Longitude}");
    if(found.Count>0)
    {
        var report=await Pulsewave.WeatherClient.ForecastAsync(new Pulsewave.WeatherSettings{City=found[0].Name,Latitude=found[0].Latitude,Longitude=found[0].Longitude},DateTime.Now);
        Console.WriteLine($"now {report.TempC}°C feels {report.FeelsC} {Pulsewave.WeatherClient.Describe(report.Code)} day {report.IsDay} high {report.HighC} low {report.LowC} sunrise {report.Sunrise:HH:mm} sunset {report.Sunset:HH:mm}");
        foreach(var day in report.Days) Console.WriteLine($"  {day.Date:ddd dd} {Pulsewave.WeatherClient.Describe(day.Code)} {day.HighC}/{day.LowC}");
    }
    return;
}
if(args.Contains("--app-volume-probe"))
{
    // Read-only: lists the apps with sound on the default playback device and their levels. Changes nothing.
    foreach(var apartment in new[]{ApartmentState.STA,ApartmentState.MTA})
    {
        var probe=new Thread(() => {
            Console.WriteLine($"[{apartment}] device " + Pulsewave.AudioVolume.Read());
            foreach(var app in Pulsewave.AppVolume.Read()) Console.WriteLine($"{app.Name} | {app.Level}% | muted {app.Muted} | playing {app.Playing} | {app.Key}");
            if(Pulsewave.AppVolume.LastError is { } audioError) Console.WriteLine("last error: " + audioError);
        });
        probe.SetApartmentState(apartment); probe.Start(); probe.Join();
    }
    return;
}
if(args.Contains("--widget-shots"))
{
    // Draws every editor widget on a wide and a vertical screen (off-screen, sample values) to PNGs.
    string shots=args[Array.IndexOf(args,"--widget-shots")+1];
    if(args.Contains("--lights")){Pulsewave.Regression.LightsChecks.Run(System.IO.Path.GetTempPath(),shots);return;}
    if(args.Contains("--volume")){Pulsewave.Regression.MixerChecks.Run(System.IO.Path.GetTempPath(),shots);return;}
    if(args.Contains("--gaming")){Pulsewave.Regression.GamingChecks.Run(System.IO.Path.GetTempPath(),shots);return;}
    if(args.Contains("--palettes")){Pulsewave.Regression.PaletteChecks.Run(System.IO.Path.GetTempPath(),shots);return;}
    if(args.Contains("--write-oneui-templates")){string outDir=args[Array.IndexOf(args,"--write-oneui-templates")+1];foreach(var (file,theme) in new[]{("26-one-ui",Pulsewave.Regression.OneUiTemplates.Bar()),("27-one-ui-column",Pulsewave.Regression.OneUiTemplates.Column()),("28-halo",Pulsewave.Regression.HaloTemplates.Bar()),("29-halo-column",Pulsewave.Regression.HaloTemplates.Column()),("30-halo-gaming",Pulsewave.Regression.HaloTemplates.GamingBar()),("31-halo-gaming-column",Pulsewave.Regression.HaloTemplates.GamingColumn()),("32-halo-productivity",Pulsewave.Regression.HaloTemplates.WorkBar()),("33-halo-productivity-column",Pulsewave.Regression.HaloTemplates.WorkColumn())}.Concat(Pulsewave.Regression.MonoTemplates.All()).Concat(new[]{("40-lumen",Pulsewave.Regression.LumenTemplates.Bar()),("41-lumen-column",Pulsewave.Regression.LumenTemplates.Column()),("42-lumen-gaming",Pulsewave.Regression.LumenTemplates.GamingBar()),("43-lumen-gaming-column",Pulsewave.Regression.LumenTemplates.GamingColumn()),("44-lumen-productivity",Pulsewave.Regression.LumenTemplates.WorkBar()),("45-lumen-productivity-column",Pulsewave.Regression.LumenTemplates.WorkColumn())})){string dir=System.IO.Path.Combine(outDir,file);System.IO.Directory.CreateDirectory(dir);Pulsewave.Themes.ThemeStore.Save(dir,theme);System.IO.File.Copy(System.IO.Path.Combine(dir,"theme.json"),System.IO.Path.Combine(outDir,file+".json"),true);}return;}
    if(args.Contains("--media")){Pulsewave.Regression.MediaChecks.Run(System.IO.Path.GetTempPath(),shots);return;}
    System.IO.Directory.CreateDirectory(shots);
    Pulsewave.Regression.GroupChecks.Run(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-widget-shots"),shots);
    return;
}
if(args.Contains("--display-power-live"))
{
    // Live (user-approved): turns one display off over DDC/CI, waits, and turns it back on.
    int at=Array.IndexOf(args,"--display-power-live");
    string device=@"\\.\"+args[at+1]; uint mode=uint.Parse(args[at+2]); int seconds=int.Parse(args[at+3]);
    string State()=>string.Join("; ",Pulsewave.DisplayPower.Probe().Where(d=>d.DeviceName==device).Select(d=>d.Note))+" | screens: "+string.Join(", ",System.Windows.Forms.Screen.AllScreens.Select(s=>s.DeviceName));
    Console.WriteLine($"{DateTime.Now:HH:mm:ss.f} before: {State()}");
    Console.WriteLine($"{DateTime.Now:HH:mm:ss.f} set mode {mode}: {Pulsewave.DisplayPower.Set(device,mode)}");
    for(int i=0;i<seconds;i+=2){System.Threading.Thread.Sleep(2000);Console.WriteLine($"{DateTime.Now:HH:mm:ss.f} off +{i+2}s: {State()}");}
    var watch=System.Diagnostics.Stopwatch.StartNew();
    bool back=Pulsewave.DisplayPower.TurnOn(device);
    Console.WriteLine($"{DateTime.Now:HH:mm:ss.f} TurnOn: {back} after {watch.Elapsed.TotalSeconds:0.0}s; {State()}");
    return;
}
if(args.Contains("--display-power-probe"))
{
    // Read-only: each display's DDC/CI power-mode support. Changes nothing.
    foreach(var d in Pulsewave.DisplayPower.Probe()) Console.WriteLine($"{d.DeviceName}  {d.Description}: {d.Note}");
    foreach(var s in System.Windows.Forms.Screen.AllScreens) Console.WriteLine($"  screen {s.DeviceName} {s.Bounds.Width}x{s.Bounds.Height} primary={s.Primary}");
    return;
}
if(args.Contains("--schedule-shot"))
{
    // Draws the Schedule window with sample rules to a PNG without showing it (no settings touched).
    string file=args[Array.IndexOf(args,"--schedule-shot")+1];
    using var form=new Pulsewave.ScheduleForm([new Pulsewave.ScheduleRule{Screen=true},new Pulsewave.ScheduleRule{Off="12:30",On="13:15",Lights=false,Screen=true,Days=0b0111110}],_=>null,"");
    form.StartPosition=System.Windows.Forms.FormStartPosition.Manual; form.Location=new System.Drawing.Point(-5000,-5000);
    form.ShowInTaskbar=false; form.Opacity=0; form.Show(); System.Windows.Forms.Application.DoEvents(); form.Refresh();
    using var bmp=new System.Drawing.Bitmap(form.Width,form.Height);
    form.DrawToBitmap(bmp,new System.Drawing.Rectangle(0,0,form.Width,form.Height));
    bmp.Save(file,System.Drawing.Imaging.ImageFormat.Png);
    return;
}
if(args.Contains("--openrgb-live"))
{
    // Live: changes the real lights step by step (user-approved), reading back after each, then loads a profile.
    string address=args[Array.IndexOf(args,"--openrgb-live")+1], profile=args[Array.IndexOf(args,"--openrgb-live")+2];
    var s=new Pulsewave.LightsSettings{Address=address,Color="#00E5FF",Brightness=100,Speed=50};
    void Step(string label, Pulsewave.LightsKind kind, string value="")
    {
        using(var client=Pulsewave.OpenRgbClient.Connect(s.Address,s.Port))
        {
            var (ok,status)=Pulsewave.Lights.Apply(client,s,new Pulsewave.LightsCommand(kind,value));
            if(ok) Pulsewave.Lights.Remember(s,new Pulsewave.LightsCommand(kind,value));
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {label}: {(ok?"OK":"NOT OK")} - {status}");
        }
        System.Threading.Thread.Sleep(600);
        using(var client=Pulsewave.OpenRgbClient.Connect(s.Address,s.Port))
            foreach(var d in client.Devices())
            {
                var m=d.Active; var c=d.Colors.Count>0?d.Colors[0]:default; var mc=m is {Colors.Count:>0}?m.Colors[0]:default;
                Console.WriteLine($"    {d.Name}: mode '{m?.Name}', brightness {m?.Brightness}, speed {m?.Speed}, LED0 #{c.R:X2}{c.G:X2}{c.B:X2}, mode color {(m is {Colors.Count:>0}?$"#{mc.R:X2}{mc.G:X2}{mc.B:X2}":"-")}");
            }
        System.Threading.Thread.Sleep(3000);
    }
    Step("Color cyan",Pulsewave.LightsKind.Color);
    s.Brightness=30; Step("Brightness 30%",Pulsewave.LightsKind.Brightness);
    s.Brightness=100; Step("Brightness 100%",Pulsewave.LightsKind.Brightness);
    s.Color="#FF20C0"; Step("Effect Breathing (pink)",Pulsewave.LightsKind.Effect,"Breathing");
    Step("Off",Pulsewave.LightsKind.Off);
    Step("On (brings back Breathing)",Pulsewave.LightsKind.On);
    Step($"Profile '{profile}'",Pulsewave.LightsKind.Profile,profile);
    return;
}
if(args.Contains("--openrgb-probe"))
{
    // Read-only: lists what OpenRGB reports at both loopback addresses. Sends no lighting commands.
    foreach(var address in Pulsewave.OpenRgbClient.Addresses)
    {
        try
        {
            using var client=Pulsewave.OpenRgbClient.Connect(address,Pulsewave.OpenRgbClient.DefaultPort,TimeSpan.FromSeconds(4));
            var devices=client.Devices();
            Console.WriteLine($"{address}: protocol {client.Protocol}, {devices.Count} devices, profiles: {string.Join(", ",client.Profiles())}");
            foreach(var d in devices)
            {
                Console.WriteLine($"  {d.Name} ({d.TypeName}), {d.Colors.Count} LEDs, active '{d.Active?.Name}'");
                foreach(var m in d.Modes)
                    Console.WriteLine($"    {m.Name}: flags 0x{m.Flags:X}, speed {m.SpeedMin}-{m.SpeedMax} ({m.Speed}), brightness {m.BrightnessMin}-{m.BrightnessMax} ({m.Brightness}), colors {m.ColorsMin}-{m.ColorsMax} [{m.Colors.Count}], color mode {m.ColorMode}");
            }
        }
        catch(Exception ex){Console.WriteLine($"{address}: {ex.GetType().Name}: {ex.Message}");}
    }
    return;
}
if(args.Contains("--y70-probe"))
{
    // Read-only look at this PC: installed Steam games (with icon source) and the playback volume.
    foreach(var g in Pulsewave.SteamLibrary.Installed()) Console.WriteLine($"steam {g.AppId} {g.Name} -> {System.IO.Path.GetFileName(g.Icon)}");
    Console.WriteLine("volume " + Pulsewave.AudioVolume.Read());
    return;
}
if(args.Contains("--playlist-shot"))
{
    // Draws the playlist window (app-owned, off-screen, nothing saved) to a PNG.
    string shot=args[Array.IndexOf(args,"--playlist-shot")+1];
    // An optional filter after the file name ("vertical", "horizontal", "fits") is shown as if saved; the display is 1920x480.
    string shotFilter=args.Length>Array.IndexOf(args,"--playlist-shot")+2 ? args[Array.IndexOf(args,"--playlist-shot")+2] : "";
    var probeSettings=new MonitorSettings { ThemePlaylist=["builtin:09-vinyl","builtin:11-synthwave"], PlaylistEnabled=true, ThemeGalleryFilter=shotFilter };
    var form=new PlaylistForm(probeSettings, Pulsewave.Themes.ThemeCycle.Available(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"pw-no-themes")), _ => [],
        displaySize: () => (1920, 480))
        { StartPosition=System.Windows.Forms.FormStartPosition.Manual, Left=-20000, Top=-20000, ShowInTaskbar=false };
    form.Show();
    using(var bitmap=new System.Drawing.Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new System.Drawing.Rectangle(0,0,form.Width,form.Height)); bitmap.Save(shot); }
    form.Close();
    return;
}if(args.Contains("--editor-shot"))
{
    // Opens the editor on a theme folder in an app-owned window placed off-screen and saves a screenshot.
    string themeFolder=args[Array.IndexOf(args,"--editor-shot")+1];
    var shotThread=new Thread(() => {
        var editor=new Pulsewave.Themes.Editor.ThemeEditorWindow(themeFolder,new MonitorSettings(),null,null)
            { Left=-20000, Top=-20000, ShowActivated=false, ShowInTaskbar=false, WindowStartupLocation=System.Windows.WindowStartupLocation.Manual };
        editor.Show();
        int at=Array.IndexOf(args,"--editor-shot");
        if(Environment.GetEnvironmentVariable("PW_EDITOR_FOLD") is { Length: >0 } fold) foreach(var s in fold.Split('|')) ((HashSet<string>)typeof(Pulsewave.Themes.Editor.ThemeEditorWindow).GetField("Folded",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!).Add(s);
        if(args.Length>at+2 && int.TryParse(args[at+2],out int pick)) typeof(Pulsewave.Themes.Editor.ThemeEditorWindow).GetMethod("Select",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,[typeof(int)])!.Invoke(editor,[pick]);
        string shotName=args.Length>at+3 ? args[at+3] : "pulsewave-editor-shot.png";
        var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        timer.Tick+=(_,_)=>{
            timer.Stop();
            var content=(System.Windows.FrameworkElement)editor.Content;
            var shot=new System.Windows.Media.Imaging.RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            shot.Render(content);
            string path=System.IO.Path.IsPathRooted(shotName) ? shotName : System.IO.Path.Combine(System.IO.Path.GetTempPath(),shotName);
            Pulsewave.Themes.ThemeMedia.SavePng(shot,path);
            Console.WriteLine("editor screenshot: "+path);
            editor.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
        };
        timer.Start();
        System.Windows.Threading.Dispatcher.Run();
    });
    shotThread.SetApartmentState(ApartmentState.STA); shotThread.Start(); shotThread.Join();
    return;
}
if(args.Contains("--theme-perf"))
{
    // Plays an imported theme in an app-owned window placed off-screen (never the real
    // overlay) and measures process CPU, once as a transparent layered window like the
    // overlay and once as a normal window.
    string source=args[Array.IndexOf(args,"--theme-perf")+1];
    var perfRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Pulsewave-theme-perf",Guid.NewGuid().ToString("N"));
    var imported=Pulsewave.Themes.LConnectImporter.Import(source,perfRoot);
    var themeDir=System.IO.Path.Combine(perfRoot,imported.ThemeId);
    foreach(bool layered in new[]{true,false})
    {
        string report="";
        var ui=new Thread(() => {
            var window=new System.Windows.Window { WindowStyle=System.Windows.WindowStyle.None, AllowsTransparency=layered,
                Background=System.Windows.Media.Brushes.Transparent, ShowInTaskbar=false, ShowActivated=false,
                Left=-30000, Top=-30000, Width=1920, Height=480 };
            var view=new Pulsewave.Themes.ThemeView(imported.Theme,themeDir);
            window.Content=new System.Windows.Controls.Viewbox { Child=view };
            int framesAtStart=0;
            var sample=Pulsewave.Regression.ThemeSamples.Context();
            var tick=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
            tick.Tick+=(_,_)=>view.Update(sample);
            var process=System.Diagnostics.Process.GetCurrentProcess();
            TimeSpan cpuStart=default; var clock=new System.Diagnostics.Stopwatch();
            var warm=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
            warm.Tick+=(_,_)=>{ warm.Stop(); process.Refresh(); cpuStart=process.TotalProcessorTime; clock.Start(); framesAtStart=view.Video?.FramesShown??0;
                var stop=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(10) };
                stop.Tick+=(_,_)=>{ stop.Stop(); process.Refresh();
                    double cpu=(process.TotalProcessorTime-cpuStart).TotalMilliseconds/clock.Elapsed.TotalMilliseconds/Environment.ProcessorCount*100;
                    report=$"{(layered?"transparent layered":"normal window")}: {(view.VideoError is {} err ? "video FAILED: "+err : $"{((view.Video?.FramesShown??0)-framesAtStart)/clock.Elapsed.TotalSeconds:0.0} frames/s shown")}, CPU {cpu:0.0}% of all cores ({cpu*Environment.ProcessorCount:0}% of one core)";
                    if(!layered)
                    {
                        var shot=new System.Windows.Media.Imaging.RenderTargetBitmap(1920,480,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                        shot.Render(view);
                        var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(shot));
                        string path=System.IO.Path.Combine(themeDir,"perf-frame.png");
                        using(var f=System.IO.File.Create(path)) png.Save(f);
                        report+=Environment.NewLine+"  live frame: "+path;
                    }
                    view.Dispose(); window.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); };
                stop.Start(); };
            window.Show(); tick.Start(); warm.Start();
            System.Windows.Threading.Dispatcher.Run();
        });
        ui.SetApartmentState(ApartmentState.STA); ui.Start(); ui.Join();
        Console.WriteLine(report);
    }
    return;
}
if(args.Contains("--theme-probe"))
{
    // Imports L-Connect theme files into a throwaway folder and checks the result with
    // Windows' own media stack: container properties and an actually decoded frame.
    int at=Array.IndexOf(args,"--theme-probe");
    var outRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Pulsewave-theme-probe",Guid.NewGuid().ToString("N"));
    foreach(var source in args.Skip(at+1).TakeWhile(a=>!a.StartsWith("--")))
    {
        try
        {
            var result=Pulsewave.Themes.LConnectImporter.Import(source,outRoot);
            var dir=System.IO.Path.Combine(outRoot,result.ThemeId);
            var t=result.Theme;
            var kinds=string.Join(", ",t.Elements.GroupBy(e=>e.Kind).Select(g=>$"{g.Count()} {g.Key}"));
            Console.WriteLine($"OK  {System.IO.Path.GetFileName(source)} -> '{t.Name}' {t.Width}x{t.Height}: {kinds}; preview={t.Preview!=null} bg={t.BackgroundImage!=null} video={t.BackgroundVideo!=null}");
            foreach(var note in result.Notes) Console.WriteLine("    note: "+note);
            var fonts=t.Elements.Where(e=>e.Font!=null).Select(e=>e.Font!.Family).Distinct();
            Console.WriteLine("    fonts: "+string.Join(", ",fonts.Select(f=>f+(Pulsewave.Themes.ThemeFonts.Resolve(f).Found?"":" (MISSING)"))));
            if(t.BackgroundVideo!=null)
            {
                var file=Windows.Storage.StorageFile.GetFileFromPathAsync(System.IO.Path.Combine(dir,t.BackgroundVideo)).AsTask().GetAwaiter().GetResult();
                var props=file.Properties.GetVideoPropertiesAsync().AsTask().GetAwaiter().GetResult();
                var clip=Windows.Media.Editing.MediaClip.CreateFromFileAsync(file).AsTask().GetAwaiter().GetResult();
                var composition=new Windows.Media.Editing.MediaComposition(); composition.Clips.Add(clip);
                using var frame=composition.GetThumbnailAsync(TimeSpan.FromSeconds(0.5),(int)props.Width/2,(int)props.Height/2,Windows.Media.Editing.VideoFramePrecision.NearestFrame).AsTask().GetAwaiter().GetResult();
                using(var saved=System.IO.File.Create(System.IO.Path.Combine(dir,"probe-frame.jpg"))) System.IO.WindowsRuntimeStreamExtensions.AsStreamForRead(frame).CopyTo(saved);
                Console.WriteLine($"    video: {props.Width}x{props.Height}, {props.Duration.TotalSeconds:0.00} s, decoded frame {frame.Size} bytes (probe-frame.jpg)");
            }
            Exception? renderFailure=null;
            var render=new Thread(() => {
                try
                {
                    var view=new Pulsewave.Themes.ThemeView(t,dir,playVideo:false);
                    view.Update(Pulsewave.Regression.ThemeSamples.Context());
                    view.Measure(new System.Windows.Size(t.Width,t.Height)); view.Arrange(new System.Windows.Rect(0,0,t.Width,t.Height)); view.UpdateLayout();
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(t.Width,t.Height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(view);
                    var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using var file=System.IO.File.Create(System.IO.Path.Combine(dir,"probe-render.png")); png.Save(file);
                    if(view.MissingFonts.Count>0) Console.WriteLine("    render fell back for: "+string.Join(", ",view.MissingFonts));
                } catch(Exception ex) { renderFailure=ex; }
            });
            render.SetApartmentState(ApartmentState.STA); render.Start(); render.Join();
            Console.WriteLine(renderFailure==null ? "    rendered probe-render.png" : "    RENDER FAIL: "+renderFailure.Message);
            Console.WriteLine("    folder: "+dir);
        }
        catch(Exception ex) { Console.WriteLine($"FAIL {System.IO.Path.GetFileName(source)}: {ex.GetType().Name}: {ex.Message}"); }
    }
    return;
}
if(args.Contains("--media-probe"))
{
    // Live read of the current Windows media session; prints metadata only.
    using var monitor=new NowPlayingMonitor { PlexToken=PlexTokenStore.Load() }; // the user's saved token, if any
    monitor.Start();
    var deadline=DateTime.UtcNow.AddSeconds(4);
    while(monitor.Current.State==MediaPlayback.None && DateTime.UtcNow<deadline) Thread.Sleep(200);
    Thread.Sleep(1200); // allow late artwork
    var media=monitor.Current;
    var manager=Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask().GetAwaiter().GetResult();
    var sessions=manager.GetSessions().Select(s => new {Source=s.SourceAppUserModelId,Status=s.GetPlaybackInfo()?.PlaybackStatus.ToString()}).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new {Chosen=new {media.State,media.Title,media.Artist,media.Source,Art=media.Art is null ? "none" : $"{media.Art.PixelWidth}x{media.Art.PixelHeight}"},AllSessions=sessions},new JsonSerializerOptions {WriteIndented=true}));
    return;
}
static void Check(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS: " + label);
}

var sourceRoot = Environment.GetEnvironmentVariable("PULSEWAVE_TEST_SOURCE_ROOT") ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../.."));
var testOutput = Environment.GetEnvironmentVariable("PULSEWAVE_TEST_OUTPUT_DIR") ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Pulsewave-tests",Guid.NewGuid().ToString("N"));
System.IO.Directory.CreateDirectory(testOutput);
Console.WriteLine("Isolated test artifacts: " + testOutput);

// Sensor identity: FPS follows its slot, other readings follow their names.
var settings = new MonitorSettings();
var fps = settings.Sensors["GpuFps"];
fps.SensorGroup = "Old game";
fps.SensorLabel = "Old FPS title";
SensorCatalogEntry Entry(int index, string group, string label, string hive = "CurrentUser") =>
    new() { Index = index, Group = group, Label = label, Hive = hive, Raw = "144" };
var catalog = new[] { Entry(19, "New game", "Different FPS title"), Entry(20, "Old game", "Old FPS title"), Entry(19, "Other hive", "FPS", "LocalMachine") };
Check(SensorCatalog.Resolve(fps, settings.RegistryHive, catalog, out _) == catalog[0], "FPS keeps slot 19 after game title changes, in the correct hive");
Check(SensorCatalog.Resolve(fps, settings.RegistryHive, new[] { catalog[1] }, out _) == null, "Missing FPS slot never uses a similarly named source");
var named = new SensorMapping { RegistryValue = "ValueRaw2", SensorGroup = "CPU", SensorLabel = "Temperature", MatchMode = "Name" };
var moved = Entry(100, "CPU", "Temperature");
Check(SensorCatalog.Resolve(named, "CurrentUser", new[] { moved }, out _) == moved, "Named HWiNFO sensor survives slot renumbering");
Check(SensorCatalog.Resolve(named, "CurrentUser", new[] { moved, Entry(101, "CPU", "Temperature") }, out _) == null, "Ambiguous named sensor is not silently guessed");

// Session statistics and graphs.
var sample = new SensorSnapshot();
sample.Values["CpuLoad"] = 20;
var telemetry = new TelemetryEngine();
var now = DateTime.Now;
telemetry.Ingest(now, sample, settings);
sample.Values["CpuLoad"] = null;
telemetry.Ingest(now.AddSeconds(1), sample, settings);
sample.Values["CpuLoad"] = 60;
telemetry.Ingest(now.AddSeconds(2), sample, settings);
Check(telemetry.Metrics["CpuLoad"].Samples == 2 && telemetry.Metrics["CpuLoad"].Average == 40, "Missing samples do not corrupt session statistics");
Check(telemetry.History("CpuLoad", now.AddSeconds(2), 90).Any(p => p.Value == null), "Missing graph periods are retained as gaps");
var boundedTelemetry=new TelemetryEngine();
var boundedSnapshot=new SensorSnapshot(); boundedSnapshot.Values["CpuLoad"]=42;
for(int i=0;i<20000;i++) boundedTelemetry.Ingest(now.AddMilliseconds(i*250),boundedSnapshot,new MonitorSettings());
Check(boundedTelemetry.RetainedRecordCount==14400 && boundedTelemetry.SamplesWereDiscarded && boundedTelemetry.Metrics["CpuLoad"].Samples==20000,"Long-session retention stays bounded while full-session statistics continue");

// Settings round-trips.
settings.MonitorInterfaceId = "test-monitor";
var restored = JsonSerializer.Deserialize<MonitorSettings>(JsonSerializer.Serialize(settings))!;
Check(restored.MonitorInterfaceId == "test-monitor" && restored.Sensors["GpuFps"].RegistryValue == "Value19", "Display settings round-trip without changing sensor mappings");
var legacy = JsonSerializer.Deserialize<MonitorSettings>("{\"MonitorDeviceName\":\"old-display\"}")!;
Check(!legacy.StartHidden && legacy.ShowNowPlaying && legacy.ActiveTheme == "" && legacy.ClickThrough, "Older settings get the current defaults");
var older=JsonSerializer.Deserialize<MonitorSettings>("{\"FuturePreference\":17,\"PanelOpacityPercent\":55,\"Visuals\":{\"Theme\":\"Ember\"},\"DemoMode\":true}")!;
string savedJson=JsonSerializer.Serialize(older);
Check(savedJson.Contains("\"FuturePreference\":17") && savedJson.Contains("\"PanelOpacityPercent\":55") && savedJson.Contains("\"Theme\":\"Ember\""),
    "Settings for removed or unknown features are kept untouched, so going back to 0.8.0 loses nothing");

// Exercise real Windows struct marshalling, read-only. Never write startup or user preferences.
var identity = typeof(MonitorSettings).Assembly.GetType("Pulsewave.DisplayIdentity")!.GetMethod("For", BindingFlags.Public | BindingFlags.Static)!;
foreach (var screen in System.Windows.Forms.Screen.AllScreens)
{
    var id = (string)identity.Invoke(null, new object[] { screen })!;
    Check(id.Length > 0, "Windows monitor interface lookup: " + screen.DeviceName);
}

// The window's real XAML: since 0.14 it holds only the theme, the pages and the covers (Classic was removed).
Exception? renderError=null;
var renderThread=new Thread(() => {
    try {
        var project=System.IO.Path.Combine(sourceRoot,"Pulsewave");
        var document=System.Xml.Linq.XDocument.Load(System.IO.Path.Combine(project,"MainWindow.xaml"));
        document.Root!.Attribute(System.Xml.Linq.XName.Get("Class","http://schemas.microsoft.com/winfx/2006/xaml"))!.Remove();
        // The pack:// icon resolves against the app assembly, not this test host; it is not layout.
        document.Root!.Attribute("Icon")?.Remove();
        var window=(System.Windows.Window)System.Windows.Markup.XamlReader.Parse(document.ToString());
        Check(window.Title=="Lumen HUD" && window.FindName("ThemeHost") is System.Windows.Controls.Viewbox && window.FindName("DetailHost") is System.Windows.Controls.Viewbox &&
              window.FindName("ClassicView")==null && window.FindName("BlankCover")!=null,
            "The window is titled Lumen HUD and holds the theme, the pages and the screen covers; the Classic layout is gone");
        window.Close();
    } catch(Exception ex) {renderError=ex;}
});
renderThread.SetApartmentState(ApartmentState.STA); renderThread.Start(); renderThread.Join();
if(renderError!=null) throw renderError;

Pulsewave.Regression.SensorCacheChecks.Run();
Pulsewave.Regression.NowPlayingChecks.Run();
Pulsewave.Regression.PlexampChecks.Run();
Pulsewave.Regression.ThemeChecks.Run(testOutput);
Pulsewave.Regression.EditorChecks.Run(testOutput);
Pulsewave.Regression.TemplateChecks.Run(testOutput);
Pulsewave.Regression.PlaylistChecks.Run(testOutput);
Pulsewave.Regression.TouchChecks.Run(testOutput);
Pulsewave.Regression.LauncherChecks.Run(testOutput);
Pulsewave.Regression.Y70Checks.Run(testOutput);
Pulsewave.Regression.GroupChecks.Run(testOutput);
Pulsewave.Regression.LightsChecks.Run(testOutput);
Pulsewave.Regression.ScheduleChecks.Run(testOutput);
Pulsewave.Regression.PairingChecks.Run();
Pulsewave.Regression.ThemeDirectorChecks.Run(testOutput);
Pulsewave.Regression.AestheticChecks.Run(testOutput);
Pulsewave.Regression.MixerChecks.Run(testOutput);
Pulsewave.Regression.WeatherChecks.Run(testOutput);
Pulsewave.Regression.PaletteChecks.Run(testOutput);
Pulsewave.Regression.MediaChecks.Run(testOutput);
Pulsewave.Regression.GamingChecks.Run(testOutput);
Pulsewave.Regression.PersistenceChecks.Run();
Console.WriteLine("All regression checks passed. No user settings or startup entries changed.");
