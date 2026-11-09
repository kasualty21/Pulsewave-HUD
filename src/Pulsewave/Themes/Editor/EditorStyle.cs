using System.Windows;
using System.Windows.Markup;

namespace Pulsewave.Themes.Editor;

/// <summary>
/// The theme editor's Halo look (the same palette as the tray windows' DesktopStyle): rounded slate buttons,
/// dark rounded fields and drop-downs with a sky-blue focus edge, and dark list items. Applied as the window's
/// implicit styles, so controls the editor creates pick it up without per-control code.
/// </summary>
internal static class EditorStyle
{
    public const string Base = "#FF0E1424", Card = "#FF161D30", Field = "#FF0B101C", Button = "#FF263048", ButtonHover = "#FF34405E",
        Text = "#FFF5F7FA", Secondary = "#FFC9D2DE", Sky = "#FF7FD8FF", Edge = "#FF343E56";

    public static ResourceDictionary Resources() => (ResourceDictionary)XamlReader.Parse($$"""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button">
            <Setter Property="Foreground" Value="{{Text}}"/>
            <Setter Property="Background" Value="{{Button}}"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="Button">
                  <Border x:Name="B" Background="{TemplateBinding Background}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="{{ButtonHover}}"/></Trigger>
                    <Trigger Property="IsEnabled" Value="False"><Setter TargetName="B" Property="Opacity" Value="0.5"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Foreground" Value="{{Text}}"/>
            <Setter Property="CaretBrush" Value="{{Text}}"/>
            <Setter Property="Background" Value="{{Field}}"/>
            <Setter Property="BorderBrush" Value="{{Edge}}"/>
            <Setter Property="Padding" Value="6,4"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                  <Border x:Name="B" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="8">
                    <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="B" Property="BorderBrush" Value="{{Sky}}"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style x:Key="ThumbStyle" TargetType="Thumb">
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="Thumb"><Border Background="#40FFFFFF" CornerRadius="4" Margin="2"/></ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Width" Value="10"/>
            <Setter Property="MinWidth" Value="10"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="ScrollBar">
                  <Track x:Name="PART_Track" IsDirectionReversed="True">
                    <Track.Thumb><Thumb Style="{StaticResource ThumbStyle}"/></Track.Thumb>
                  </Track>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
            <Style.Triggers>
              <Trigger Property="Orientation" Value="Horizontal">
                <Setter Property="Width" Value="Auto"/>
                <Setter Property="MinWidth" Value="0"/>
                <Setter Property="Height" Value="10"/>
                <Setter Property="Template">
                  <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                      <Track x:Name="PART_Track"><Track.Thumb><Thumb Style="{StaticResource ThumbStyle}"/></Track.Thumb></Track>
                    </ControlTemplate>
                  </Setter.Value>
                </Setter>
              </Trigger>
            </Style.Triggers>
          </Style>
          <Style TargetType="CheckBox"><Setter Property="Foreground" Value="{{Text}}"/></Style>
          <Style TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="{{Text}}"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="ComboBoxItem">
                  <Border x:Name="B" Background="Transparent" CornerRadius="6" Padding="8,4" Margin="3,1">
                    <ContentPresenter/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="B" Property="Background" Value="{{Button}}"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter Property="Foreground" Value="{{Sky}}"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style TargetType="ComboBox">
            <Setter Property="Foreground" Value="{{Text}}"/>
            <Setter Property="MaxDropDownHeight" Value="420"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="ComboBox">
                  <Grid>
                    <ToggleButton Focusable="False" ClickMode="Press" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent} }">
                      <ToggleButton.Template>
                        <ControlTemplate TargetType="ToggleButton">
                          <Border x:Name="B" Background="{{Field}}" BorderBrush="{{Edge}}" BorderThickness="1" CornerRadius="8">
                            <TextBlock Text="&#xE70D;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="10" Foreground="{{Secondary}}"
                                       HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,0"/>
                          </Border>
                          <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="BorderBrush" Value="{{Sky}}"/></Trigger>
                          </ControlTemplate.Triggers>
                        </ControlTemplate>
                      </ToggleButton.Template>
                    </ToggleButton>
                    <ContentPresenter x:Name="Shown" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}"
                                      ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="10,5,28,5" VerticalAlignment="Center"/>
                    <TextBox x:Name="PART_EditableTextBox" Visibility="Hidden" Background="Transparent" BorderThickness="0" Margin="4,0,24,0"
                             VerticalAlignment="Center" IsReadOnly="{TemplateBinding IsReadOnly}"/>
                    <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
                      <Border Background="{{Card}}" BorderBrush="{{Edge}}" BorderThickness="1" CornerRadius="8" Padding="0,4"
                              MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent} }" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                        <ScrollViewer><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer>
                      </Border>
                    </Popup>
                  </Grid>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsEditable" Value="True">
                      <Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible"/>
                      <Setter TargetName="Shown" Property="Visibility" Value="Hidden"/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
          <Style TargetType="ListBoxItem">
            <Setter Property="Foreground" Value="{{Text}}"/>
            <Setter Property="Template">
              <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                  <Border x:Name="B" Background="Transparent" CornerRadius="6" Padding="8,4" Margin="4,1">
                    <ContentPresenter/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="B" Property="Background" Value="#1FFFFFFF"/></Trigger>
                    <Trigger Property="IsSelected" Value="True"><Setter TargetName="B" Property="Background" Value="{{Button}}"/></Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </Setter.Value>
            </Setter>
          </Style>
        </ResourceDictionary>
        """);
}

/// <summary>The data picker's sections: every reading a theme can show, grouped by what it is about.</summary>
public static class ThemeDataGroups
{
    public static readonly IReadOnlyList<string> Order =
        ["CPU", "GPU", "Performance limits", "Frame rate", "Memory", "Drives", "Cooling", "Network", "Clock", "Weather", "Media", "Volume", "Timer & stopwatch", "Gaming", "AI usage", "Shortcuts", "Sensor feed"];

    public static string Of(string name) => name switch
    {
        "CPU_LIMIT" or "GPU_LIMIT" => "Performance limits",
        _ when name.StartsWith("CPU", StringComparison.Ordinal) => "CPU",
        _ when name.StartsWith("GPU", StringComparison.Ordinal) => "GPU",
        _ when name.StartsWith("FPS", StringComparison.Ordinal) => "Frame rate",
        _ when name.StartsWith("RAM", StringComparison.Ordinal) => "Memory",
        "DRVLOAD" or "HDDTEMP" or "HDDUSED" or "DRIVE_FREE" or "DRIVE_USED_PCT" => "Drives",
        "WATERPUMP" or "CASEFAN1" or "CASEFAN2" => "Cooling",
        "DOWNDSPEED" or "UPSPEED" => "Network",
        "TIME" or "DATE" or "DAY" or "APM" => "Clock",
        _ when name.StartsWith("WEATHER", StringComparison.Ordinal) => "Weather",
        _ when name.StartsWith("MEDIA", StringComparison.Ordinal) => "Media",
        _ when name.StartsWith("VOLUME", StringComparison.Ordinal) => "Volume",
        "TIMER" or "STOPWATCH" or "TIMER_ACTION" or "STOPWATCH_ACTION" or "TIMER_PHASE" => "Timer & stopwatch",
        _ when name.StartsWith("FOCUS_", StringComparison.Ordinal) => "Timer & stopwatch",
        _ when name.StartsWith("GAME", StringComparison.Ordinal) => "Gaming",
        _ when name.StartsWith("CLAUDE", StringComparison.Ordinal) || name.StartsWith("CODEX", StringComparison.Ordinal) => "AI usage",
        "SENSOR_STATUS" => "Sensor feed",
        _ => "Shortcuts"
    };

    /// <summary>The catalog in sections (in <see cref="Order"/>), keeping only readings whose label or name contains <paramref name="search"/>.</summary>
    public static IReadOnlyList<(string Group, IReadOnlyList<(string Name, string Label)> Items)> Grouped(string? search = null)
    {
        string s = search?.Trim() ?? "";
        return ThemeData.Catalog
            .Where(c => s.Length == 0 || c.Label.Contains(s, StringComparison.OrdinalIgnoreCase) || c.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                        || Of(c.Name).Contains(s, StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => Of(c.Name))
            .OrderBy(g => Order.ToList().IndexOf(g.Key))
            .Select(g => (g.Key, (IReadOnlyList<(string, string)>)g.ToList()))
            .ToList();
    }
}

/// <summary>Starting points the editor offers when warning colors are turned on for a reading.</summary>
public static class ThemeEditorDefaults
{
    /// <summary>Warning and critical levels for a reading: hot temperatures, nearly full memory and limits, a slow 1% low.</summary>
    public static (double Warn, double Critical) Thresholds(string? data) => data switch
    {
        // Gaming loads are normal: a 265K runs into the 90s and an RTX 5070 Ti in the 70s–80s, NVMe drives throttle near 70–80 °C.
        "CPUTEMP" or "GAME_PEAK_CPU" => (90, 98),
        "GPUTEMP" or "GAME_PEAK_GPU" => (83, 90),
        "HDDTEMP" => (70, 80),
        _ when data?.StartsWith("CLAUDE", StringComparison.Ordinal) == true || data?.StartsWith("CODEX", StringComparison.Ordinal) == true => (75, 90),
        "GAME_FPS_LOW_PCT" => (60, 40),
        _ => (85, 95)
    };
}
