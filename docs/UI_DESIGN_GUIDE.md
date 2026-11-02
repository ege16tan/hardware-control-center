# UI- und Theme-Leitfaden

Gilt für alle Änderungen an der WPF-Oberfläche (`src/HCC.App`). Ziel: eine Oberfläche,
zwei Themes (Dunkel/Hell), keine hardcodierten Farben in Views, keine kaputten
Kontraste. Dieses Dokument ist die Arbeitsanweisung für Menschen und KI-Agenten.

## 1. Architektur

| Datei | Aufgabe |
|---|---|
| `src/HCC.App/Themes/DarkTheme.xaml` | Farbtokens für das dunkle Theme |
| `src/HCC.App/Themes/LightTheme.xaml` | Farbtokens für das helle Theme (gleiche Keys!) |
| `src/HCC.App/Themes/Controls.xaml` | Wiederverwendbare Styles/Templates |
| `src/HCC.App/Theming/ThemeManager.cs` | Tauscht das Theme-Dictionary zur Laufzeit |
| `src/HCC.Infrastructure/Configuration/ThemeSettings.cs` | Persistiert `theme.json`, validiert Tokens |
| `src/HCC.App/ViewModels/SettingsViewModel.cs` | Theme-Auswahl im UI, zeigt Speicherfehler |

Startablauf:

1. `App.xaml` merged `DarkTheme.xaml` und `Controls.xaml`.
2. `App.OnStartup` lädt `ThemeSettings.Load()` und ruft `ThemeManager.Apply(theme)` auf.
3. `SettingsViewModel.SelectedTheme` speichert über `ThemeSettings.TrySetTheme(...)`
   und wendet das Theme sofort über `ThemeManager.Apply(...)` an.

Gespeichert wird unter `%LOCALAPPDATA%\HardwareControlCenter\theme.json`.
Interne Tokens sind `Dark` und `Light`; die UI-Labels sind `Dunkel` und `Hell`.
Labels niemals persistieren.

## 2. Farbtoken-Vertrag

Jedes Theme muss **alle** folgenden Keys als `SolidColorBrush` definieren.
`Controls.xaml` und alle Views referenzieren sie.

| Key | Rolle |
|---|---|
| `AppBackgroundBrush` | Fensterhintergrund |
| `SidebarBrush` | Navigationsleiste |
| `CardBrush` | Karten/Panel-Fläche |
| `CardBorderBrush` | Kartenrahmen, Trennlinien, Input-Rahmen |
| `MutedTextBrush` | Sekundärtext, Labels, Hinweise |
| `TextPrimaryBrush` | Haupttext |
| `TextSecondaryBrush` | Navigation, weniger wichtiger Text |
| `AccentBrush` | Akzent (Logo, Fokus, Primäraktion) |
| `AccentForegroundBrush` | Text auf `AccentBrush` |
| `SurfaceAltBrush` | Chips, sekundäre Buttons, Hinweisflächen |
| `InputBackgroundBrush` | TextBox/ComboBox-Fläche |
| `InputForegroundBrush` | Text in Eingabefeldern |
| `HoverBrush` | Hover-Zustand |
| `SelectionBrush` | Auswahl/gedrückt |
| `TrackBrush` | Fortschrittsbalken-Hintergrund |
| `DangerSurfaceBrush` | Alarmfläche |
| `DangerBorderBrush` | Alarmrahmen |
| `DangerTextBrush` | Alarmtext |
| `WarningTextBrush` | Warnhinweise (z. B. gesperrte Steuerung) |
| `ChartCpuBrush`, `ChartGpuBrush`, `ChartMemoryBrush` | Kennzahlen/Diagrammfarben |
| `ChartBackgroundBrush` | Diagrammfläche (beide Themes dunkel) |

Regeln:

- **Farben immer** mit `{DynamicResource ...Brush}` referenzieren, damit der
  Laufzeitwechsel bereits geladene Controls erreicht.
- **Styles** mit `{StaticResource ...}` referenzieren. Die Styles selbst enthalten
  `DynamicResource`-Farben.
- Keine Hex-Literale (`#RRGGBB`), keine `Colors.*`/`Brushes.*` in XAML-Views.
- Status niemals nur über Farbe transportieren: immer Text und/oder Symbol
  (z. B. `StatusDisplay`, `CapabilitySymbol`).

## 3. Wiederverwendbare Styles (`Controls.xaml`)

| Key | Verwendung |
|---|---|
| `CardStyle` | Oberste Fläche einer Seite, `Border` mit Hintergrund/Rahmen/Radius/Padding |
| `SectionHeaderStyle` | Abschnittsüberschrift (`TextBlock`) |
| `NavigationButtonStyle` | Nur für Sidebar-Navigation |
| `PrimaryButtonStyle` | Eine klare Hauptaktion pro Kontext |
| `SecondaryButtonStyle` | Alle normalen Aktionen (Standardwahl) |
| `AppTextBoxStyle` | Jedes `TextBox` |
| `AppComboBoxStyle` | Jedes `ComboBox` (inkl. Dropdown-Template) |

Style-Overrides sind lokal erlaubt (z. B. `Margin`), aber keine neuen Farben.
Fehlt ein Style: zuerst prüfen, ob `SecondaryButtonStyle`/`CardStyle` reicht.
Neue Styles gehören nach `Controls.xaml`, nicht in eine View.

## 4. Rezept: neue Seite

1. ViewModel unter `src/HCC.App/ViewModels/` anlegen (ObservableObject).
2. View unter `src/HCC.App/Views/` anlegen. Grundgerüst:

```xml
<UserControl x:Class="HCC.App.Views.ExampleView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Border Style="{StaticResource CardStyle}">
        <DockPanel>
            <StackPanel DockPanel.Dock="Top" Margin="0,0,0,16">
                <TextBlock Text="Titel" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="{Binding Status}" Foreground="{DynamicResource MutedTextBrush}"
                           TextWrapping="Wrap" Margin="0,8,0,0" />
            </StackPanel>
            <ScrollViewer VerticalScrollBarVisibility="Auto">
                <StackPanel>
                    <TextBlock Text="Abschnitt" Style="{StaticResource SectionHeaderStyle}" />
                    <TextBox Text="{Binding Name, UpdateSourceTrigger=PropertyChanged}"
                             Style="{StaticResource AppTextBoxStyle}" />
                    <Button Content="Aktion" Command="{Binding DoCommand}"
                            Style="{StaticResource SecondaryButtonStyle}"
                            HorizontalAlignment="Left" Margin="0,10,0,0" />
                </StackPanel>
            </ScrollViewer>
        </DockPanel>
    </Border>
</UserControl>
```

3. `DataTemplate` in `MainWindow.xaml` registrieren.
4. Navigation: Command + Titel + Untertitel in `MainWindowViewModel.NavigateTo`
   ergänzen, Button in der Sidebar mit `NavigationButtonStyle`.
5. ViewModel in `App.xaml.cs` als Singleton registrieren, falls es Konstruktor-
   Abhängigkeiten hat (`MainWindowViewModel` bekommt es injiziert).

## 5. Rezept: neues Theme

1. `src/HCC.App/Themes/<Name>Theme.xaml` mit **allen** Keys aus Abschnitt 2 anlegen.
   Dateiname muss auf `Theme.xaml` enden; `ThemeManager` erkennt das Dictionary daran.
2. Token in `ThemeSettings` ergänzen (`const string ...`, `IsValidTheme`,
   Normalisierung in `TrySetTheme`/`Load`).
3. Anzeige-Label in `SettingsViewModel.AvailableThemes` und Mapping im
   `SelectedTheme`-Setter ergänzen.
4. Mapping in `ThemeManager.Apply` ergänzen (Token → Dateipfad).
5. Test in `tests/HCC.Tests/ThemeSettingsTests.cs` ergänzen (speichern/laden,
   ungültiger Token, Rollback).
6. Beide Themes visuell prüfen: Kontrast von Text, Inputs, Hover, Alarmbanner.

Wichtig: Die Dateinamen sind Teil des Laufzeitvertrags. Umbenennen bricht
`ThemeManager.Apply`.

## 6. Bekannte Ausnahmen

- `DashboardViewModel` erzeugt LiveCharts-Paints in C# (`SolidColorPaint`,
  `SKColor`). Diese wechseln zur Laufzeit **nicht** mit dem Theme. Das
  Diagramm bleibt deshalb in beiden Themes dunkel (`ChartBackgroundBrush`
  ist in beiden Themes `#1C2028`). Änderung nur mit explizitem Reload der
  Chart-Paints.
- Statusindikator und Alarmzustände nutzen semantische `Brushes.*` in
  `DashboardViewModel`; das ist erlaubt, weil Farbe dort nur Zusatz zum Text ist.
- `PrimaryButtonStyle` wird derzeit nicht verwendet. Nicht löschen, aber auch
  nicht ohne konkrete Hauptaktion einführen.

## 7. Prüfliste vor dem Abschluss

```bash
dotnet build HardwareControlCenter.sln --nologo
dotnet test HardwareControlCenter.sln --nologo
```

Danach die App starten (vorher laufende Instanz beenden, sonst ist
`HCC.App.exe` gesperrt), durch alle Seiten klicken und **Dunkel sowie Hell**
prüfen:

- [ ] Text auf Karten und im Fensterhintergrund lesbar
- [ ] Eingabefelder, ComboBox-Dropdown und Fokus-Rahmen sichtbar
- [ ] Hover/gedrückt bei Navigation und Buttons unterscheidbar
- [ ] Alarmbanner und Warnhinweis kontrastreich
- [ ] Diagramm und Fortschrittsbalken lesbar
- [ ] Theme-Wechsel ohne Neustart, Auswahl bleibt nach Neustart erhalten
- [ ] Keine hardcodierten Hex-Farben in Views (Ausnahme: Abschnitt 6)

## 8. Sicherheitsregel

Die UI darf die Lüftersteuerung nicht entsperren. Text, Buttons und Status
in `FanControlView` bleiben monitoring-only; `LhmFanController` bleibt gesperrt.
Ein Theme- oder UI-Umbau ändert daran nichts.
