using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToLaunch.Models;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using System.IO;
using Avalonia.Platform;
using System.Diagnostics;
using System;

namespace ToLaunch.ViewModels;

/// <summary>
/// Represents a single CPU core for affinity selection.
/// </summary>
public partial class CpuCoreViewModel : ObservableObject
{
    [ObservableProperty]
    private int coreIndex;

    [ObservableProperty]
    private bool isSelected = true;

    public string DisplayName => $"CPU {CoreIndex}";

    public CpuCoreViewModel(int index, bool selected = true)
    {
        CoreIndex = index;
        IsSelected = selected;
    }
}

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

    // Priority settings - using separate bools for radio button binding
    [ObservableProperty]
    private bool priorityDefault = true;

    [ObservableProperty]
    private bool priorityLow = false;

    [ObservableProperty]
    private bool priorityHigh = false;

    // CPU Affinity settings
    public ObservableCollection<CpuCoreViewModel> CpuCores { get; } = new();

    [ObservableProperty]
    private bool useAllCores = true;

    public int CpuCoreCount => Environment.ProcessorCount;

    public ObservableCollection<string> AvailablePrograms { get; } = new();

    public bool SaveRequested { get; set; }
    public bool DeleteRequested { get; set; }

    public IRelayCommand? BrowseExecutableCommand { get; set; }
    public IRelayCommand? SaveCommand { get; set; }
    public IRelayCommand? DeleteCommand { get; set; }

    public LaunchCardSettingsViewModel()
    {
        LoadIconBitmap(string.Empty);
        InitializeCpuCores();
    }

    public LaunchCardSettingsViewModel(ProgramModel model, ObservableCollection<LaunchCardViewModel> existingCards)
    {
        IsNewProgram = false;
        InitializeCpuCores();
        LoadFromModel(model);
        LoadAvailablePrograms(existingCards);
    }

    private void InitializeCpuCores()
    {
        CpuCores.Clear();
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            CpuCores.Add(new CpuCoreViewModel(i, true));
        }
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
        SetPriorityFromModel(model.Priority);
        SetAffinityFromModel(model.CpuAffinity);
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
            DelayStopSeconds = DelayStopSeconds,
            Priority = GetPriorityFromSelection(),
            CpuAffinity = GetAffinityFromSelection()
        };
    }

    private void SetPriorityFromModel(ProcessPriority priority)
    {
        PriorityDefault = priority == ProcessPriority.Default;
        PriorityLow = priority == ProcessPriority.Low;
        PriorityHigh = priority == ProcessPriority.High;
    }

    private ProcessPriority GetPriorityFromSelection()
    {
        if (PriorityLow) return ProcessPriority.Low;
        if (PriorityHigh) return ProcessPriority.High;
        return ProcessPriority.Default;
    }

    private void SetAffinityFromModel(long affinity)
    {
        // 0 means use all cores (default)
        if (affinity == 0)
        {
            UseAllCores = true;
            foreach (var core in CpuCores)
            {
                core.IsSelected = true;
            }
        }
        else
        {
            UseAllCores = false;
            foreach (var core in CpuCores)
            {
                core.IsSelected = (affinity & (1L << core.CoreIndex)) != 0;
            }
        }
    }

    private long GetAffinityFromSelection()
    {
        if (UseAllCores)
            return 0; // 0 means use all cores

        long affinity = 0;
        foreach (var core in CpuCores)
        {
            if (core.IsSelected)
            {
                affinity |= (1L << core.CoreIndex);
            }
        }

        // If all cores are selected, return 0 (default)
        if (affinity == ((1L << CpuCores.Count) - 1))
            return 0;

        // If no cores are selected, return 0 (use all)
        if (affinity == 0)
            return 0;

        return affinity;
    }

    partial void OnUseAllCoresChanged(bool value)
    {
        if (value)
        {
            foreach (var core in CpuCores)
            {
                core.IsSelected = true;
            }
        }
    }

    partial void OnPriorityDefaultChanged(bool value)
    {
        if (value)
        {
            PriorityLow = false;
            PriorityHigh = false;
        }
    }

    partial void OnPriorityLowChanged(bool value)
    {
        if (value)
        {
            PriorityDefault = false;
            PriorityHigh = false;
        }
    }

    partial void OnPriorityHighChanged(bool value)
    {
        if (value)
        {
            PriorityDefault = false;
            PriorityLow = false;
        }
    }

    [RelayCommand]
    private void SelectAllCores()
    {
        UseAllCores = true;
    }

    [RelayCommand]
    private void DeselectAllCores()
    {
        UseAllCores = false;
        foreach (var core in CpuCores)
        {
            core.IsSelected = false;
        }
    }
}