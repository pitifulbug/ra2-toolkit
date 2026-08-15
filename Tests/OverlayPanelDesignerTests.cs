using System.Runtime.ExceptionServices;
using System.Windows.Forms;

public sealed class OverlayPanelDesignerTests
{
    [Fact]
    public void MapControls_ContainOnlySupportedFeatures()
    {
        RunInSta(() =>
        {
            using var panel = new OverlayPanel();
            var controls = Descendants(panel).ToArray();
            var mapGroup = controls.Single(control => control.Name == "mapGroupBox");
            var mapControlNames = mapGroup.Controls.Cast<Control>()
                .Select(control => control.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.Contains(controls, control => control.Name == "revealMapCheckBox");
            Assert.Contains(controls,
                control => control.Name == "disableGapGeneratorsCheckBox");
            Assert.Equal(
                new[]
                {
                    "crateCheckBox",
                    "crateHotkeyButton",
                    "crateRouteLinesCheckBox",
                    "crateRouteLinesHotkeyButton",
                    "disableGapGeneratorsCheckBox",
                    "disableGapGeneratorsHotkeyButton",
                    "revealMapCheckBox",
                    "revealMapHotkeyButton"
                },
                mapControlNames);
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)),
            "WinForms test did not finish within 10 seconds.");
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
