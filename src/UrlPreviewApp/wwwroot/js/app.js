// Browser helpers used by UrlPreviewApp.Client (BrowserInterop).

export function getImageSize(url) {
    return new Promise(resolve => {
        const img = new Image();
        img.onload = () => resolve({ width: img.naturalWidth, height: img.naturalHeight });
        img.onerror = () => resolve(null);
        img.src = url;
    });
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

function isTyping(target) {
    return target instanceof HTMLElement
        && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName));
}

// "/" focuses the URL input (unless the user is typing), "Escape" clears.
export function registerShortcuts(dotNetRef) {
    const onKeyDown = event => {
        if (event.key === "/" && !isTyping(event.target) && !event.ctrlKey && !event.metaKey && !event.altKey) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync("FocusInput");
        } else if (event.key === "Escape") {
            dotNetRef.invokeMethodAsync("ClearFromShortcut");
        }
    };

    window.addEventListener("keydown", onKeyDown);
    return { dispose: () => window.removeEventListener("keydown", onKeyDown) };
}
