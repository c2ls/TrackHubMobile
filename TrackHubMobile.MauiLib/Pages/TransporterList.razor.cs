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
using TrackHubMobile.Interfaces.Services;
using TrackHubMobile.Messages;
using TrackHubMobile.Models;

namespace TrackHubMobile.Pages;

public partial class TransporterList(
    IDataRefresh refresh,
    NavigationManager navigationManager) : ActiveScreenComponentBase(refresh, navigationManager), IDisposable
{
    [SupplyParameterFromQuery(Name = "status")]
    public string? StatusParam { get; set; }

    private (bool Applied, string? Status) routeFilter;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        WeakReferenceMessenger.Default.Register<DataRefreshedMessage>(this, HandleRefreshMessage);
    }

    protected override void OnParametersSet()
    {
        if (routeFilter != (true, StatusParam))
        {
            routeFilter = (true, StatusParam);
            ViewModel.SetFilter(Enum.TryParse<UnitFilter>(StatusParam, ignoreCase: true, out var filter) ? filter : UnitFilter.All);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await ViewModel.LoadDataAsync();
            StateHasChanged();
        }
    }

    private void HandleRefreshMessage(object recipient, DataRefreshedMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ViewModel.UpdateFromRefresh(message.Value, message.Rules);
            StateHasChanged();
        });
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        ViewModel.OnSearchChanged(e.Value?.ToString() ?? string.Empty);
    }

    private void OpenUnit(Guid transporterId) => Navigation.NavigateTo($"/transporter/{transporterId}");

    public new void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<DataRefreshedMessage>(this);
        base.Dispose();
    }
}
