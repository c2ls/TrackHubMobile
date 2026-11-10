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

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TrackHubMobile.Interfaces.Helpers;
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;
using TrackHubMobile.Utils;

namespace TrackHubMobile.Pages;

public partial class TransporterMap : ActiveScreenComponentBase, IDisposable
{
    // Clears the search field and filter chips floating over the top of the map.
    private const int TopOverlayPixels = 130;

    // Keeps a tapped marker from ending up underneath the unit sheet.
    private const int UnitSheetPixels = 210;

    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private TransporterMapViewModel ViewModel { get; set; } = default!;
    [Inject] private ITransporterHelper TransporterHelper { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "transporterId")]
    public string? TransporterIdParam { get; set; }

    private DotNetObjectReference<TransporterMap>? selfReference;
    private bool isLoading = true;
    private bool mapInitialized;
    private string searchText = string.Empty;
    private UnitFilter filter;
    private Guid? selectedId;

    public TransporterMap(IDataRefresh refresh, NavigationManager navigationManager)
        : base(refresh, navigationManager)
    {
    }

    private PositionVm? Selected => selectedId is { } id ? ViewModel.Find(id) : null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, HandleRefreshMessage);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            try
            {
                selfReference = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("trackHubMap.initMap",
                    Array.Empty<object>(),
                    new { dotNetRef = selfReference, topInset = TopOverlayPixels, bottomInset = UnitSheetPixels });
                mapInitialized = true;
                await LoadInitialDataAsync();
            }
            catch
            {
                // JS interop can fail if the page is navigated away quickly
            }
        }
    }

    private async Task LoadInitialDataAsync()
    {
        try
        {
            await ViewModel.LoadDataAsync();

            var focused = Guid.TryParse(TransporterIdParam, out var transporterId) && ViewModel.Find(transporterId) is not null;
            await UpdateMapMarkersAsync(fitView: !focused);
            if (focused)
            {
                await SelectAsync(transporterId, center: true);
            }
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private IEnumerable<PositionVm> VisibleUnits()
    {
        var now = DateTimeOffset.UtcNow;
        return (ViewModel.Transporters ?? []).Where(t =>
            filter.Matches(ViewModel.StatusRules.DisplayStatusOf(t, now))
            && (string.IsNullOrWhiteSpace(searchText) || t.DeviceName.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task UpdateMapMarkersAsync(bool fitView)
    {
        var markers = VisibleUnits().Select(t => MapMarker.From(t, ViewModel.StatusRules)).ToArray();
        await InvokeMapAsync("trackHubMap.updateMarkers", markers, fitView);
    }

    private async Task SelectAsync(Guid? transporterId, bool center)
    {
        selectedId = transporterId;
        await InvokeMapAsync("trackHubMap.selectUnit", transporterId?.ToString(), center);
    }

    [JSInvokable]
    public async Task OnMarkerSelected(string transporterId)
    {
        if (Guid.TryParse(transporterId, out var id))
        {
            await SelectAsync(id, center: false);
            StateHasChanged();
        }
    }

    [JSInvokable]
    public async Task OnMapTapped()
    {
        await DeselectAsync();
        StateHasChanged();
    }

    private Task DeselectAsync() => SelectAsync(null, center: false);

    private Task CenterOnSelectedAsync() => SelectAsync(selectedId, center: true);

    private Task FitFleetAsync() => UpdateMapMarkersAsync(fitView: true);

    private async Task OnSearchInputAsync(ChangeEventArgs e)
    {
        searchText = e.Value?.ToString() ?? string.Empty;
        await ApplyFiltersAsync();
    }

    private async Task SetFilterAsync(UnitFilter value)
    {
        filter = value;
        await ApplyFiltersAsync();
    }

    private async Task ApplyFiltersAsync()
    {
        if (selectedId is { } id && VisibleUnits().All(t => t.TransporterId != id))
        {
            await DeselectAsync();
        }
        await UpdateMapMarkersAsync(fitView: true);
    }

    private void HandleRefreshMessage(object recipient, DataRefreshedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            ViewModel.UpdateFromRefresh(message.Value, message.Rules);
            // Keep the user's pan/zoom on periodic refreshes
            await UpdateMapMarkersAsync(fitView: false);
            StateHasChanged();
        });
    }

    private async Task InvokeMapAsync(string identifier, params object?[] args)
    {
        if (!mapInitialized) return;

        try
        {
            await JS.InvokeVoidAsync(identifier, args);
        }
        catch
        {
            // JS interop can fail during navigation
        }
    }

    public new void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<DataRefreshedMessage>(this);
        try
        {
            _ = JS.InvokeVoidAsync("trackHubMap.destroyMap");
        }
        catch
        {
            // Dispose should not throw
        }
        selfReference?.Dispose();
        base.Dispose();
    }
}
