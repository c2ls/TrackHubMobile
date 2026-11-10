using TrackHubMobile.Models;
using TrackHubMobile.ViewModels;

namespace TrackHubMobile.Tests;

[TestFixture]
public class TransporterListViewModelTests
{
    private static readonly UnitStatusRules Rules = UnitStatusRules.FromOnlineInterval(10);

    private static TransporterListViewModel Loaded()
    {
        var viewModel = new TransporterListViewModel(null!);
        viewModel.UpdateFromRefresh(
        [
            Unit("Zeta", minutesAgo: 60, speed: 0),
            Unit("Bravo", minutesAgo: 1, speed: 0),
            Unit("Alpha", minutesAgo: 1, speed: 40),
            Unit("Delta", minutesAgo: 1, speed: 120),
            Unit("Charlie", minutesAgo: 1, speed: 30)
        ], Rules);
        return viewModel;
    }

    [Test]
    public void Rows_AreOrderedByStatusThenName()
        => Assert.That(Loaded().FilteredTransporters!.Select(r => r.Unit.DeviceName),
            Is.EqualTo(new[] { "Delta", "Alpha", "Charlie", "Bravo", "Zeta" }));

    [Test]
    public void Counts_CoverTheWholeFleet_WhateverIsFiltered()
    {
        var viewModel = Loaded();
        viewModel.OnSearchChanged("alp");
        viewModel.SetFilter(UnitFilter.Offline);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.FilteredTransporters, Is.Empty);
            Assert.That(viewModel.Counts[UnitFilter.All], Is.EqualTo(5));
            Assert.That(viewModel.Counts[UnitFilter.Moving], Is.EqualTo(3));
            Assert.That(viewModel.Counts[UnitFilter.Speeding], Is.EqualTo(1));
            Assert.That(viewModel.Counts[UnitFilter.Stopped], Is.EqualTo(1));
            Assert.That(viewModel.Counts[UnitFilter.Offline], Is.EqualTo(1));
        });
    }

    [Test]
    public void FilterAndSearch_Combine()
    {
        var viewModel = Loaded();
        viewModel.SetFilter(UnitFilter.Moving);
        viewModel.OnSearchChanged("l");

        Assert.That(viewModel.FilteredTransporters!.Select(r => r.Unit.DeviceName),
            Is.EqualTo(new[] { "Delta", "Alpha", "Charlie" }));
    }

    private static PositionVm Unit(string name, int minutesAgo, double speed)
        => new(Guid.NewGuid(), name, "TRUCK", 0, 0, null, DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), speed,
            null, null, null, null, null, null, null);
}
