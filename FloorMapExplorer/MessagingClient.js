export class MessagingClient {
    #messagePort;
    #selectionListeners = [];

    constructor(theWindow) {
        const channel = new MessageChannel();
        this.#messagePort = channel.port1;
        this.#messagePort.onmessage = this.#onMessage.bind(this);
        theWindow.postMessage("InitFloorMapExplorerMessaging", "*", [channel.port2]);
    }

    addSelectionListener(listener) {
        this.#selectionListeners.push(listener);
    }

    removeSelectionListener(listener) {
        this.#selectionListeners.splice(this.#selectionListeners.indexOf(listener), 1);
    }

    #onMessage(event) {
        if (event.data.type === "SelectFeature") {
            for (const listener of this.#selectionListeners) {
                listener(event.data.feature);
            }
        }
    }

    setSelectionEnabled(enabled) {
        this.#messagePort.postMessage({ type: "SetSelectionEnabled", enabled });
    }

    loadMap(packageName, zoneName) {
        this.#messagePort.postMessage({ type: "LoadMap", packageName, zoneName });
    }

    remove() {
        if (this.#messagePort) {
            this.#messagePort.close();
        }
    }
}