using ClipSave.Infrastructure;
using ClipSave.Services;
using FluentAssertions;
using System.Globalization;

namespace ClipSave.UnitTests;

[UnitTest]
public class LocalizationServiceTests
{
    [Fact]
    public void SetLanguage_WhenLanguageCodeUnchanged_ReappliesCultureToCurrentThread()
    {
        var originalCurrentUiCulture = CultureInfo.CurrentUICulture;
        var originalCurrentCulture = CultureInfo.CurrentCulture;
        var originalThreadUiCulture = Thread.CurrentThread.CurrentUICulture;
        var originalThreadCulture = Thread.CurrentThread.CurrentCulture;
        var originalDefaultThreadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var originalDefaultThreadCulture = CultureInfo.DefaultThreadCurrentCulture;

        try
        {
            var localizer = new LocalizationService();
            localizer.SetLanguage(AppLanguage.English);

            var japanese = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentUICulture = japanese;
            CultureInfo.CurrentCulture = japanese;
            Thread.CurrentThread.CurrentUICulture = japanese;
            Thread.CurrentThread.CurrentCulture = japanese;

            var changed = localizer.SetLanguage(AppLanguage.English);

            changed.Should().BeFalse();
            CultureInfo.CurrentUICulture.Name.Should().Be("en-US");
            CultureInfo.CurrentCulture.Name.Should().Be("en-US");
            localizer.GetString("Common_Save").Should().Be("Save");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCurrentUiCulture;
            CultureInfo.CurrentCulture = originalCurrentCulture;
            Thread.CurrentThread.CurrentUICulture = originalThreadUiCulture;
            Thread.CurrentThread.CurrentCulture = originalThreadCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultThreadUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultThreadCulture;
        }
    }

    [Fact]
    public void GetString_UsesConfiguredLanguage_WhenThreadCultureDiffers()
    {
        var originalCurrentUiCulture = CultureInfo.CurrentUICulture;
        var originalCurrentCulture = CultureInfo.CurrentCulture;
        var originalThreadUiCulture = Thread.CurrentThread.CurrentUICulture;
        var originalThreadCulture = Thread.CurrentThread.CurrentCulture;
        var originalDefaultThreadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var originalDefaultThreadCulture = CultureInfo.DefaultThreadCurrentCulture;

        try
        {
            var localizer = new LocalizationService();
            localizer.SetLanguage(AppLanguage.English);

            var japanese = CultureInfo.GetCultureInfo("ja-JP");
            CultureInfo.CurrentUICulture = japanese;
            CultureInfo.CurrentCulture = japanese;
            Thread.CurrentThread.CurrentUICulture = japanese;
            Thread.CurrentThread.CurrentCulture = japanese;

            localizer.GetString("Common_Save").Should().Be("Save");
            localizer.Format("Notification_SaveCompleted", "test.txt").Should().Be("Saved: test.txt");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCurrentUiCulture;
            CultureInfo.CurrentCulture = originalCurrentCulture;
            Thread.CurrentThread.CurrentUICulture = originalThreadUiCulture;
            Thread.CurrentThread.CurrentCulture = originalThreadCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultThreadUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultThreadCulture;
        }
    }
}

