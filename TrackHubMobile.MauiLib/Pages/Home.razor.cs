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
using TrackHubMobile.Models;

namespace TrackHubMobile.Pages;

public partial class Home(
    IDataRefresh refresh,
    NavigationManager navigationManager) : ActiveScreenComponentBase(refresh, navigationManager)
{

    private void OpenList(UnitFilter filter) => Navigation.NavigateTo($"/listview?status={filter}");

    private void OpenMap() => Navigation.NavigateTo("/mapview");

    private void OpenUnit(Guid transporterId) => Navigation.NavigateTo($"/transporter/{transporterId}");

    protected override void OnInitialized()
    {
        // The base subscribes to LocationChanged, which is the only thing that ever marks the screen
        // inactive. Without it the 30 s position refresh keeps running after the driver navigates away.
        base.OnInitialized();
        ViewModel.OnUpdated = StateHasChanged;
    }

}
