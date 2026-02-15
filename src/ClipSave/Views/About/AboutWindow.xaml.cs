using ClipSave.ViewModels.About;
using System.Windows;

namespace ClipSave.Views.About;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AboutViewModel oldVm)
        {
            oldVm.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is AboutViewModel newVm)
        {
            newVm.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is AboutViewModel vm)
        {
            vm.RequestClose -= OnRequestClose;
        }
    }
}
