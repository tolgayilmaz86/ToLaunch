using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToLaunch.Models;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using System.IO;
using Avalonia.Platform;

namespace ToLaunch.ViewModels;

public partial class LaunchCardSettingsViewModel : ViewModelBase
{
    private const string DefaultIconPath = "avares://ToLaunch/Assets/cmd-icon.ico";

    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private string iconPath = string.Empty;

    [ObservableProperty]
    private Bitmap? iconBitmap;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string arguments = string.Empty;

    [ObservableProperty]
    private bool isEnabled = true;

    [ObservableProperty]
    private bool startHidden;

    [ObservableProperty]
    private bool stopOnExit = true;

    [ObservableProperty]
    private bool startWithProgram;

    [ObservableProperty]
    private string? selectedStartProgram;

    [ObservableProperty]
    private bool stopWithProgram;

    [ObservableProperty]
    private string? selectedStopProgram;

    [ObservableProperty]
    private int delayStartSeconds;

    [ObservableProperty]
    private int delayStopSeconds;

    [ObservableProperty]
    private bool useApplicationIcon = true;

    [ObservableProperty]
    private bool isNewProgram = true;

    [ObservableProperty]
    private bool showAdvancedSettings = false;

    public ObservableCollection<string> AvailablePrograms { get; } = new();

    public bool SaveRequested { get; set; }
    public bool DeleteRequested { get; set; }

    public IRelayCommand? BrowseExecutableCommand { get; set; }
    public IRelayCommand? SaveCommand { get; set; }
    public IRelayCommand? DeleteCommand { get; set; }

    public LaunchCardSettingsViewModel()
    {
        LoadIconBitmap(string.Empty);
    }

    public LaunchCardSettingsViewModel(ProgramModel model, ObservableCollection<LaunchCardViewModel> existingCards)
    {
        IsNewProgram = false;
        LoadFromModel(model);
        LoadAvailablePrograms(existingCards);
    }

    public void LoadAvailablePrograms(ObservableCollection<LaunchCardViewModel> cards)
    {
        AvailablePrograms.Clear();
        foreach (var card in cards.Where(c => !string.IsNullOrEmpty(c.Name)))
        {
            AvailablePrograms.Add(card.Name);
        }
    }

    private void LoadFromModel(ProgramModel model)
    {
        Path = model.Path;
        IconPath = model.IconPath;
        LoadIconBitmap(model.IconPath);
        Name = model.Name;
        Arguments = model.Arguments;
        IsEnabled = model.IsEnabled;
        StartHidden = model.StartHidden;
        StopOnExit = model.StopOnExit;
        StartWithProgram = model.StartWithProgram;
        SelectedStartProgram = model.StartWithProgramName;
        StopWithProgram = model.StopWithProgram;
        SelectedStopProgram = model.StopWithProgramName;
        DelayStartSeconds = model.DelayStartSeconds;
        DelayStopSeconds = model.DelayStopSeconds;
    }

    public void LoadIconBitmap(string iconFilePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath))
            {
                IconBitmap = new Bitmap(iconFilePath);
            }
            else
            {
                // Load default icon from assets
                var assets = AssetLoader.Open(new System.Uri(DefaultIconPath));
                IconBitmap = new Bitmap(assets);
            }
            OnPropertyChanged(nameof(IconBitmap));
        }
        catch
        {
            IconBitmap = null;
            OnPropertyChanged(nameof(IconBitmap));
        }
    }

    public ProgramModel ToModel()
    {
        return new ProgramModel
        {
            Name = Name,
            Path = Path,
            Arguments = Arguments,
            IconPath = IconPath,
            IsEnabled = IsEnabled,
            StartHidden = StartHidden,
            StopOnExit = StopOnExit,
            StartWithProgram = StartWithProgram,
            StartWithProgramName = SelectedStartProgram,
            StopWithProgram = StopWithProgram,
            StopWithProgramName = SelectedStopProgram,
            DelayStartSeconds = DelayStartSeconds,
            DelayStopSeconds = DelayStopSeconds
        };
    }

    [RelayCommand]
    private void ToggleAdvancedSettings()
    {
        ShowAdvancedSettings = !ShowAdvancedSettings;
        OnPropertyChanged(nameof(ShowAdvancedSettings));
    }
}