/**
 * @typedef {'top-start'|'top-center'|'top-end'|
 *           'middle-start'|'middle-center'|'middle-end'|
 *           'bottom-start'|'bottom-center'|'bottom-end'} ToastPosition
 */

/**
 * @typedef {Object} ToastOptions
 * @property {string} msg - The toast message content
 * @property {ToastPosition} [position='bottom-center'] - Toast position
 * @property {string} [bg='primary'] - Bootstrap background color class (bg-X; e.g.: primary, success, danger, etc.)
 * @property {boolean} [autohide=true] - Should the toast hide automatically after some duration?
 * @property {string} [extraClasses=''] - Specify further classes to be added to the main toast div
 * @property {number} [duration=3000] - Display duration in milliseconds
 */

/**
 * Manages dynamic Bootstrap toast notifications with automatic cleanup
 */
class ToastManager {
  /** @type {HTMLElement} */
  rootContainer;

  /** @type {Map<ToastPosition, HTMLElement>} */
  positionContainers;

  /** Default position for toasts */
  static DEFAULT_POSITION = "bottom-center";

  constructor() {
    this.rootContainer = document.createElement("div");
    this.rootContainer.style.zIndex = "1200";
    document.body.appendChild(this.rootContainer);

    this.positionContainers = new Map();
  }

  /**
   * Get or create a container for a specific position
   * @param {ToastPosition} position - The toast position
   * @returns {HTMLElement} The container element
   * @private
   */
  getPositionContainer(position = ToastManager.DEFAULT_POSITION) {
    if (this.positionContainers.has(position)) {
      return this.positionContainers.get(position);
    }

    const container = document.createElement("div");
    container.className = "toast-container position-fixed p-3";

    const [vertical, horizontal] = position.split("-");

    // Vertical positioning
    if (vertical === "top") container.classList.add("top-0");
    if (vertical === "middle") container.classList.add("top-50", "translate-middle-y");
    if (vertical === "bottom") container.classList.add("bottom-0");

    // Horizontal positioning
    if (horizontal === "start") container.classList.add("start-0");
    if (horizontal === "center") container.classList.add("start-50", "translate-middle-x");
    if (horizontal === "end") container.classList.add("end-0");

    this.rootContainer.appendChild(container);
    this.positionContainers.set(position, container);

    return container;
  }

  /**
   * Creates and shows a new toast notification
   * @param {ToastOptions} options - Configuration options
   * @example
   * // Basic usage
   * toastManager.showToast({ msg: "Operation completed!" });
   *
   * // With options
   * toastManager.showToast({
   *   msg: 'Error!',
   *   bg: 'danger',
   *   duration: 5000,
   *   position: 'top-center'
   * });
   */
  showToast(options) {
    const {
      msg,
      position = "bottom-center",
      bg = "primary",
      extraClasses = "",
      duration = 3000,
      autohide = true,
    } = options;

    if (!msg) {
      console.warn("Toast message cannot be empty");
      return;
    }

    const container = this.getPositionContainer(position);
    const toastId = `toast-${Date.now()}`;
    const bgClass = `bg-${bg}`;

    const toastHtml = `
      <div id="${toastId}"
           class="toast align-items-center text-white border-0 ${bgClass} ${extraClasses}"
           role="alert" aria-live="assertive" aria-atomic="true">
        <div class="d-flex">
          <div class="toast-body">${msg}</div>
          <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
      </div>
    `;

    container.insertAdjacentHTML("beforeend", toastHtml);
    const toastEl = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastEl, {
      delay: duration,
      autohide: autohide,
    });

    toast.show();
  }
}

// Initialize a single instance
const toastManager = new ToastManager();

/**
 * HTMX event listener for toast notifications
 * @typedef {Object} ToastEventDetail
 * @property {string} msg - The toast message
 * @property {string} [position] - Toast position
 * @property {string} [bg] - Background color class
 * @property {string} [extraClasses] - Additional classes
 * @property {number} [duration] - Display duration
 */

/**
 * Handles HTMX toast events
 * @param {CustomEvent<ToastEventDetail>} event - The HTMX event containing toast details
 */
htmx.on("sendToast", (event) => {
  const detail = event.detail;
  if (!detail?.msg) {
    console.warn("Toast triggered without message", detail);
    return;
  }

  toastManager.showToast(detail);
});
