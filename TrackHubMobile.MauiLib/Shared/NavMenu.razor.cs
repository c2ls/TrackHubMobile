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

using Microsoft.AspNetCore.Components.Routing;

namespace TrackHubMobile.Shared;

public partial class NavMenu
{
    protected override void OnInitialized()
    {
        Navigation.LocationChanged += HandleLocationChanged;
    }

    private string ActiveClass(string section) => section == CurrentSection ? "active" : string.Empty;

    private string CurrentSection
    {
        get
        {
            var path = Navigation.ToBaseRelativePath(Navigation.Uri);
            if (path.StartsWith("mapview", StringComparison.OrdinalIgnoreCase)) return "map";
            if (path.StartsWith("listview", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("transporter", StringComparison.OrdinalIgnoreCase)) return "units";
            if (path.StartsWith("about", StringComparison.OrdinalIgnoreCase)) return "account";
            return "home";
        }
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e) => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Navigation.LocationChanged -= HandleLocationChanged;
    }
}
