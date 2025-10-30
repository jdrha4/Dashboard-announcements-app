// Initialize tooltips on page load
document.addEventListener('DOMContentLoaded', () => {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    [...tooltipTriggerList].map(el => new bootstrap.Tooltip(el));
});

// Reinitialize tooltips after HTMX content updates
document.body.addEventListener('htmx:afterSettle', () => {
    const newTooltips = document.querySelectorAll('[data-bs-toggle="tooltip"]:not(.tooltip-initialized)');
    [...newTooltips].map(el => {
        el.classList.add('tooltip-initialized');
        return new bootstrap.Tooltip(el);
    });
});
