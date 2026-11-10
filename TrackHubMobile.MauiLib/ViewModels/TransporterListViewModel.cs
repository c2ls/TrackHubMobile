// Copyright (c) 2025 Sergio Hernandez. All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License").
//  You may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Models;

namespace TrackHubMobile.ViewModels;

public partial class TransporterListViewModel(IDataRefresh dataRefresh) : BaseViewModel
{
    [ObservableProperty]
    private IEnumerable<PositionVm>? transporters = null;
    [ObservableProperty]
    private bool isRefreshing;
    [ObservableProperty]
    private string searchText = string.Empty;

    // Materialized once per refresh or search, not per render; Virtualize needs a collection.
    public List<UnitRow>? FilteredTransporters { get; private set; }

    public UnitFilter Filter { get; private set; }

    public IReadOnlyDictionary<UnitFilter, int> Counts { get; private set; } = new Dictionary<UnitFilter, int>();

    public UnitStatusRules StatusRules { get; private set; } = UnitStatusRules.Default;

    public async Task LoadDataAsync()
    {
        if (dataRefresh.Transporters.Any())
        {
            UpdateFromRefresh(dataRefresh.Transporters, dataRefresh.StatusRules);
            return;
        }

        IsRefreshing = true;
        try
        {
            await dataRefresh.ForceRefreshAsync();
            UpdateFromRefresh(dataRefresh.Transporters, dataRefresh.StatusRules);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void UpdateFromRefresh(IEnumerable<PositionVm> transporters, UnitStatusRules rules)
    {
        StatusRules = rules;
        Transporters = transporters;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var now = DateTimeOffset.UtcNow;
        var rows = Transporters?.Select(t => new UnitRow(t, StatusRules.DisplayStatusOf(t, now))).ToList();

        Counts = Enum.GetValues<UnitFilter>().ToDictionary(f => f, f => rows?.Count(r => f.Matches(r.Status)) ?? 0);
        FilteredTransporters = rows?
            .Where(r => Filter.Matches(r.Status)
                && (string.IsNullOrWhiteSpace(SearchText) || r.Unit.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(r => r.Status)
            .ThenBy(r => r.Unit.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        OnPropertyChanged(nameof(FilteredTransporters));
    }

    public void SetFilter(UnitFilter filter)
    {
        Filter = filter;
        ApplyFilter();
    }

    public void OnSearchChanged(string value)
    {
        SearchText = value;
        ApplyFilter();
    }
}

public readonly record struct UnitRow(PositionVm Unit, UnitDisplayStatus Status);
