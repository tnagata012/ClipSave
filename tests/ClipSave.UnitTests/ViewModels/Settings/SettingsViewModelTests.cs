using ClipSave.Infrastructure;
using ClipSave.Services;
using ClipSave.ViewModels.Settings;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Windows.Input;

namespace ClipSave.UnitTests;

[Collection("SettingsTests")]
public class SettingsViewModelTests : IDisposable
{
    private readonly string _testAppDataPath;
    private readonly SettingsService _settingsService;

    public SettingsViewModelTests()
    {
        _testAppDataPath = Path.Combine(Path.GetTempPath(), $"ClipSave_ViewModelTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testAppDataPath);

        var mockLogger = new Mock<ILogger<SettingsService>>();
        _settingsService = new SettingsService(mockLogger.Object, _testAppDataPath);
        _settingsService.UpdateSettings(settings => settings.Ui.Language = AppLanguage.English);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testAppDataPath))
        {
            try
            {
                Directory.Delete(_testAppDataPath, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void IsDirty_InitialLoad_ReturnsFalse()
    {
        // Arrange & Act
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void CanSave_InitialLoad_ReturnsFalse()
    {
        // Arrange & Act
        var viewModel = new SettingsViewModel(_settingsService);

        // Assert
        viewModel.CanSave.Should().BeFalse();
        viewModel.SaveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void HasUnsavedChanges_InitialLoad_ReturnsFalse()
    {
        // Arrange & Act
        var viewModel = new SettingsViewModel(_settingsService);

        // Assert
        viewModel.HasUnsavedChanges.Should().BeFalse();
        viewModel.CancelButtonText.Should().Be("Close");
    }

    [Fact]
    public void IsDirty_AfterPropertyChange_ReturnsTrue()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.SaveFormat = viewModel.SaveFormat == "png" ? "jpg" : "png";

        viewModel.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void CanSave_AfterPropertyChange_ReturnsTrue()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        // Act
        viewModel.SaveFormat = viewModel.SaveFormat == "png" ? "jpg" : "png";

        // Assert
        viewModel.CanSave.Should().BeTrue();
        viewModel.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void HasUnsavedChanges_AfterPropertyChange_ReturnsTrue()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        // Act
        viewModel.SaveFormat = viewModel.SaveFormat == "png" ? "jpg" : "png";

        // Assert
        viewModel.HasUnsavedChanges.Should().BeTrue();
        viewModel.CancelButtonText.Should().Be("Cancel");
    }

    [Fact]
    public void IsDirty_AfterRevertingChange_ReturnsFalse()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        var originalFormat = viewModel.SaveFormat;

        viewModel.SaveFormat = originalFormat == "png" ? "jpg" : "png";
        viewModel.SaveFormat = originalFormat;

        viewModel.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void IsDirty_AfterSuccessfulSave_ReturnsFalse()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.SaveFormat = viewModel.SaveFormat == "png" ? "jpg" : "png";
        viewModel.IsDirty.Should().BeTrue();

        bool closeRequested = false;
        viewModel.RequestClose += (sender, result) => closeRequested = true;

        viewModel.SaveCommand.Execute(null);

        closeRequested.Should().BeTrue();
        viewModel.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Save_PreservesStartupGuidanceShown()
    {
        _settingsService.UpdateSettings(s => s.Advanced.StartupGuidanceShown = true);
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.Logging = !viewModel.Logging;

        // Act
        viewModel.SaveCommand.Execute(null);

        _settingsService.Current.Advanced.StartupGuidanceShown.Should().BeTrue();
    }

    [Fact]
    public void IsDirty_AllPropertiesTracked()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        var propertiesToTest = new Action<SettingsViewModel>[]
        {
            vm => vm.SaveFormat = vm.SaveFormat == "png" ? "jpg" : "png",
            vm => vm.JpegQuality = vm.JpegQuality + 1,
            vm => vm.FileNamePrefix = vm.FileNamePrefix == "CS" ? "CLIP" : "CS",
            vm => vm.IncludeTimestamp = !vm.IncludeTimestamp,
            vm => vm.ImageEnabled = !vm.ImageEnabled,
            vm => vm.TextEnabled = !vm.TextEnabled,
            vm => vm.MarkdownEnabled = !vm.MarkdownEnabled,
            vm => vm.JsonEnabled = !vm.JsonEnabled,
            vm => vm.CsvEnabled = !vm.CsvEnabled,
            vm => vm.HotkeyCtrl = !vm.HotkeyCtrl,
            vm => vm.HotkeyShift = !vm.HotkeyShift,
            vm => vm.HotkeyAlt = !vm.HotkeyAlt,
            vm => vm.HotkeyKey = vm.HotkeyKey == Key.V ? Key.A : Key.V,
            vm => vm.NotifyOnSuccess = !vm.NotifyOnSuccess,
            vm => vm.NotifyOnNoContent = !vm.NotifyOnNoContent,
            vm => vm.NotifyOnError = !vm.NotifyOnError,
            vm => vm.Logging = !vm.Logging,
            vm => vm.SelectedLanguage = vm.SelectedLanguage == AppLanguage.English
                ? AppLanguage.Japanese
                : AppLanguage.English
        };

        foreach (var changeProperty in propertiesToTest)
        {
            var vm = new SettingsViewModel(_settingsService);
            vm.IsDirty.Should().BeFalse("initial state");

            changeProperty(vm);

            vm.IsDirty.Should().BeTrue("after a property change");
        }
    }

    [Fact]
    public void HasError_InitialState_ReturnsFalse()
    {
        // Arrange & Act
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public void HasError_WhenStatusMessageSet_ReturnsTrue()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = false;

        viewModel.SaveCommand.Execute(null);

        viewModel.HasError.Should().BeTrue();
        viewModel.StatusMessage.Should().NotBeEmpty();
    }

    [Fact]
    public void HasError_AfterSuccessfulSave_ReturnsFalse()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = false;
        viewModel.SaveCommand.Execute(null);
        viewModel.HasError.Should().BeTrue();

        bool closeRequested = false;
        viewModel.RequestClose += (sender, result) => closeRequested = true;

        viewModel.HotkeyCtrl = true;
        viewModel.SaveCommand.Execute(null);

        closeRequested.Should().BeTrue();
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public void StatusMessage_WhenInputChanged_ClearsError()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = false;
        viewModel.SaveCommand.Execute(null);
        viewModel.HasError.Should().BeTrue();

        // Act
        viewModel.HotkeyCtrl = true;

        // Assert
        viewModel.HasError.Should().BeFalse();
        viewModel.StatusMessage.Should().BeEmpty();
    }

    [Fact]
    public void JpegQuality_OutOfRange_IsClamped()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        // Act
        viewModel.JpegQuality = 0;

        // Assert
        viewModel.JpegQuality.Should().Be(1);

        // Act
        viewModel.JpegQuality = 101;

        // Assert
        viewModel.JpegQuality.Should().Be(100);
    }

    [Fact]
    public void HotkeyPreview_WhenHotkeyChanges_UpdatesText()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        // Act
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = true;
        viewModel.HotkeyKey = Key.D1;

        // Assert
        viewModel.HotkeyPreview.Should().Be("Ctrl + Alt + 1");
    }

    [Fact]
    public void IsImageSettingsEnabled_WhenImageDisabled_ReturnsFalse()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.IsImageSettingsEnabled.Should().BeTrue();

        // Act
        viewModel.ImageEnabled = false;

        // Assert
        viewModel.IsImageSettingsEnabled.Should().BeFalse();
    }

    [Fact]
    public void HasNoEnabledContentType_WhenAllDisabled_ReturnsTrue()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);

        // Act
        viewModel.ImageEnabled = false;
        viewModel.TextEnabled = false;
        viewModel.MarkdownEnabled = false;
        viewModel.JsonEnabled = false;
        viewModel.CsvEnabled = false;

        // Assert
        viewModel.HasAnyEnabledContentType.Should().BeFalse();
        viewModel.HasNoEnabledContentType.Should().BeTrue();
    }

    [Fact]
    public void Save_WhenAllContentTypesDisabled_ShowsValidationError()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.ImageEnabled = false;
        viewModel.TextEnabled = false;
        viewModel.MarkdownEnabled = false;
        viewModel.JsonEnabled = false;
        viewModel.CsvEnabled = false;

        // Act
        viewModel.SaveCommand.Execute(null);

        // Assert
        viewModel.HasError.Should().BeTrue();
        viewModel.StatusMessage.Should().Contain("at least one");
    }

    [Fact]
    public void ResetToDefaults_RestoresAllDefaultValues()
    {
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.SaveFormat = "jpg";
        viewModel.JpegQuality = 50;
        viewModel.FileNamePrefix = "CUSTOM";
        viewModel.IncludeTimestamp = false;
        viewModel.ImageEnabled = false;
        viewModel.TextEnabled = false;
        viewModel.MarkdownEnabled = false;
        viewModel.JsonEnabled = false;
        viewModel.CsvEnabled = false;
        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = true;
        viewModel.HotkeyKey = Key.A;
        viewModel.NotifyOnSuccess = true;
        viewModel.NotifyOnNoContent = true;
        viewModel.NotifyOnError = false;
        viewModel.Logging = true;

        viewModel.ResetToDefaultsCommand.Execute(null);

        viewModel.SaveFormat.Should().Be("png");
        viewModel.JpegQuality.Should().Be(90);
        viewModel.FileNamePrefix.Should().Be("CS");
        viewModel.IncludeTimestamp.Should().BeTrue();
        viewModel.ImageEnabled.Should().BeTrue();
        viewModel.TextEnabled.Should().BeTrue();
        viewModel.MarkdownEnabled.Should().BeTrue();
        viewModel.JsonEnabled.Should().BeTrue();
        viewModel.CsvEnabled.Should().BeTrue();
        viewModel.HotkeyCtrl.Should().BeTrue();
        viewModel.HotkeyShift.Should().BeTrue();
        viewModel.HotkeyAlt.Should().BeFalse();
        viewModel.HotkeyKey.Should().Be(Key.V);
        viewModel.NotifyOnSuccess.Should().BeFalse();
        viewModel.NotifyOnNoContent.Should().BeFalse();
        viewModel.NotifyOnError.Should().BeTrue();
        viewModel.Logging.Should().BeFalse();
        viewModel.SelectedLanguage.Should().BeOneOf(AppLanguage.English, AppLanguage.Japanese);
    }

    [Fact]
    public void ResetToDefaults_MarksAsDirty_WhenSettingsWereChanged()
    {
        _settingsService.UpdateSettings(s => s.Save.ImageFormat = "jpg");
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.IsDirty.Should().BeFalse();

        viewModel.ResetToDefaultsCommand.Execute(null);

        // Assert
        viewModel.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void IsDirty_PropertyChangedNotification_Raised()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.IsDirty))
            {
                propertyChangedRaised = true;
            }
        };

        // Act
        viewModel.SaveFormat = viewModel.SaveFormat == "png" ? "jpg" : "png";

        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void HasError_PropertyChangedNotification_Raised()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_settingsService);
        var propertyChangedRaised = false;

        viewModel.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.HasError))
            {
                propertyChangedRaised = true;
            }
        };

        viewModel.HotkeyCtrl = false;
        viewModel.HotkeyShift = false;
        viewModel.HotkeyAlt = false;
        viewModel.SaveCommand.Execute(null);

        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public void Save_WhenTimestampDisabledAndPrefixEmpty_Succeeds()
    {
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.IncludeTimestamp = false;
        viewModel.FileNamePrefix = "___";
        var closeRequested = false;
        viewModel.RequestClose += (_, _) => closeRequested = true;

        viewModel.SaveCommand.Execute(null);

        closeRequested.Should().BeTrue();
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public void Save_WhenLanguageChanged_PersistsLanguage()
    {
        var viewModel = new SettingsViewModel(_settingsService);
        viewModel.SelectedLanguage = AppLanguage.Japanese;

        viewModel.SaveCommand.Execute(null);

        _settingsService.Current.Ui.Language.Should().Be(AppLanguage.Japanese);
    }

    [Fact]
    public void FileNamePrefix_OnChange_IsTrimmedAndLengthLimited()
    {
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.FileNamePrefix = "  12345678901234567890  ";

        viewModel.FileNamePrefix.Should().Be("1234567890123456");
    }

    [Fact]
    public void FileNamePrefix_OnChange_InvalidCharsAreSanitized()
    {
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.FileNamePrefix = "  C:B/TEST  ";

        viewModel.FileNamePrefix.Should().Be("C_B_TEST");
    }

    [Fact]
    public void FileNamePreview_WhenFileNameSettingsChanged_Updates()
    {
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.FileNamePrefix = "CLIP";
        viewModel.IncludeTimestamp = false;

        viewModel.FileNamePreview.Should().Be("CLIP.png");
    }

    [Fact]
    public void FileNamePreview_WhenReservedDeviceName_UsesEscapedName()
    {
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.FileNamePrefix = "CON";
        viewModel.IncludeTimestamp = false;

        viewModel.FileNamePreview.Should().Be("_CON.png");
    }

    [Fact]
    public void FileNamePreview_WhenTimestampDisabledAndPrefixEmpty_UsesNumericSequenceOnly()
    {
        var viewModel = new SettingsViewModel(_settingsService);

        viewModel.FileNamePrefix = "___";
        viewModel.IncludeTimestamp = false;

        viewModel.FileNamePreview.Should().Be("1.png");
    }
}

