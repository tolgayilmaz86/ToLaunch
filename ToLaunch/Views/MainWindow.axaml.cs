using Avalonia.Controls;
using ToLaunch.ViewModels;
using Avalonia.Platform.Storage;
using System.Linq;
using System.Threading.Tasks;

namespace ToLaunch.Views;
public partial class MainWindow : Window
{
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.OnAddProgramRequested = ShowAddProgramDialog;
            ViewModel.OnEditProgramRequested = ShowEditProgramDialog;
            ViewModel.OnApplicationSettingsRequested = ShowApplicationSettingsDialog;
            ViewModel.OnSaveProfileDialogRequested = ShowSaveProfileDialog;
            ViewModel.OnLoadProfileDialogRequested = ShowLoadProfileDialog;
            ViewModel.OnSelectMainProgramRequested = ShowSelectMainProgramDialog;

            // Wire up callbacks for existing cards (cards that were loaded from profile)
            foreach (var card in ViewModel.LaunchCards)
            {
                card.OnSettingsRequested = ShowEditProgramDialog;
                card.OnProgramChanged = () => ViewModel.SaveCurrentProfile();
            }
        }
    }

    private async void ShowAddProgramDialog()
    {
        var viewModel = new LaunchCardSettingsViewModel
        {
            IsNewProgram = true
        };

        if (ViewModel != null)
        {
            viewModel.LoadAvailablePrograms(ViewModel.LaunchCards);
        }

        var dialog = new Window
        {
            Title = "Add Program",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new LaunchCardSettingsView
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowDialog(this);

        if (viewModel.SaveRequested && ViewModel != null)
        {
            var result = viewModel.ToModel();
            ViewModel.AddProgramFromModel(result);

            // Auto-save profile after adding program
            ViewModel.SaveCurrentProfile();
            System.Diagnostics.Debug.WriteLine($"Profile auto-saved after adding program: {result.Name}");
        }
    }

    private async void ShowEditProgramDialog(LaunchCardViewModel card)
    {
        var viewModel = new LaunchCardSettingsViewModel(card.ToModel(), ViewModel?.LaunchCards ?? new())
        {
            IsNewProgram = false
        };

        var dialog = new Window
        {
            Title = "Edit Program",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new LaunchCardSettingsView
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowDialog(this);

        if (viewModel.DeleteRequested && ViewModel != null)
        {
            ViewModel.DeleteProgram(card);

            // Auto-save profile after deleting program
            ViewModel.SaveCurrentProfile();
            System.Diagnostics.Debug.WriteLine($"Profile auto-saved after deleting program: {card.Name}");
        }
        else if (viewModel.SaveRequested)
        {
            var result = viewModel.ToModel();
            card.LoadFromModel(result);

            // Auto-save profile after editing program
            if (ViewModel != null)
            {
                ViewModel.SaveCurrentProfile();
                System.Diagnostics.Debug.WriteLine($"Profile auto-saved after editing program: {result.Name}");
            }
        }
    }

    private async void ShowApplicationSettingsDialog()
    {
        var viewModel = new ApplicationSettingsViewModel();

        if (ViewModel != null)
        {
            viewModel.LoadFromModel(ViewModel.AppSettings);
        }

        var dialog = new Window
        {
            Title = "Application Settings",
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new ApplicationSettingsView
            {
                DataContext = viewModel
            }
        };

        await dialog.ShowDialog(this);

        // Save settings when dialog closes
        if (ViewModel != null)
        {
            var result = viewModel.ToModel();
            ViewModel.AppSettings.StartWithWindows = result.StartWithWindows;
            ViewModel.AppSettings.CloseToSystemTray = result.CloseToSystemTray;
            ViewModel.AppSettings.MinimizeToSystemTray = result.MinimizeToSystemTray;
            ViewModel.AppSettings.StartMainProgramWithStartAll = result.StartMainProgramWithStartAll;

            // Persist to disk
            ViewModel.SaveAppSettings();
        }
    }

    private async Task<string?> ShowSaveProfileDialog(string? defaultFileName)
    {
        var storageProvider = StorageProvider;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Profile",
            SuggestedFileName = string.IsNullOrWhiteSpace(defaultFileName) ? "NewProfile.json" : $"{defaultFileName}.json",
            DefaultExtension = "json",
            SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(MainWindowViewModel.ProfilesFolder),
            FileTypeChoices =
            [
                new FilePickerFileType("Profile Files") { Patterns = ["*.json"] }
            ]
        });

        return file?.Path.LocalPath;
    }

    private async Task<string?> ShowLoadProfileDialog()
    {
        var storageProvider = StorageProvider;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Profile",
            AllowMultiple = false,
            SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(MainWindowViewModel.ProfilesFolder),
            FileTypeFilter =
            [
                new FilePickerFileType("Profile Files") { Patterns = ["*.json"] }
            ]
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> ShowSelectMainProgramDialog(string? defaultPath)
    {
        var storageProvider = StorageProvider;

        var startLocation = await storageProvider.TryGetFolderFromPathAsync(defaultPath ?? @"C:\Program Files");

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Main Program/Game",
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable Files") { Patterns = ["*.exe"] },
                new FilePickerFileType("All Files") { Patterns = ["*.*"] }
            ]
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }
}