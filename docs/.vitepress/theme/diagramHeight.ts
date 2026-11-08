// The renderer fits every diagram into a box of one fixed height, so tall diagrams shrink
// until their text cannot be read. This gives each box the diagram's own height, scaled
// down only when the diagram is wider than the page. The renderer re-fits on resize.

const padding = 32;

function sizeBox(container: HTMLElement) {
    const viewport = container.querySelector<HTMLElement>(".mermaid-viewport");
    if (!viewport?.querySelector("svg")) {
        return;
    }
    const scale = Math.min(1, container.clientWidth / viewport.offsetWidth);
    const height = Math.ceil(viewport.offsetHeight * scale) + padding;
    container.style.setProperty("--diagram-height", `${height}px`);
    container.dataset.naturalHeight = "";
}

function sizeAllBoxes() {
    document.querySelectorAll<HTMLElement>(".vp-doc .mermaid-container").forEach(sizeBox);
}

export function sizeDiagramsToContent() {
    let pending = false;
    const schedule = () => {
        if (pending) {
            return;
        }
        pending = true;
        requestAnimationFrame(() => {
            pending = false;
            sizeAllBoxes();
        });
    };
    // Diagrams render when they scroll into view, and again when the theme changes.
    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
    window.addEventListener("resize", schedule);
}
