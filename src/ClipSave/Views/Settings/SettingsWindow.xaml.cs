using ClipSave.ViewModels.Settings;
using System.ComponentModel;
using System.Windows;

namespace ClipSave.Views.Settings;

public partial class SettingsWindow : Window
{
    private bool _closingConfirmed;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closingConfirmed)
        {
            return;
        }

        if (DataContext is SettingsViewModel vm && vm.IsDirty)
        {
            e.Cancel = true;
            if (ShowCloseConfirmation())
            {
                _closingConfirmed = true;
                Dispatcher.BeginInvoke(Close);
            }
        }
    }

    private bool ShowCloseConfirmation()
    {
        var message = "Changes are not saved. Close without saving?";
        var caption = "Confirm";

        if (DataContext is SettingsViewModel vm)
        {
            message = vm.Localizer.GetString("SettingsWindow_CloseConfirmMessage");
            caption = vm.Localizer.GetString("Common_Confirmation");
        }

        var result = System.Windows.MessageBox.Show(
            message,
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SettingsViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is SettingsViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, bool? dialogResult)
    {
        // Skip confirmation only after successful save or explicit close requests.
        if (dialogResult.HasValue)
        {
            _closingConfirmed = true;

            try
            {
                DialogResult = dialogResult;
            }
            catch (InvalidOperationException)
            {
                // Ignore because DialogResult cannot be set when opened with Show().
            }

            Close();
            return;
        }

        // dialogResult == null means normal close flow (with unsaved-change confirmation).
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }
    }
}
