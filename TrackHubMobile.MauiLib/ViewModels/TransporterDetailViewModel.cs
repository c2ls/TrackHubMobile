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

public partial class TransporterDetailViewModel(IRouter router, IDataRefresh dataRefresh) : BaseViewModel
{
    [ObservableProperty]
    private PositionVm? transporter;
    [ObservableProperty]
    private bool hasError;

    public UnitStatusRules StatusRules => dataRefresh.StatusRules;

    public async Task LoadAsync(Guid transporterId)
    {
        HasError = false;

        // The view model outlives the page: drop the previous unit, and show the fleet's
        // cached position while the full details load.
        if (Transporter?.TransporterId != transporterId)
        {
            var cached = dataRefresh.Transporters.FirstOrDefault(t => t.TransporterId == transporterId);
            Transporter = cached.TransporterId != Guid.Empty ? cached : null;
        }

        try
        {
            var result = await router.GetDeviceAsync(transporterId, CancellationToken.None);
            if (result.DeviceDateTime != default)
            {
                Transporter = result;
            }
        }
        catch
        {
        }

        // A failed or empty answer keeps whatever is already on screen
        HasError = Transporter is null;
    }
}
