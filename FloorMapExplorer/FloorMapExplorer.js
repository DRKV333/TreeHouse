import { Map, TileLayer, CRS, Projection, Transformation, Point, GeoJSON, CircleMarker, Icon, Marker } from "leaflet";
import { BurgerMenuControl } from "leaflet-burgermenu";

class SelectableMarker extends Marker {
    #icon;
    #iconSelected;

    constructor(latlng, icon, iconSelected) {
        super(latlng, { icon: icon });

        this.#icon = icon;
        this.#iconSelected = iconSelected;
    }

    setSelected(selected) {
        if (selected) {
            this.setIcon(this.#iconSelected);
        } else {
            this.setIcon(this.#icon);
        }
    }
}

const MarkerColor = "#3388ff";
const MarkerSelectedColor = "#ff3300";

class SelectableCicleMarker extends CircleMarker {
    constructor(latlng) {
        super(latlng, { radius: 5, color: MarkerColor });
    }

    setSelected(selected) {
        if (selected) {
            this.setStyle({ color: MarkerSelectedColor });
        } else {
            this.setStyle({ color: MarkerColor });
        }
    }
}

export class FloorMapExplorer {
    #portalIcon = this.#createIcon("icons/portal.svg");
    #vendorIcon = this.#createIcon("icons/shopping-cart.svg");

    #element;
    #map;
    #menu;

    #infoJson;

    #selectionEnabled = false;
    #selectedMarker = null;
    #selectionListeners = [];

    constructor(element) {
        this.#element = element;
        this.#map = new Map(element);
    }

    async loadInfo() {
        const infoResponse = await fetch("data/MapInfo.json");
        this.#infoJson = await infoResponse.json();

        const menuItems = Object.keys(this.#infoJson).sort().map((packageName) => {
            const zones = this.#infoJson[packageName];
            const nameTrimmed = packageName.replace("_P_FloorMaps", "");
            const zoneNames = Object.keys(zones).sort();

            if (zoneNames.length == 1)
            {
                const zoneName = zoneNames[0];
                return {
                    title: nameTrimmed == zoneName ? nameTrimmed : `${nameTrimmed} - ${zoneName}`,
                    onClick: () => { this.loadMap(packageName, zoneName); }
                };
            }
            else
            {
                const zoneItems = zoneNames.map((zoneName) => ({
                    title: zoneName,
                    onClick: () => { this.loadMap(packageName, zoneName); }
                }));

                return { title: nameTrimmed, menuItems: zoneItems };
            }
        });

        this.#menu = new BurgerMenuControl({
            title: "main",
            menuItems: menuItems
        });
        this.#menu.addTo(this.#map);
    }

    async loadMap(packageName, zoneName) {
        const packageInfo = this.#infoJson[packageName];
        if (packageInfo == undefined)
            return false;

        const info = packageInfo[zoneName];
        if (info == undefined)
            return false;

        const mapsPath = `data/maps/${packageName}/${zoneName}`;

        const crs = this.#createCRS(info);

        this.#map.remove();
        this.#selectedMarker = null;
        this.#map = new Map(this.#element, { crs: crs })

        this.#menu.addTo(this.#map);

        const imageMax = crs.pointToLatLng(new Point(info.TileWidth, 0), 0);

        new TileLayer(`${mapsPath}/{z}/{x}/{y}.png`, {
            tileSize: [info.TileWidth, info.TileHeight],
            bounds: [[info.MinX + 1, info.MinY + 1], [imageMax.lat - 1, imageMax.lng - 1]],
            maxNativeZoom: info.MaxZoom,
            maxZoom: info.MaxZoom + 2,
            noWrap: true,
            attribution: '<a href="https://github.com/DRKV333/TreeHouse">TreeHouse</a>'
        }).addTo(this.#map);

        const centerX = (info.MinX + info.MaxX) / 2;
        const centerY = (info.MinY + info.MaxY) / 2;

        this.#map.setView([centerX, centerY], 0);

        const geoJsonResponse = await fetch(`data/GeoJson/${packageName}.${zoneName}.geojson`);
        const geoJson = await geoJsonResponse.json();

        new GeoJSON(geoJson, {pointToLayer: (geoJsonPoint, latlng) => {
            let marker = null;
            if (geoJsonPoint.properties.type == "Portal") {
                marker = new SelectableMarker(latlng, this.#portalIcon, this.#portalIcon);
            } else if (geoJsonPoint.properties.type == "Vendor") {
                marker = new SelectableCicleMarker(latlng, this.#vendorIcon, this.#vendorIcon);
            } else {
                marker = new SelectableCicleMarker(latlng);
            }

            marker.bindTooltip(geoJsonPoint.properties.name);

            marker.on("click", e => {
                if (!this.#selectionEnabled)
                    return;

                if (this.#selectedMarker == marker)
                    return;

                if (this.#selectedMarker)
                    this.#selectedMarker.setSelected(false);

                marker.setSelected(true);
                this.#selectedMarker = marker;

                for (let listener of this.#selectionListeners) {
                    listener(marker.feature);
                }
            });

            return marker;
        }}).addTo(this.#map);

        return true;
    }

    setSelectionEnabled(enabled) {
        this.#selectionEnabled = enabled;
        if (!enabled) {
            if (this.#selectedMarker)
                this.#selectedMarker.setSelected(false);
            this.#selectedMarker = null;
        }
    }

    addSelectionListener(listener) {
        this.#selectionListeners.push(listener);
    }

    removeSelectionListener(listener) {
        this.#selectionListeners.splice(this.#selectionListeners.indexOf(listener), 1);
    }

    remove() {
        this.#map.remove();
    }

    #createIcon(url) {
        const iconWidth = 30;
        const iconHeight = (642 / 512 * iconWidth);

        return new Icon({
            iconUrl: url,
            iconSize: [iconWidth, iconHeight],
            iconAnchor: [iconWidth / 2, iconHeight],
            tooltipAnchor: [0, -(iconHeight - iconWidth + iconWidth / 2)]
        });
    }

    #createCRS(info) {
        const tileCount = 1 << info.MaxZoom;
        const scale = 1 / info.UnitsPerPixel / tileCount;

        class ScaledSimple extends CRS.Simple {
            static projection = Projection.LonLat;
            static transformation = new Transformation(scale, -info.MinY * scale, -scale, info.TileHeight + (info.MinX * scale));
            static infinite = true;
        }

        return ScaledSimple;
    }
}

export class MessagingServer {
    #eventHandler = this.#onWindowMessage.bind(this);
    #selectionHandler = this.#onMapSelected.bind(this);

    #map;
    #messagePort;

    constructor(map) {
        this.#map = map;

        window.addEventListener("message", this.#eventHandler);
        this.#map.addSelectionListener(this.#selectionHandler);
    }

    remove() {
        window.removeEventListener("message", this.#eventHandler);
        this.#map.removeSelectionListener(this.#selectionHandler);
        if (this.#messagePort) {
            this.#messagePort.close();
        }
    }

    #onWindowMessage(event) {
        if (event.data == "InitFloorMapExplorerMessaging") {
            this.#messagePort = event.ports[0];
            this.#messagePort.onmessage = this.#onPortMessage.bind(this);

            window.removeEventListener("message", this.#eventHandler);
        }
    }

    #onPortMessage(event) {
        if (event.data.type == "SetSelectionEnabled") {
            this.#map.setSelectionEnabled(event.data.enabled);
        }
    }

    #onMapSelected(feature) {
        if (this.#messagePort) {
            this.#messagePort.postMessage({ type: "SelectFeature", feature: feature });
        }
    }
}