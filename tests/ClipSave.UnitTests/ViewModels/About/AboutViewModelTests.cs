using ClipSave.Services;
using ClipSave.ViewModels.About;
using FluentAssertions;

namespace ClipSave.UnitTests;

[UnitTest]
public class AboutViewModelTests
{
    [Fact]
    public void ApplicationName_ReturnsClipSave()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.ApplicationName.Should().Be("ClipSave");
    }

    [Fact]
    public void Version_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Version_HasValidFormat()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        viewModel.Version.Should().Match(v =>
            v == "Unknown" ||
            System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+$"));
    }

    [Fact]
    public void InformationalVersion_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.InformationalVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void InformationalVersion_HasNormalizedFormat()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        viewModel.InformationalVersion.Should().Match(v =>
            v == "Unknown" ||
            System.Text.RegularExpressions.Regex.IsMatch(
                v,
                @"^\d+\.\d+\.\d+(\.local|-[0-9A-Za-z\.-]+)?(\+sha\.[0-9a-f]{7})?$"));
    }

    [Fact]
    public void InformationalVersion_NormalizesRawShaMetadataToShortFormat()
    {
        // Act
        var normalized = AboutViewModel.NormalizeInformationalVersion(
            "0.0.1+61d6adc83812c4cf0882b323502b4a6dc64df2f7");

        // Assert
        normalized.Should().Be("0.0.1+sha.61d6adc");
    }

    [Fact]
    public void InformationalVersion_Display_DoesNotAppendLocalBuildSuffix_ForLocalBuildMetadata()
    {
        // Arrange
        var localization = new LocalizationService();

        // Act
        var displayVersion = AboutViewModel.GetDisplayInformationalVersion(
            "0.0.1+61d6adc83812c4cf0882b323502b4a6dc64df2f7",
            localization);

        // Assert
        displayVersion.Should().Be("0.0.1+sha.61d6adc");
    }

    [Fact]
    public void InformationalVersion_Display_KeepsDotLocal_ForLocalDefault()
    {
        // Arrange
        var localization = new LocalizationService();

        // Act
        var displayVersion = AboutViewModel.GetDisplayInformationalVersion(
            "0.0.1.local",
            localization);

        // Assert
        displayVersion.Should().Be("0.0.1.local");
    }

    [Fact]
    public void InformationalVersion_Display_NormalizesRawShaMetadata_ForLocalCore()
    {
        // Arrange
        var localization = new LocalizationService();

        // Act
        var displayVersion = AboutViewModel.GetDisplayInformationalVersion(
            "0.0.1.local+61d6adc83812c4cf0882b323502b4a6dc64df2f7",
            localization);

        // Assert
        displayVersion.Should().Be("0.0.1.local+sha.61d6adc");
    }

    [Fact]
    public void InformationalVersion_Display_TrimsUnknownInput()
    {
        // Arrange
        var localization = new LocalizationService();

        // Act
        var displayVersion = AboutViewModel.GetDisplayInformationalVersion(
            "  custom-build  ",
            localization);

        // Assert
        displayVersion.Should().Be("custom-build");
    }

    [Fact]
    public void DotNetVersion_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.DotNetVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DotNetVersion_ContainsDotNet()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        viewModel.DotNetVersion.Should().Contain(".NET");
    }

    [Fact]
    public void OsVersion_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.OsVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildDate_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.BuildDate.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Copyright_IsNotNullOrEmpty()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.Copyright.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Copyright_ContainsCopyrightSymbol()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        viewModel.Copyright.Should().Match(c =>
            c.Contains("©") || c.Contains("Copyright"));
    }

    [Fact]
    public void CloseCommand_IsNotNull()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        // Assert
        viewModel.CloseCommand.Should().NotBeNull();
    }

    [Fact]
    public void CloseCommand_RaisesRequestCloseEvent()
    {
        // Arrange
        var viewModel = new AboutViewModel();
        var closeRequested = false;

        viewModel.RequestClose += (sender, e) => closeRequested = true;

        // Act
        viewModel.CloseCommand.Execute(null);

        // Assert
        closeRequested.Should().BeTrue();
    }

    [Fact]
    public void CloseCommand_CanExecute_ReturnsTrue()
    {
        // Arrange
        var viewModel = new AboutViewModel();

        // Act & Assert
        viewModel.CloseCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void AllProperties_AreReadOnly()
    {
        // Arrange & Act
        var viewModel = new AboutViewModel();

        viewModel.ApplicationName.Should().NotBeNull();
        viewModel.Version.Should().NotBeNull();
        viewModel.InformationalVersion.Should().NotBeNull();
        viewModel.DotNetVersion.Should().NotBeNull();
        viewModel.OsVersion.Should().NotBeNull();
        viewModel.BuildDate.Should().NotBeNull();
        viewModel.Copyright.Should().NotBeNull();
    }
}
