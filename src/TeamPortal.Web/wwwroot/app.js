// *****************************************************************************
// Purpose : The small amount of browser work Blazor cannot do from C#.
//
//           Kept deliberately thin. Anything that can be decided in a component
//           belongs in the component - JS here is for things that only exist in
//           the DOM, like scroll position.
// Ticket  : SD-PENDING
// *****************************************************************************
// Date            By                      Action
// *****************************************************************************
// 26 Aug 2026     Orchestra Team          Created
// *****************************************************************************

window.teamPortal = window.teamPortal || {};

/**
 * Scrolls a container to its newest content.
 *
 * Deferred a frame, because Blazor calls this straight after StateHasChanged and
 * the new message has not been laid out yet - measuring scrollHeight in the same
 * tick scrolls to where the bottom USED to be.
 *
 * @param {string} selector CSS selector for the scrolling element.
 */
window.teamPortal.scrollToEnd = function (selector) {
    const element = document.querySelector(selector);

    if (!element) {
        return;
    }

    requestAnimationFrame(() => {
        element.scrollTop = element.scrollHeight;
    });
};
