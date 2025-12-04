using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ToLaunch.Models;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Reflection;

namespace ToLaunch.ViewModels;
public partial class MainWindowViewModel : ViewModelBase
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

    private Timer? _processMonitorTimer;
    private bool _mainProgramWasRunning = false;

    public ICommand AddProgramCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand OpenApplicationSettingsCommand { get; }
    public ICommand LoadProfileCommand { get; }

    public Action? OnAddProgramRequested { get; set; }
    public Action<LaunchCardViewModel>? OnEditProgramRequested { get; set; }
    public Action? OnApplicationSettingsRequested { get; set; }
    public Func<string?, Task<string?>>? OnSaveProfileDialogRequested { get; set; }
    public Func<Task<string?>>? OnLoadProfileDialogRequested { get; set; }
    public Func<string?, Task<string?>>? OnSelectMainProgramRequested { get; set; }

    public static string ProfilesFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToLaunch", "Profiles");

    private static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToLaunch", "settings.json");

    private static string ProfileFilePath(string profileName) =>
    Path.Combine(ProfilesFolder, $"{profileName}.json");

    private static readonly JsonSerializerOptions CachedSerializeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CachedDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private bool _isUpdatingProfile = false;

    private void SaveProfileToDisk(string profileName)
    {
        var profileModel = new ProfileModel
        {
            MainProgramPath = MainProgramPath,
            MainProgramIconPath = MainProgramIconPath,
            Programs = LaunchCards.Select(c => c.ToModel()).ToList()
        };

        var json = JsonSerializer.Serialize(profileModel, CachedSerializeOptions);
        var filePath = ProfileFilePath(profileName);
        File.WriteAllText(filePath, json);
    }

    private static JsonSerializerOptions GetOptions()
    {
        return CachedDeserializeOptions;
    }

    public MainWindowViewModel()
    {
        AddProgramCommand = new RelayCommand(AddProgram);
        StartAllCommand = new RelayCommand(StartAll);
        SaveProfileCommand = new RelayCommand(async () => await SaveProfileWithDialogAsync());
        OpenApplicationSettingsCommand = new RelayCommand(OpenApplicationSettings);
        LoadProfileCommand = new RelayCommand(async () => await SelectMainProgramAsync());

        // Subscribe to collection changes to update AllCardsWithAddButton
        LaunchCards.CollectionChanged += (s, e) => OnPropertyChanged(nameof(AllCardsWithAddButton));

        EnsureProfilesFolderExists();
        LoadAvailableProfiles();

        // Load application settings
        LoadAppSettings();

        // Load the last selected profile from settings
        if (!string.IsNullOrWhiteSpace(AppSettings.LastSelectedProfile) && Profiles.Contains(AppSettings.LastSelectedProfile))
        {
            SelectedProfile = AppSettings.LastSelectedProfile;
        }

        if (!string.IsNullOrWhiteSpace(SelectedProfile) && Profiles.Contains(SelectedProfile))
        {
            LoadProfile(SelectedProfile);
        }

        // Start process monitoring timer
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
            OnProgramChanged = () => SaveCurrentProfile()
        };

        // Subscribe to IsRunning changes to track overall running state
        card.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LaunchCardViewModel.IsRunning))
            {
                UpdateAreAnyProgramsRunning();
            }
        };

        LaunchCards.Add(card);
    }

    public void DeleteProgram(LaunchCardViewModel card)
    {
        LaunchCards.Remove(card);
        UpdateAreAnyProgramsRunning();
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

    private void StartAll()
    {
        if (AreAnyProgramsRunning)
        {
            // Stop all running programs
            foreach (var card in LaunchCards)
            {
                if (card.IsRunning)
                {
                    card.StartCommand.Execute(null); // This will stop the program since it's running
                }
            }
        }
        else
        {
            // Start the main program first if defined for this profile AND setting is enabled
            if (AppSettings.StartMainProgramWithStartAll && !string.IsNullOrWhiteSpace(MainProgramPath) && File.Exists(MainProgramPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = MainProgramPath,
                        UseShellExecute = true
                    });
                    System.Diagnostics.Debug.WriteLine($"Started main program: {MainProgramPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start main program: {ex.Message}");
                }
            }

            // Start all enabled cards
            foreach (var card in LaunchCards)
            {
                if (card.IsEnabled && !card.IsRunning)
                {
                    card.StartCommand.Execute(null);
                }
            }
        }
    }

    private void StartProcessMonitoring()
    {
        // Check for main program every 2 seconds
        _processMonitorTimer = new Timer(CheckMainProgramStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    private void CheckMainProgramStatus(object? state)
    {
        if (string.IsNullOrWhiteSpace(MainProgramPath))
        {
            _mainProgramWasRunning = false;
            return;
        }

        var mainProgramName = Path.GetFileNameWithoutExtension(MainProgramPath);
        var isRunning = Process.GetProcesses().Any(p =>
        {
            try
            {
                return p.ProcessName.Equals(mainProgramName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });

        // Detect when main program starts
        if (isRunning && !_mainProgramWasRunning)
        {
            _mainProgramWasRunning = true;

            // Start all enabled cards with their delays
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var card in LaunchCards)
                {
                    if (card.IsEnabled && !card.IsRunning)
                    {
                        _ = card.StartCommand.ExecuteAsync(null);
                    }
                }
            });
        }
        else if (!isRunning && _mainProgramWasRunning)
        {
            _mainProgramWasRunning = false;

            // Stop all enabled cards with StopOnExit enabled
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                foreach (var card in LaunchCards)
                {
                    if (card.IsEnabled && card.StopOnExit && card.IsRunning)
                    {
                        _ = card.StartCommand.ExecuteAsync(null); // This will stop the program since it's running
                    }
                }
            });
        }
    }

    private static void EnsureProfilesFolderExists()
    {
        Directory.CreateDirectory(ProfilesFolder);
    }

    private void LoadAvailableProfiles()
    {
        Profiles.Clear();

        EnsureProfilesFolderExists();

        var files = Directory.EnumerateFiles(ProfilesFolder, "*.json");
        foreach (var f in files)
        {
            Profiles.Add(Path.GetFileNameWithoutExtension(f));
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

        // Determine default path
        string? defaultPath;
        if (!string.IsNullOrWhiteSpace(MainProgramPath))
        {
            defaultPath = Path.GetDirectoryName(MainProgramPath);
        }
        else
        {
            defaultPath = @"C:\Program Files";
        }

        var selectedFilePath = await OnSelectMainProgramRequested.Invoke(defaultPath);

        if (!string.IsNullOrWhiteSpace(selectedFilePath) && File.Exists(selectedFilePath))
        {
            var programName = Path.GetFileNameWithoutExtension(selectedFilePath);

            // Extract icon from the selected executable
            try
            {
                var iconService = new Services.IconService();
                var iconPath = await iconService.ExtractIconAsync(selectedFilePath);
                MainProgramIconPath = iconPath;

                // Load the icon bitmap
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    MainProgramIcon = new Avalonia.Media.Imaging.Bitmap(iconPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to extract main program icon: {ex.Message}");
                MainProgramIconPath = null;
                MainProgramIcon = null;
            }

            // Check if a profile with this name already exists
            var profileFilePath = ProfileFilePath(programName);
            if (File.Exists(profileFilePath))
            {
                _isUpdatingProfile = true;
                MainProgramPath = selectedFilePath;

                // Add to profiles list if not already there
                if (!Profiles.Contains(programName))
                {
                    Profiles.Add(programName);
                }

                // Set the profile name (will trigger LoadProfile via OnSelectedProfileChanged)
                SelectedProfile = programName;
                _isUpdatingProfile = false;

                // Manually load the profile since we prevented OnSelectedProfileChanged
                LoadProfile(programName);
            }
            else
            {   // Profile doesn't exist - create new with just main program

                // Clear existing cards and set main program
                LaunchCards.Clear();
                MainProgramPath = selectedFilePath;

                // Add to profiles list
                if (!Profiles.Contains(programName))
                {
                    Profiles.Add(programName);
                }

                // Save the new profile (with empty programs list)
                SaveProfileToDisk(programName);

                _isUpdatingProfile = true;
                SelectedProfile = programName;
                _isUpdatingProfile = false;
            }
        }
    }

    public void LoadProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return;

        var filePath = ProfileFilePath(profileName);
        if (!File.Exists(filePath))
        {
            // Clear cards and main program if profile doesn't exist
            LaunchCards.Clear();
            MainProgramPath = null;
            MainProgramIconPath = null;
            MainProgramIcon = null;
            return;
        }

        LoadProfileFromPath(filePath, profileName, GetOptions());
    }

    private void LoadProfileFromPath(string filePath, string profileName, JsonSerializerOptions options)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var profileModel = JsonSerializer.Deserialize<ProfileModel>(json, options);

            if (profileModel != null)
            {
                // Load main program path
                MainProgramPath = profileModel.MainProgramPath;
                MainProgramIconPath = profileModel.MainProgramIconPath;

                // Load main program icon bitmap
                if (!string.IsNullOrEmpty(MainProgramIconPath) && File.Exists(MainProgramIconPath))
                {
                    try
                    {
                        MainProgramIcon = new Avalonia.Media.Imaging.Bitmap(MainProgramIconPath);
                    }
                    catch
                    {
                        MainProgramIcon = null;
                    }
                }
                else
                {
                    MainProgramIcon = null;
                }

                // Load programs
                LaunchCards.Clear();
                foreach (var model in profileModel.Programs ?? new List<ProgramModel>())
                {
                    AddProgramFromModel(model);
                }
            }
        }
        catch (JsonException)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var models = JsonSerializer.Deserialize<List<ProgramModel>>(json, options) ?? [];

                MainProgramPath = null;
                MainProgramIconPath = null;
                MainProgramIcon = null;
                LaunchCards.Clear();
                foreach (var model in models)
                {
                    AddProgramFromModel(model);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load profile {profileName}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load profile {profileName}: {ex.Message}");
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
        try
        {
            AppSettings.LastSelectedProfile = SelectedProfile;
            var json = JsonSerializer.Serialize(AppSettings, CachedSerializeOptions);
            var directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save app settings: {ex.Message}");
        }
    }

    private void LoadAppSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, CachedDeserializeOptions);
                if (settings != null)
                {
                    AppSettings = settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load app settings: {ex.Message}");
        }
    }
}
