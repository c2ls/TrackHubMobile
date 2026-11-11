window.trackHubMap = {
    map: null,
    clusterGroup: null,
    markers: {},
    trackLayer: null,
    hasFittedView: false,
    selectedId: null,
    dotNetRef: null,
    topInset: 50,
    bottomInset: 50,

    // options: { zoomPosition, attributionPosition, dotNetRef, topInset, bottomInset, interactive }
    // zoomPosition: omitted on screens where the phone's pinch is enough and the corners
    // are taken by overlays. topInset/bottomInset keep markers clear of the overlays.
    initMap: function (positions, options) {
        if (this.map) {
            this.destroyMap();
        }

        options = options || {};
        this.hasFittedView = false;
        this.dotNetRef = options.dotNetRef || null;
        this.topInset = options.topInset || 50;
        this.bottomInset = options.bottomInset || 50;

        var interactive = options.interactive !== false;
        this.map = L.map('map', {
            zoomControl: false,
            attributionControl: false,
            dragging: interactive,
            touchZoom: interactive,
            doubleClickZoom: interactive,
            scrollWheelZoom: interactive,
            boxZoom: interactive,
            keyboard: interactive
        }).setView([14.6349, -90.5069], 10);

        L.tileLayer('https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png', {
            attribution: '&copy; <a href="https://carto.com/">CARTO</a> &copy; <a href="https://osm.org/">OSM</a>',
            maxZoom: 20,
            subdomains: 'abcd'
        }).addTo(this.map);

        L.control.attribution({ position: options.attributionPosition || 'bottomleft', prefix: false }).addTo(this.map);

        if (options.zoomPosition) {
            L.control.zoom({ position: options.zoomPosition }).addTo(this.map);
        }

        var self = this;
        this.map.on('click', function () {
            if (self.dotNetRef && self.selectedId) {
                self.dotNetRef.invokeMethodAsync('OnMapTapped');
            }
        });

        this.clusterGroup = L.markerClusterGroup({
            maxClusterRadius: 45,
            spiderfyOnMaxZoom: true,
            showCoverageOnHover: false,
            animate: true,
            iconCreateFunction: function (cluster) {
                var count = cluster.getChildCount();
                var size = count < 10 ? 'small' : count < 50 ? 'medium' : 'large';
                var sizes = { small: 36, medium: 44, large: 52 };
                var colors = { small: '#0078d4', medium: '#005fa3', large: '#003d6b' };
                return L.divIcon({
                    html: '<div class="cluster-icon cluster-' + size + '" style="' +
                        'background:' + colors[size] + ';' +
                        'width:' + sizes[size] + 'px;height:' + sizes[size] + 'px;' +
                        'border-radius:50%;color:#fff;display:flex;align-items:center;justify-content:center;' +
                        'font-weight:700;font-size:' + (size === 'large' ? '15' : '13') + 'px;' +
                        'box-shadow:0 3px 10px rgba(0,0,0,0.25),0 0 0 4px rgba(0,120,212,0.2);' +
                        'border:2.5px solid rgba(255,255,255,0.9);">' +
                        count + '</div>',
                    className: 'custom-cluster',
                    iconSize: L.point(sizes[size], sizes[size])
                });
            }
        });
        this.map.addLayer(this.clusterGroup);

        if (positions && positions.length > 0) {
            this.updateMarkers(positions, true);
        }
    },

    // Markers are kept per unit and updated in place, so a refresh keeps the user's
    // pan/zoom. The view is fitted to the fleet only on the first load (or when
    // fitView is true).
    updateMarkers: function (positions, fitView) {
        if (!this.map || !this.clusterGroup) return;

        positions = positions || [];
        this._syncMarkers(positions);

        if (positions.length === 0 || (!fitView && this.hasFittedView)) return;

        var bounds = positions.map(function (p) { return [p.lat, p.lng]; });
        if (bounds.length === 1) {
            this.map.setView(bounds[0], 15);
        } else {
            this.map.fitBounds(bounds, { paddingTopLeft: [50, this.topInset], paddingBottomRight: [50, 50] });
        }
        this.hasFittedView = true;
    },

    // Highlights one unit's marker (null clears it); center also brings it into view,
    // zooming past any cluster that hides it.
    selectUnit: function (transporterId, center) {
        if (!this.map) return;

        var previous = this.markers[this.selectedId];
        this.selectedId = transporterId;
        if (previous) this._refreshIcon(previous);

        var marker = this.markers[transporterId];
        if (!marker) return;
        this._refreshIcon(marker);

        if (center) {
            var map = this.map;
            map.setView(marker.getLatLng(), Math.max(map.getZoom(), 16));
            this.clusterGroup.zoomToShowLayer(marker, function () {
                map.panTo(marker.getLatLng());
            });
            this.hasFittedView = true;
        } else {
            this.map.panInside(marker.getLatLng(), {
                paddingTopLeft: [40, this.topInset],
                paddingBottomRight: [40, this.bottomInset]
            });
        }
    },

    _syncMarkers: function (positions) {
        var current = {};
        var added = [];
        for (var i = 0; i < positions.length; i++) {
            var p = positions[i];
            current[p.transporterId] = true;
            var marker = this.markers[p.transporterId];
            if (marker) {
                this._updateMarker(marker, p);
            } else {
                marker = this._createMarker(p);
                this.markers[p.transporterId] = marker;
                added.push(marker);
            }
        }

        var removed = [];
        var ids = Object.keys(this.markers);
        for (var j = 0; j < ids.length; j++) {
            if (!current[ids[j]]) {
                removed.push(this.markers[ids[j]]);
                delete this.markers[ids[j]];
            }
        }

        if (removed.length > 0) this.clusterGroup.removeLayers(removed);
        if (added.length > 0) this.clusterGroup.addLayers(added);
    },

    // The cluster group re-clusters a moved marker itself (it listens to the marker's
    // move event), and cluster icons depend only on child counts, so no refreshClusters.
    _updateMarker: function (marker, p) {
        var latLng = marker.getLatLng();
        if (latLng.lat !== p.lat || latLng.lng !== p.lng) {
            marker.setLatLng([p.lat, p.lng]);
        }

        marker.thPosition = p;
        this._refreshIcon(marker);
    },

    _refreshIcon: function (marker) {
        var iconKey = this._iconKey(marker.thPosition);
        if (marker.thIconKey !== iconKey) {
            marker.setIcon(this._markerIcon(marker.thPosition));
            marker.thIconKey = iconKey;
        }
    },

    destroyMap: function () {
        this.clearTrack();
        if (this.clusterGroup) {
            this.clusterGroup.clearLayers();
            this.clusterGroup = null;
        }
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        this.markers = {};
        this.hasFittedView = false;
        this.selectedId = null;
        this.dotNetRef = null;
    },

    // Draws a track polyline with start/end markers and fits the map to it.
    // A single point renders as one stop marker.
    // points: [{ lat, lng, speed, dateTime }], options: { color, weight, dashed, bottomInsetRatio }
    drawTrack: function (points, options) {
        if (!this.map) return;

        this.clearTrack();
        if (!points || points.length === 0) return;

        options = options || {};
        this.trackLayer = L.layerGroup().addTo(this.map);

        if (points.length === 1) {
            this.trackLayer.addLayer(this._stopMarker(points[0], '#ef4444', 18));
            this.map.setView([points[0].lat, points[0].lng], 16);
            return;
        }

        var polyline = this._segmentPolyline(points, options);
        this.trackLayer.addLayer(polyline);
        this.trackLayer.addLayer(this._endpointMarker(points[0], '#22c55e'));
        this.trackLayer.addLayer(this._endpointMarker(points[points.length - 1], '#ef4444'));

        this._fitTrack(polyline.getBounds(), options);
    },

    // Draws a whole range at once: one polyline per moving segment, a dot per stop and a
    // dashed line per stretch without a driven track, fitted to everything.
    // segments, dashed: [[{ lat, lng, speed, dateTime }, ...], ...]
    drawTracks: function (segments, stops, dashed, options) {
        if (!this.map) return;

        this.clearTrack();
        segments = segments || [];
        stops = stops || [];
        dashed = dashed || [];
        if (segments.length === 0 && stops.length === 0 && dashed.length === 0) return;

        options = options || {};
        this.trackLayer = L.layerGroup().addTo(this.map);

        var bounds = L.latLngBounds([]);
        var firstPoint = null;
        var lastPoint = null;

        for (var i = 0; i < segments.length; i++) {
            var seg = segments[i];
            if (!seg || seg.length === 0) continue;

            if (seg.length > 1) {
                var line = this._segmentPolyline(seg, options);
                this.trackLayer.addLayer(line);
                bounds.extend(line.getBounds());
            } else {
                bounds.extend([seg[0].lat, seg[0].lng]);
            }

            if (!firstPoint) firstPoint = seg[0];
            lastPoint = seg[seg.length - 1];
        }

        for (var k = 0; k < dashed.length; k++) {
            var gap = this._segmentPolyline(dashed[k], { color: '#7f7f7f', weight: 3, dashed: true });
            this.trackLayer.addLayer(gap);
            bounds.extend(gap.getBounds());
        }

        for (var j = 0; j < stops.length; j++) {
            this.trackLayer.addLayer(this._stopMarker(stops[j], '#f59e0b', 12));
            bounds.extend([stops[j].lat, stops[j].lng]);
        }

        if (firstPoint) {
            this.trackLayer.addLayer(this._endpointMarker(firstPoint, '#22c55e'));
        }
        if (lastPoint && lastPoint !== firstPoint) {
            this.trackLayer.addLayer(this._endpointMarker(lastPoint, '#ef4444'));
        }

        if (bounds.isValid()) {
            this._fitTrack(bounds, options);
        }
    },

    // Re-measures the container after the surrounding layout changed (a filter panel
    // opening or closing), otherwise Leaflet keeps rendering at the stale size.
    resize: function () {
        if (this.map) {
            this.map.invalidateSize();
        }
    },

    // Re-fits the drawn track, used when the space left over by an overlay changes.
    refit: function (options) {
        if (!this.map || !this.trackLayer) return;

        var bounds = L.latLngBounds([]);
        this.trackLayer.eachLayer(function (layer) {
            if (layer.getBounds) {
                bounds.extend(layer.getBounds());
            } else if (layer.getLatLng) {
                bounds.extend(layer.getLatLng());
            }
        });

        if (bounds.isValid()) {
            this._fitTrack(bounds, options || {});
        }
    },

    clearTrack: function () {
        if (this.trackLayer) {
            if (this.map) {
                this.map.removeLayer(this.trackLayer);
            }
            this.trackLayer = null;
        }
    },

    _segmentPolyline: function (points, options) {
        var latlngs = [];
        for (var i = 0; i < points.length; i++) {
            latlngs.push([points[i].lat, points[i].lng]);
        }
        return L.polyline(latlngs, {
            color: options.color || (options.dashed ? '#7f7f7f' : '#0078d4'),
            weight: options.weight || 4,
            dashArray: options.dashed ? '4 8' : null,
            opacity: 0.85,
            lineJoin: 'round',
            lineCap: 'round'
        });
    },

    _endpointMarker: function (point, color) {
        return this._stopMarker(point, color, 18);
    },

    _stopMarker: function (point, color, size) {
        var marker = L.marker([point.lat, point.lng], {
            icon: this._trackEndpointIcon(color, size)
        });
        if (point.dateTime) {
            marker.bindPopup(this._trackEndpointPopup(point));
        }
        return marker;
    },

    // Keeps the fitted track clear of whatever overlays the bottom of the map.
    _fitTrack: function (bounds, options) {
        var inset = 40;
        if (options.bottomInsetRatio) {
            inset = Math.max(40, Math.round(this.map.getSize().y * options.bottomInsetRatio));
        }
        // A single point has no extent to fit; recentring it would jump to max zoom
        if (bounds.getNorthEast().equals(bounds.getSouthWest())) {
            this.map.setView(bounds.getCenter(), this.map.getZoom());
            return;
        }
        this.map.fitBounds(bounds, {
            paddingTopLeft: [40, 40],
            paddingBottomRight: [40, inset]
        });
    },

    _trackEndpointIcon: function (color, size) {
        size = size || 18;
        return L.divIcon({
            className: 'custom-marker',
            html: '<div style="' +
                'width:' + size + 'px;height:' + size + 'px;' +
                'background:' + color + ';' +
                'border:2.5px solid rgba(255,255,255,0.95);' +
                'border-radius:50%;' +
                'box-shadow:0 2px 6px rgba(0,0,0,0.3);"></div>',
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2],
            popupAnchor: [0, -(size / 2 + 3)]
        });
    },

    _trackEndpointPopup: function (p) {
        var html = '<div class="th-popup-content"><div class="th-popup-body">';
        html += '<div class="th-popup-row"><i class="fas fa-clock"></i><span>' +
            new Date(p.dateTime).toLocaleString() + '</span></div>';
        if (p.speed !== null && p.speed !== undefined) {
            html += '<div class="th-popup-row"><i class="fas fa-tachometer-alt"></i><span>' +
                Number(p.speed).toFixed(1) + ' km/h</span></div>';
        }
        html += '</div></div>';
        return html;
    },

    _statusColors: {
        moving:   { bg: '#1f9d55', ring: 'rgba(31,157,85,0.25)' },
        stopped:  { bg: '#d98a00', ring: 'rgba(217,138,0,0.25)' },
        offline:  { bg: '#7b8594', ring: 'rgba(123,133,148,0.25)' },
        speeding: { bg: '#d6364a', ring: 'rgba(214,54,74,0.25)' }
    },

    _createMarker: function (p) {
        var self = this;
        var marker = L.marker([p.lat, p.lng], { icon: this._markerIcon(p) });
        marker.thPosition = p;
        marker.thIconKey = this._iconKey(p);
        marker.on('click', function () {
            if (self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('OnMarkerSelected', p.transporterId);
            }
        });
        return marker;
    },

    _iconKey: function (p) {
        return p.status + '|' + (p.speed > 0 ? (p.course || 0) : 'still') + '|' + (p.transporterId === this.selectedId);
    },

    _markerIcon: function (p) {
        var c = this._statusColors[p.status];
        var selected = p.transporterId === this.selectedId;
        var size = selected ? 40 : 32;

        var innerCircle;
        if (p.speed > 0) {
            innerCircle = '<svg viewBox="0 0 24 24" width="14" height="14" style="transform:rotate(' + (p.course || 0) + 'deg)">' +
                '<path d="M12 2 L18 18 L12 14 L6 18 Z" fill="white" opacity="0.95"/></svg>';
        } else {
            innerCircle = '<div style="width:7px;height:7px;border-radius:50%;background:white;opacity:0.9;"></div>';
        }

        return L.divIcon({
            className: 'custom-marker',
            html: '<div style="' +
                'width:' + size + 'px;height:' + size + 'px;' +
                'background:' + c.bg + ';' +
                'border:2.5px solid rgba(255,255,255,0.95);' +
                'border-radius:50%;' +
                'display:flex;align-items:center;justify-content:center;' +
                'box-shadow:0 2px 8px rgba(0,0,0,0.3),0 0 0 ' + (selected ? '5px #0078d4' : '4px ' + c.ring) + ';">' +
                innerCircle +
                '</div>',
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2]
        });
    }
};
