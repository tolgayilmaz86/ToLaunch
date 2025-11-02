using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ToLaunch.ViewModels;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using System;

namespace ToLaunch.Views;
public partial class LaunchCardSettingsView : UserControl
{
    private LaunchCardSettingsViewModel? ViewModel => DataContext as LaunchCardSettingsViewModel;

    public LaunchCardSettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.BrowseExecutableCommand = new RelayCommand(BrowseExecutable);
            ViewModel.SaveCommand = new RelayCommand(OnSave);
            ViewModel.DeleteCommand = new RelayCommand(OnDelete);
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Ensure commands are set even if DataContextChanged didn't fire
        if (ViewModel != null)
        {
            ViewModel.BrowseExecutableCommand ??= new RelayCommand(BrowseExecutable);
            ViewModel.SaveCommand ??= new RelayCommand(OnSave);
            ViewModel.DeleteCommand ??= new RelayCommand(OnDelete);
        }
    }

    private async void BrowseExecutable()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Program",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Executable Files")
                {
                    Patterns = ["*.exe"]
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = ["*.*"]
                }
            ]
        });

        if (files.Any() && ViewModel != null)
        {
            var file = files[0];
            ViewModel.Path = file.Path.LocalPath;

            if (string.IsNullOrWhiteSpace(ViewModel.Name))
            {
                ViewModel.Name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
            }

            // Extract icon using IconService
            try
            {
                var iconService = new Services.IconService();
                var iconPath = await iconService.ExtractIconAsync(ViewModel.Path);
                if (!string.IsNullOrEmpty(iconPath))
                {
                    ViewModel.IconPath = iconPath;
                    ViewModel.LoadIconBitmap(iconPath);
                }
                else
                {
                    // Use default icon if extraction fails
                    ViewModel.IconPath = string.Empty;
                    ViewModel.LoadIconBitmap(string.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Icon extraction failed: {ex.Message}");
                // Use default icon on exception
                ViewModel.IconPath = string.Empty;
                ViewModel.LoadIconBitmap(string.Empty);
            }
        }
    }

    private void OnSave()
    {
        if (ViewModel != null)
        {
            ViewModel.SaveRequested = true;
            CloseParentWindow();
        }
    }

    private void OnDelete()
    {
        if (ViewModel != null)
        {
            ViewModel.DeleteRequested = true;
            CloseParentWindow();
        }
    }

    private void CloseParentWindow()
    {
        // Find the parent window and close it
        var window = TopLevel.GetTopLevel(this) as Window;
        window?.Close();
    }
}