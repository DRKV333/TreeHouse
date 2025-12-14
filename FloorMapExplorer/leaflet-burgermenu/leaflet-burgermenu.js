// Adapted from https://github.com/BenPortner/leaflet-burgermenu

/*
MIT License

Copyright (c) 2025 Benjamin W. Portner

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

// leaflet-burgermenu.js
// A Leaflet plugin that adds a burger menu with submenus
// Author: Benjamin W. Portner
// License: MIT
import { Control, DomUtil, DomEvent } from "leaflet";


export class BurgerMenuControl extends Control {

    constructor(options) {
        const defaultOptions = {
            position: 'topleft',
            menuIcon: '&#9776;', // Burger icon
            subMenuIndicator: '⊳',
            title: 'Menu',
            menuItems: [] // [{ title: 'Main', subItems: [{ title: 'Sub1', onClick: fn }, ...] }]
        };
        options = { ...defaultOptions, ...options };
        super(options);
    }

    _generateSubMenus(item, itemEl, level) {
        if (item.menuItems && item.menuItems.length) {
            if (level > 0) {
                itemEl.textContent += ` ${this.options.subMenuIndicator}`;
            }
            const classList = `burger-menu level-${level} hidden`;
            const subMenu = DomUtil.create('div', classList, itemEl);
            DomEvent.on(itemEl, 'mouseover', function (e) {
                subMenu.classList.remove('hidden');
            });
            DomEvent.on(itemEl, 'mouseout', function (e) {
                subMenu.classList.add('hidden');
            });
            item.menuItems.forEach(subItem => {
                const classList = `burger-menu-item level-${level}`;
                const subItemEl = DomUtil.create('div', classList, subMenu);
                subItemEl.textContent = subItem.title;
                this._generateSubMenus(subItem, subItemEl, level + 1);
            });
        } else if (typeof item.onClick === 'function') {
            DomEvent.on(itemEl, 'click', function (e) {
                e.stopPropagation();
                item.onClick(e);
            });
        }
    }

    onAdd(map) {
        const container = DomUtil.create('div', 'leaflet-control-burgermenu');
        DomEvent.disableClickPropagation(container);

        const button = DomUtil.create('div', 'burger-button', container);
        button.innerHTML = this.options.menuIcon;
        button.title = this.options.title;

        this._generateSubMenus(this.options, container, 0);

        return container;
    }
};
