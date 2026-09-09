using cpms_Domain.Models;

namespace cpms_Tests;

public class PhaseDomainTests
{
    [Fact]
    public void UpdatePlan_AppliesValidValues()
    {
        var phase = OpenPhase();

        phase.UpdatePlan("  Foundation  ", "  Groundwork  ", 2,
            new DateTime(2026, 9, 2), new DateTime(2026, 9, 20));

        Assert.Equal("Foundation", phase.Name);
        Assert.Equal("Groundwork", phase.Description);
        Assert.Equal(2, phase.SequenceOrder);
        Assert.Equal(new DateTime(2026, 9, 2), phase.BaselineStart);
        Assert.Equal(new DateTime(2026, 9, 20), phase.BaselineEnd);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdatePlan_RejectsEmptyName(string? name)
    {
        var phase = OpenPhase();

        Assert.Throws<ArgumentException>(() => phase.UpdatePlan(name!, null, 0,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
    }

    [Fact]
    public void UpdatePlan_RejectsNegativeSequence()
    {
        var phase = OpenPhase();

        Assert.Throws<ArgumentException>(() => phase.UpdatePlan("Foundation", null, -1,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
    }

    [Fact]
    public void UpdatePlan_RejectsEndBeforeStart()
    {
        var phase = OpenPhase();

        Assert.Throws<ArgumentException>(() => phase.UpdatePlan("Foundation", null, 0,
            new DateTime(2026, 9, 30), new DateTime(2026, 9, 1)));
    }

    [Theory]
    [InlineData(PhaseStatus.COMPLETED)]
    [InlineData(PhaseStatus.CANCELLED)]
    public void UpdatePlan_RejectsClosedPhases(PhaseStatus status)
    {
        var phase = OpenPhase();
        phase.Status = status;

        Assert.Throws<InvalidOperationException>(() => phase.UpdatePlan("Foundation", null, 0,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)));
    }

    [Fact]
    public void Cancel_SetsCancelledFromPlanned()
    {
        var phase = OpenPhase();

        phase.Cancel();

        Assert.Equal(PhaseStatus.CANCELLED, phase.Status);
    }

    [Fact]
    public void Cancel_RejectsCompletedPhase()
    {
        var phase = OpenPhase();
        phase.Status = PhaseStatus.COMPLETED;

        Assert.Throws<InvalidOperationException>(phase.Cancel);
    }

    [Fact]
    public void Cancel_RejectsAlreadyCancelledPhase()
    {
        var phase = OpenPhase();
        phase.Cancel();

        Assert.Throws<InvalidOperationException>(phase.Cancel);
    }

    private static Phase OpenPhase() => new()
    {
        PhaseId = 1,
        ProjectId = 1,
        Name = "Foundation",
        SequenceOrder = 0,
        BaselineStart = new DateTime(2026, 9, 1),
        BaselineEnd = new DateTime(2026, 9, 30),
        Status = PhaseStatus.PLANNED
    };
}
