using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ToLaunch.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using ToLaunch.Services;

namespace ToLaunch.ViewModels;
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    public ObservableCollection<LaunchCardViewModel> LaunchCards { get; } = [];
    public ObservableCollection<string> Profiles { get; } = [];

    /// <summary>
    /// Combined collection of all launch cards plus the add button placeholder.
    /// This allows all items to flow naturally in a WrapPanel.
    /// </summary>
    public IEnumerable<object> AllCardsWithAddButton
    {
        get
        {
            foreach (var card in LaunchCards)
            {
                yield return card;
            }
            if (ShowAddCard)
            {
                yield return AddCardPlaceholder.Instance;
            }
        }
    }

    public static string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}" : "0.1";
        }
    }

    public string WindowTitle => $"ToLaunch v{AppVersion}";

    [ObservableProperty]
    private bool showAddCard = true;

    partial void OnShowAddCardChanged(bool value)
    {
        OnPropertyChanged(nameof(AllCardsWithAddButton));
    }

    [ObservableProperty]
    private string? selectedProfile = "Default";

    [ObservableProperty]
    private AppSettings appSettings = new();

    [ObservableProperty]
    private string? mainProgramPath;

    [ObservableProperty]
    private string? mainProgramIconPath;

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? mainProgramIcon;

    [ObservableProperty]
    private bool areAnyProgramsRunning = false;

    public string StartStopButtonText => AreAnyProgramsRunning ? "Stop all programs" : "Start all programs";

    private ProcessMonitorService? _processMonitor;
    private bool _mainProgramWasRunning;
    private bool _isUpdatingProfile;
    private bool _disposed;

    public ICommand AddProgramCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand OpenApplicationSettingsCommand { get; }
    public ICommand SelectMainProgramCommand { get; }

    public Action? OnAddProgramRequested { get; set; }
    public Action<LaunchCardViewModel>? OnEditProgramRequested { get; set; }
    public Action? OnApplicationSettingsRequested { get; set; }
    public Func<string?, Task<string?>>? OnSaveProfileDialogRequested { get; set; }
    public Func<string?, Task<string?>>? OnSelectMainProgramRequested { get; set; }

    public static string ProfilesFolder => SettingsService.ProfilesFolder;

    public MainWindowViewModel()
    {
        AddProgramCommand = new RelayCommand(AddProgram);
        StartAllCommand = new RelayCommand(ToggleAllPrograms);
        SaveProfileCommand = new RelayCommand(async () => await SaveProfileWithDialogAsync());
        OpenApplicationSettingsCommand = new RelayCommand(OpenApplicationSettings);
        SelectMainProgramCommand = new RelayCommand(async () => await SelectMainProgramAsync());

        // Subscribe to collection changes to update AllCardsWithAddButton
        LaunchCards.CollectionChanged += (s, e) => OnPropertyChanged(nameof(AllCardsWithAddButton));

        // The XAML previewer instantiates this view model. Stop before touching the file system or
        // starting the process monitor, which have no business running inside the designer.
        if (Avalonia.Controls.Design.IsDesignMode)
            return;

        EnsureProfilesFolderExists();
        LoadAvailableProfiles();

        AppSettings = SettingsService.Load();

        // Load the last selected profile from settings
        if (!string.IsNullOrWhiteSpace(AppSettings.LastSelectedProfile) && Profiles.Contains(AppSettings.LastSelectedProfile))
        {
            SelectedProfile = AppSettings.LastSelectedProfile;
        }

        if (!string.IsNullOrWhiteSpace(SelectedProfile) && Profiles.Contains(SelectedProfile))
        {
            LoadProfile(SelectedProfile);
        }

        StartProcessMonitoring();
    }

    private void AddProgram()
    {
        OnAddProgramRequested?.Invoke();
    }

    public void AddProgramFromModel(ProgramModel model)
    {
        var card = new LaunchCardViewModel(model)
        {
            OnSettingsRequested = OnEditProgramRequested,
            OnProgramChanged = SaveCurrentProfile
        };

        card.PropertyChanged += Card_PropertyChanged;
        LaunchCards.Add(card);

        // Pick up the new program's actual state without waiting out the monitor interval.
        _processMonitor?.PollNow();
    }

    public void DeleteProgram(LaunchCardViewModel card)
    {
        if (LaunchCards.Remove(card))
        {
            DetachCard(card);
        }

        UpdateAreAnyProgramsRunning();
    }

    private void Card_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LaunchCardViewModel.IsRunning))
        {
            UpdateAreAnyProgramsRunning();
        }
    }

    private void DetachCard(LaunchCardViewModel card)
    {
        card.PropertyChanged -= Card_PropertyChanged;
        card.OnSettingsRequested = null;
        card.OnProgramChanged = null;
        card.Dispose();
    }

    private void ClearLaunchCards()
    {
        foreach (var card in LaunchCards)
        {
            DetachCard(card);
        }

        LaunchCards.Clear();
    }

    private void UpdateAreAnyProgramsRunning()
    {
        AreAnyProgramsRunning = LaunchCards.Any(c => c.IsRunning);
        OnPropertyChanged(nameof(StartStopButtonText));
    }

    public void SaveCurrentProfile()
    {
        if (!string.IsNullOrWhiteSpace(SelectedProfile))
        {
            SaveProfileToDisk(SelectedProfile);
        }
    }

    private void SaveProfileToDisk(string profileName)
    {
        try
        {
            var profileModel = new ProfileModel
            {
                MainProgramPath = MainProgramPath,
                MainProgramIconPath = MainProgramIconPath,
                Programs = LaunchCards.Select(c => c.ToModel()).ToList()
            };

            EnsureProfilesFolderExists();
            File.WriteAllText(SettingsService.ProfileFilePath(profileName), SettingsService.SerializeProfile(profileModel));
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to save profile {profileName}: {ex.Message}");
        }
    }

    /// <summary>The start/stop-all button: stops everything if anything is running, starts otherwise.</summary>
    private void ToggleAllPrograms()
    {
        if (AreAnyProgramsRunning)
            StopAllPrograms();
        else
            StartAllPrograms();
    }

    /// <summary>
    /// Starts the main program (when configured and enabled in settings) and every enabled card.
    /// </summary>
    public void StartAllPrograms()
    {
        if (AppSettings.StartMainProgramWithStartAll && !string.IsNullOrWhiteSpace(MainProgramPath) && File.Exists(MainProgramPath))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = MainProgramPath,
                    UseShellExecute = true
                })?.Dispose();
                LogService.LogInfo($"Started main program: {MainProgramPath}");
            }
            catch (Exception ex)
            {
                LogService.LogError("Failed to start main program", ex);
            }
        }

        foreach (var card in LaunchCards)
        {
            if (card.IsEnabled && !card.IsRunning && !HasPendingStartDependency(card))
            {
                _ = card.StartAsync();
            }
        }
    }

    /// <summary>Stops every running card.</summary>
    public void StopAllPrograms()
    {
        foreach (var card in LaunchCards)
        {
            if (card.IsRunning)
            {
                _ = card.StopAsync();
            }
        }
    }

    /// <summary>
    /// Asks the process monitor for an immediate refresh, so a change to what is being watched (a new
    /// card, an edited path) is reflected without waiting out the poll interval.
    /// </summary>
    public void RefreshProcessState()
    {
        _processMonitor?.PollNow();
    }

    private void StartProcessMonitoring()
    {
        _processMonitor = new ProcessMonitorService(CollectWatchedPaths, OnProcessSnapshot);
        _processMonitor.Start();
    }

    /// <summary>
    /// The executables the monitor should look for. Read on the UI thread before each capture.
    /// </summary>
    private IReadOnlyList<string?> CollectWatchedPaths()
    {
        var paths = new List<string?>(LaunchCards.Count + 1) { MainProgramPath };

        foreach (var card in LaunchCards)
        {
            paths.Add(card.Path);
        }

        return paths;
    }

    /// <summary>
    /// Single ordered pass over a process snapshot, on the UI thread.
    /// </summary>
    /// <remarks>
    /// Order matters: card state is refreshed <em>before</em> anything is started or stopped, so a
    /// program launched during this pass is never re-evaluated against the snapshot that predates it.
    /// </remarks>
    private void OnProcessSnapshot(ProcessSnapshot snapshot)
    {
        if (_disposed)
            return;

        // 1. Refresh every card from the snapshot.
        foreach (var card in LaunchCards)
        {
            card.RefreshRunningState(snapshot);
        }

        // 2. React to the main program starting or stopping.
        bool mainProgramRunning = !string.IsNullOrWhiteSpace(MainProgramPath) && snapshot.IsRunning(MainProgramPath);

        if (mainProgramRunning && !_mainProgramWasRunning)
        {
            LogService.LogInfo("Main program started; starting enabled programs");
            foreach (var card in LaunchCards)
            {
                if (card.IsEnabled && !card.IsRunning && !HasPendingStartDependency(card))
                {
                    _ = card.StartAsync();
                }
            }
        }
        else if (!mainProgramRunning && _mainProgramWasRunning)
        {
            LogService.LogInfo("Main program exited; stopping programs flagged to stop on exit");
            foreach (var card in LaunchCards)
            {
                if (card.IsEnabled && card.StopOnExit && card.IsRunning)
                {
                    _ = card.StopAsync();
                }
            }
        }

        _mainProgramWasRunning = mainProgramRunning;

        // 3. Propagate start-with / stop-with dependencies.
        ApplyProgramDependencies();

        // 4. Recompute the summary and record this tick's state for the next one.
        UpdateAreAnyProgramsRunning();

        foreach (var card in LaunchCards)
        {
            card.PreviousIsRunning = card.IsRunning;
        }
    }

    /// <summary>
    /// Starts cards whose "start with" target has just started, and stops cards whose "stop with"
    /// target has just stopped.
    /// </summary>
    /// <remarks>
    /// Only one link of a dependency chain advances per tick, which keeps the pass bounded and makes
    /// cycles harmless. Targets are matched on card name, the same way the settings dialog offers them.
    /// </remarks>
    private void ApplyProgramDependencies()
    {
        foreach (var card in LaunchCards)
        {
            if (!card.IsEnabled)
                continue;

            if (card.StartWithProgram && !card.IsRunning && !card.IsLaunchPending)
            {
                var target = FindCardByName(card.StartWithProgramName);

                // Fire on the transition into running, not on the steady state, so a program the user
                // deliberately stopped is not immediately restarted.
                if (target != null && target != card && target.IsRunning && !target.PreviousIsRunning)
                {
                    LogService.LogInfo($"Starting {card.Name} because {target.Name} started");
                    _ = card.StartAsync();
                }
            }

            if (card.StopWithProgram && card.IsRunning)
            {
                var target = FindCardByName(card.StopWithProgramName);

                if (target != null && target != card && !target.IsRunning && target.PreviousIsRunning)
                {
                    LogService.LogInfo($"Stopping {card.Name} because {target.Name} stopped");
                    _ = card.StopAsync();
                }
            }
        }
    }

    /// <summary>
    /// True when this card is configured to wait for another card before starting, so the bulk
    /// "start everything" paths should leave it to <see cref="ApplyProgramDependencies"/>. A dependency
    /// naming a program that no longer exists is treated as no dependency, so the card still starts.
    /// </summary>
    private bool HasPendingStartDependency(LaunchCardViewModel card)
    {
        if (!card.StartWithProgram)
            return false;

        var target = FindCardByName(card.StartWithProgramName);
        return target != null && target != card;
    }

    private LaunchCardViewModel? FindCardByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return LaunchCards.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureProfilesFolderExists()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.ProfilesFolder);
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to create profiles folder: {ex.Message}");
        }
    }

    private void LoadAvailableProfiles()
    {
        Profiles.Clear();

        EnsureProfilesFolderExists();

        try
        {
            foreach (var file in Directory.EnumerateFiles(SettingsService.ProfilesFolder, "*.json"))
            {
                Profiles.Add(Path.GetFileNameWithoutExtension(file));
            }
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to enumerate profiles: {ex.Message}");
        }

        // Always have a Default profile available
        if (!Profiles.Contains("Default"))
            Profiles.Insert(0, "Default");

        // Ensure selected profile has a sensible default
        if (string.IsNullOrWhiteSpace(SelectedProfile))
            SelectedProfile = Profiles.FirstOrDefault();
    }

    private async Task SaveProfileWithDialogAsync()
    {
        if (OnSaveProfileDialogRequested == null)
            return;

        var selectedFileName = await OnSaveProfileDialogRequested.Invoke(SelectedProfile);

        if (!string.IsNullOrWhiteSpace(selectedFileName))
        {
            var profileName = Path.GetFileNameWithoutExtension(selectedFileName);
            SaveProfileToDisk(profileName);

            // Update ComboBox
            if (!Profiles.Contains(profileName))
            {
                Profiles.Add(profileName);
            }
            SelectedProfile = profileName;
        }
    }

    private async Task SelectMainProgramAsync()
    {
        if (OnSelectMainProgramRequested == null)
            return;

        var defaultPath = !string.IsNullOrWhiteSpace(MainProgramPath)
            ? Path.GetDirectoryName(MainProgramPath)
            : @"C:\Program Files";

        var selectedFilePath = await OnSelectMainProgramRequested.Invoke(defaultPath);

        if (string.IsNullOrWhiteSpace(selectedFilePath) || !File.Exists(selectedFilePath))
            return;

        var programName = Path.GetFileNameWithoutExtension(selectedFilePath);

        // Extract icon from the selected executable
        try
        {
            var iconService = new IconService();
            var iconPath = await iconService.ExtractIconAsync(selectedFilePath);
            MainProgramIconPath = iconPath;
            MainProgramIcon = LoadBitmap(iconPath);
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to extract main program icon: {ex.Message}");
            MainProgramIconPath = null;
            MainProgramIcon = null;
        }

        // Check if a profile with this name already exists
        if (File.Exists(SettingsService.ProfileFilePath(programName)))
        {
            _isUpdatingProfile = true;
            MainProgramPath = selectedFilePath;

            if (!Profiles.Contains(programName))
            {
                Profiles.Add(programName);
            }

            // Set the profile name without triggering OnSelectedProfileChanged, then load explicitly.
            SelectedProfile = programName;
            _isUpdatingProfile = false;

            LoadProfile(programName);
        }
        else
        {
            // Profile doesn't exist - create a new one holding just the main program.
            ClearLaunchCards();
            MainProgramPath = selectedFilePath;

            if (!Profiles.Contains(programName))
            {
                Profiles.Add(programName);
            }

            SaveProfileToDisk(programName);

            _isUpdatingProfile = true;
            SelectedProfile = programName;
            _isUpdatingProfile = false;
        }

        _processMonitor?.PollNow();
    }

    public void LoadProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return;

        var filePath = SettingsService.ProfileFilePath(profileName);
        if (!File.Exists(filePath))
        {
            // Clear cards and main program if profile doesn't exist
            ClearLaunchCards();
            MainProgramPath = null;
            MainProgramIconPath = null;
            MainProgramIcon = null;
            return;
        }

        LoadProfileFromPath(filePath, profileName);
    }

    private void LoadProfileFromPath(string filePath, string profileName)
    {
        try
        {
            var json = File.ReadAllText(filePath);

            ProfileModel? profileModel;
            try
            {
                profileModel = SettingsService.Deserialize<ProfileModel>(json);
            }
            catch (JsonException)
            {
                // Legacy profiles were a bare array of programs.
                var models = SettingsService.Deserialize<List<ProgramModel>>(json) ?? [];
                profileModel = new ProfileModel { Programs = models };
            }

            if (profileModel == null)
                return;

            MainProgramPath = profileModel.MainProgramPath;
            MainProgramIconPath = profileModel.MainProgramIconPath;
            MainProgramIcon = LoadBitmap(MainProgramIconPath);

            ClearLaunchCards();
            foreach (var model in profileModel.Programs ?? [])
            {
                AddProgramFromModel(model);
            }

            _processMonitor?.PollNow();
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to load profile {profileName}: {ex.Message}");
        }
    }

    private static Avalonia.Media.Imaging.Bitmap? LoadBitmap(string? iconPath)
    {
        if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
            return null;

        try
        {
            return new Avalonia.Media.Imaging.Bitmap(iconPath);
        }
        catch
        {
            LogService.LogWarning($"Failed to load icon from path: {iconPath}");
            return null;
        }
    }

    private void OpenApplicationSettings()
    {
        OnApplicationSettingsRequested?.Invoke();
    }

    partial void OnSelectedProfileChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !_isUpdatingProfile)
        {
            SaveAppSettings();
            LoadProfile(value);
        }
    }

    public void SaveAppSettings()
    {
        AppSettings.LastSelectedProfile = SelectedProfile;
        SettingsService.Save(AppSettings);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _processMonitor?.Dispose();
        _processMonitor = null;

        foreach (var card in LaunchCards)
        {
            DetachCard(card);
        }
    }
}
