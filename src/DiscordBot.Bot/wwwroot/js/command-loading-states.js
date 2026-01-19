/**
 * CommandLoadingStates Module
 *
 * Simplified loading state API for command pages.
 * Wraps LoadingManager with command-page-specific defaults and provides
 * form state management utilities.
 *
 * @module CommandLoadingStates
 * @requires LoadingManager
 */
(function() {
  'use strict';

  // Private state
  const state = {
    disabledFormsMap: new WeakMap(), // Stores form state: { elements: [], originalStates: [] }
    activeSpinners: new Map(),       // Tracks active spinners and their timeouts: container -> timeoutId
    defaultSpinnerDelay: 150         // Delay before showing spinner (prevents flash)
  };

  /**
   * Resolves a container reference to an HTMLElement.
   * @private
   * @param {string|HTMLElement} container - Container ID or element
   * @returns {HTMLElement|null} Resolved element or null
   */
  function resolveContainer(container) {
    if (!container) {
      console.warn('[CommandLoadingStates] Container is null or undefined');
      return null;
    }

    if (typeof container === 'string') {
      const element = document.getElementById(container);
      if (!element) {
        console.warn(`[CommandLoadingStates] Container with ID "${container}" not found`);
      }
      return element;
    }

    if (container instanceof HTMLElement) {
      return container;
    }

    console.warn('[CommandLoadingStates] Invalid container type:', typeof container);
    return null;
  }

  /**
   * Resolves a form reference to an HTMLFormElement.
   * @private
   * @param {string|HTMLFormElement} form - Form ID or element
   * @returns {HTMLFormElement|null} Resolved form element or null
   */
  function resolveForm(form) {
    if (!form) {
      console.warn('[CommandLoadingStates] Form is null or undefined');
      return null;
    }

    if (typeof form === 'string') {
      const element = document.getElementById(form);
      if (!element) {
        console.warn(`[CommandLoadingStates] Form with ID "${form}" not found`);
        return null;
      }
      if (!(element instanceof HTMLFormElement)) {
        console.warn(`[CommandLoadingStates] Element with ID "${form}" is not a form`);
        return null;
      }
      return element;
    }

    if (form instanceof HTMLFormElement) {
      return form;
    }

    console.warn('[CommandLoadingStates] Invalid form type:', typeof form);
    return null;
  }

  /**
   * Checks if LoadingManager is available.
   * @private
   * @returns {boolean} True if LoadingManager exists
   */
  function checkLoadingManager() {
    if (typeof window.LoadingManager === 'undefined') {
      console.warn('[CommandLoadingStates] LoadingManager is not available');
      return false;
    }
    return true;
  }

  /**
   * Shows a loading spinner in the specified container after a short delay.
   * Prevents flash of loading state for fast operations.
   *
   * @public
   * @param {string|HTMLElement} container - Container element or ID
   * @param {string} [message='Loading...'] - Loading message to display
   * @example
   * CommandLoadingStates.showLoadingSpinner('results-container', 'Fetching data...');
   */
  function showLoadingSpinner(container, message = 'Loading...') {
    if (!checkLoadingManager()) {
      return;
    }

    // Resolve to container ID (LoadingManager expects string ID)
    let containerId;
    if (typeof container === 'string') {
      containerId = container;
      // Verify element exists
      if (!document.getElementById(containerId)) {
        console.warn(`[CommandLoadingStates] Container with ID "${containerId}" not found`);
        return;
      }
    } else if (container instanceof HTMLElement) {
      if (!container.id) {
        console.warn('[CommandLoadingStates] Container element must have an ID attribute');
        return;
      }
      containerId = container.id;
    } else {
      console.warn('[CommandLoadingStates] Container must be a string ID or HTMLElement with ID');
      return;
    }

    // Cancel any existing timeout for this container
    const existingTimeout = state.activeSpinners.get(containerId);
    if (existingTimeout) {
      clearTimeout(existingTimeout);
    }

    // Show spinner after delay to prevent flash
    const timeoutId = setTimeout(() => {
      window.LoadingManager.showContainerLoading(containerId, message);
      state.activeSpinners.delete(containerId);
    }, state.defaultSpinnerDelay);

    state.activeSpinners.set(containerId, timeoutId);
  }

  /**
   * Hides the loading spinner from the specified container.
   * Cancels pending spinner display if called before delay expires.
   *
   * @public
   * @param {string|HTMLElement} container - Container element or ID
   * @example
   * CommandLoadingStates.hideLoadingSpinner('results-container');
   */
  function hideLoadingSpinner(container) {
    if (!checkLoadingManager()) {
      return;
    }

    // Resolve to container ID (LoadingManager expects string ID)
    let containerId;
    if (typeof container === 'string') {
      containerId = container;
      // Element may have been removed, so don't error if not found
    } else if (container instanceof HTMLElement) {
      if (!container.id) {
        console.warn('[CommandLoadingStates] Container element must have an ID attribute');
        return;
      }
      containerId = container.id;
    } else {
      console.warn('[CommandLoadingStates] Container must be a string ID or HTMLElement with ID');
      return;
    }

    // Cancel pending spinner if not yet shown
    const existingTimeout = state.activeSpinners.get(containerId);
    if (existingTimeout) {
      clearTimeout(existingTimeout);
      state.activeSpinners.delete(containerId);
      return;
    }

    // Hide spinner if already shown
    window.LoadingManager.hideContainerLoading(containerId);
  }

  /**
   * Disables all interactive elements in a form and stores their original states.
   * Adds visual indicator via CSS class.
   *
   * @public
   * @param {string|HTMLFormElement} formElement - Form element or ID
   * @example
   * CommandLoadingStates.disableForm('filter-form');
   */
  function disableForm(formElement) {
    const form = resolveForm(formElement);
    if (!form) {
      return;
    }

    // Find all interactive elements
    const elements = form.querySelectorAll('button, input, select, textarea, a');
    if (elements.length === 0) {
      console.warn('[CommandLoadingStates] No interactive elements found in form');
      return;
    }

    // Store original states
    const originalStates = Array.from(elements).map(el => ({
      element: el,
      disabled: el.disabled,
      ariaDisabled: el.getAttribute('aria-disabled')
    }));

    // Disable all elements
    elements.forEach(el => {
      el.disabled = true;
      el.setAttribute('aria-disabled', 'true');
    });

    // Add visual class
    form.classList.add('form-disabled');

    // Store state in WeakMap
    state.disabledFormsMap.set(form, {
      elements: Array.from(elements),
      originalStates: originalStates
    });
  }

  /**
   * Re-enables form elements and restores their original states.
   * Removes visual indicator CSS class.
   *
   * @public
   * @param {string|HTMLFormElement} formElement - Form element or ID
   * @example
   * CommandLoadingStates.enableForm('filter-form');
   */
  function enableForm(formElement) {
    const form = resolveForm(formElement);
    if (!form) {
      return;
    }

    // Retrieve stored state
    const formState = state.disabledFormsMap.get(form);
    if (!formState) {
      console.warn('[CommandLoadingStates] No stored state found for form, cannot restore');
      return;
    }

    // Restore original states
    formState.originalStates.forEach(({ element, disabled, ariaDisabled }) => {
      element.disabled = disabled;
      if (ariaDisabled === null) {
        element.removeAttribute('aria-disabled');
      } else {
        element.setAttribute('aria-disabled', ariaDisabled);
      }
    });

    // Remove visual class
    form.classList.remove('form-disabled');

    // Clean up WeakMap (optional, but good practice)
    state.disabledFormsMap.delete(form);
  }

  /**
   * Shows a skeleton loader placeholder in the specified container.
   *
   * @public
   * @param {string|HTMLElement} container - Container element or ID
   * @example
   * CommandLoadingStates.showSkeletonLoader('table-container');
   */
  function showSkeletonLoader(container) {
    if (!checkLoadingManager()) {
      return;
    }

    // Resolve to container ID (LoadingManager expects string ID)
    let containerId;
    if (typeof container === 'string') {
      containerId = container;
      if (!document.getElementById(containerId)) {
        console.warn(`[CommandLoadingStates] Container with ID "${containerId}" not found`);
        return;
      }
    } else if (container instanceof HTMLElement) {
      if (!container.id) {
        console.warn('[CommandLoadingStates] Container element must have an ID attribute');
        return;
      }
      containerId = container.id;
    } else {
      console.warn('[CommandLoadingStates] Container must be a string ID or HTMLElement with ID');
      return;
    }

    window.LoadingManager.showSkeleton(containerId);
  }

  /**
   * Hides the skeleton loader placeholder from the specified container.
   *
   * @public
   * @param {string|HTMLElement} container - Container element or ID
   * @example
   * CommandLoadingStates.hideSkeletonLoader('table-container');
   */
  function hideSkeletonLoader(container) {
    if (!checkLoadingManager()) {
      return;
    }

    // Resolve to container ID (LoadingManager expects string ID)
    let containerId;
    if (typeof container === 'string') {
      containerId = container;
      // Element may have been removed, so don't error if not found
    } else if (container instanceof HTMLElement) {
      if (!container.id) {
        console.warn('[CommandLoadingStates] Container element must have an ID attribute');
        return;
      }
      containerId = container.id;
    } else {
      console.warn('[CommandLoadingStates] Container must be a string ID or HTMLElement with ID');
      return;
    }

    window.LoadingManager.hideSkeleton(containerId);
  }

  // Public API
  window.CommandLoadingStates = {
    showLoadingSpinner: showLoadingSpinner,
    hideLoadingSpinner: hideLoadingSpinner,
    disableForm: disableForm,
    enableForm: enableForm,
    showSkeletonLoader: showSkeletonLoader,
    hideSkeletonLoader: hideSkeletonLoader
  };

  // Log module initialization
  if (window.console && console.log) {
    console.log('[CommandLoadingStates] Module initialized');
  }

})();
