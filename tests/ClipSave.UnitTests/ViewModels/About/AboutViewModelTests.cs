using ClipSave.ViewModels.About;
using FluentAssertions;

namespace ClipSave.UnitTests;

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
            System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+\.\d+$"));
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
        viewModel.DotNetVersion.Should().NotBeNull();
        viewModel.OsVersion.Should().NotBeNull();
        viewModel.BuildDate.Should().NotBeNull();
        viewModel.Copyright.Should().NotBeNull();
    }
}
