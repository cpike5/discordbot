/**
 * CommandErrorHandler Module
 *
 * Comprehensive error handling for AJAX operations in command pages.
 * Provides error type detection, user-friendly messaging, and retry logic.
 *
 * @module CommandErrorHandler
 * @requires ToastManager
 */
(function() {
  'use strict';

  // Private state
  const state = {
    retryAttempts: new Map(),           // Tracks retry attempts: operationId -> count
    maxRetries: 3,                      // Default max retry attempts
    retryableStatusCodes: [408, 429, 500, 502, 503, 504], // HTTP status codes eligible for retry
    defaultTimeout: 15000               // Default timeout for requests (15 seconds)
  };

  /**
   * Checks if ToastManager is available.
   * @private
   * @returns {boolean} True if ToastManager exists
   */
  function checkToastManager() {
    if (typeof window.ToastManager === 'undefined') {
      console.warn('[CommandErrorHandler] ToastManager is not available');
      return false;
    }
    return true;
  }

  /**
   * Generates a unique operation ID for retry tracking.
   * @private
   * @param {Object} context - Error context
   * @returns {string} Unique operation identifier
   */
  function getOperationId(context) {
    if (context && context.operationType) {
      return context.operationType;
    }
    // Generate random ID for anonymous operations
    return `operation-${Math.random().toString(36).substring(2, 11)}`;
  }

  /**
   * Detects if an error is a network-related error.
   * Network errors typically occur when the request cannot reach the server.
   *
   * @public
   * @param {Error} error - Error object to check
   * @returns {boolean} True if error is network-related
   * @example
   * if (CommandErrorHandler.isNetworkError(error)) {
   *   console.log('Network connectivity issue');
   * }
   */
  function isNetworkError(error) {
    if (!error) {
      return false;
    }

    // Check error type
    if (error.name === 'TypeError') {
      return true;
    }

    // Check error message for common network error patterns
    const message = error.message || '';
    const networkPatterns = [
      'Failed to fetch',
      'NetworkError',
      'Network request failed',
      'fetch failed',
      'The network connection was lost'
    ];

    return networkPatterns.some(pattern => message.includes(pattern));
  }

  /**
   * Checks if an error is a timeout error.
   * @private
   * @param {Error} error - Error object to check
   * @returns {boolean} True if error is timeout-related
   */
  function isTimeoutError(error) {
    if (!error) {
      return false;
    }

    // Check for timeout flag set by createTimeoutChecker
    if (error.isTimeout === true) {
      return true;
    }

    // Check for AbortError name (timeout aborts the request)
    if (error.name === 'AbortError' && error.message && error.message.includes('timeout')) {
      return true;
    }

    return false;
  }

  /**
   * Extracts a user-friendly error message based on the error type and HTTP status code.
   *
   * @public
   * @param {Error|Response} error - Error object or fetch Response
   * @returns {string|null} User-friendly error message, or null for silent errors
   * @example
   * const message = CommandErrorHandler.getErrorMessage(error);
   * if (message) {
   *   console.error(message);
   * }
   */
  function getErrorMessage(error) {
    if (!error) {
      return 'An unexpected error occurred. Please try again.';
    }

    // AbortError should be silent (user-initiated cancellation)
    if (error.name === 'AbortError' && !isTimeoutError(error)) {
      return null;
    }

    // Timeout errors
    if (isTimeoutError(error)) {
      return 'Request timed out. Please try again.';
    }

    // Network errors
    if (isNetworkError(error)) {
      return 'Network error. Please check your connection and try again.';
    }

    // HTTP status code errors
    if (error.status) {
      const status = error.status;

      switch (status) {
        case 400:
          return 'Invalid request. Please check your input and try again.';
        case 401:
          return 'Your session has expired. Please refresh the page and log in again.';
        case 403:
          return 'You are not authorized to view this content.';
        case 404:
          return 'The requested data was not found.';
        case 408:
          return 'Request timed out. Please try again.';
        case 429:
          return 'Too many requests. Please wait a moment and try again.';
        case 500:
        case 502:
        case 503:
        case 504:
          return 'An error occurred on the server. Please try again later.';
        default:
          if (status >= 500 && status < 600) {
            return 'An error occurred on the server. Please try again later.';
          }
          if (status >= 400 && status < 500) {
            return 'Invalid request. Please check your input and try again.';
          }
          return 'An unexpected error occurred. Please try again.';
      }
    }

    // Generic error with message
    if (error.message) {
      console.error('[CommandErrorHandler] Error details:', error.message);
      return 'An unexpected error occurred. Please try again.';
    }

    // Fallback
    return 'An unexpected error occurred. Please try again.';
  }

  /**
   * Determines if an error is eligible for retry based on its type and status code.
   * @private
   * @param {Error|Response} error - Error object or fetch Response
   * @returns {boolean} True if error is retryable
   */
  function isRetryable(error) {
    if (!error) {
      return false;
    }

    // Network errors are retryable
    if (isNetworkError(error)) {
      return true;
    }

    // Timeout errors are retryable
    if (isTimeoutError(error)) {
      return true;
    }

    // Check HTTP status code
    if (error.status) {
      // Non-retryable status codes
      const nonRetryableStatuses = [400, 401, 403, 404];
      if (nonRetryableStatuses.includes(error.status)) {
        return false;
      }

      // Check against retryable status codes list
      return state.retryableStatusCodes.includes(error.status);
    }

    // Unknown error types are retryable by default
    return true;
  }

  /**
   * Shows an error toast notification.
   *
   * @public
   * @param {string} message - Error message to display
   * @param {Object} [options={}] - Toast options
   * @param {string} [options.title='Error'] - Toast title
   * @param {number} [options.duration=5000] - Display duration in milliseconds
   * @example
   * CommandErrorHandler.showErrorToast('Failed to load data', { duration: 3000 });
   */
  function showErrorToast(message, options = {}) {
    if (!checkToastManager()) {
      // Fallback to console if ToastManager unavailable
      console.error('[CommandErrorHandler]', message);
      return;
    }

    const defaultOptions = {
      title: 'Error',
      duration: 5000
    };

    const toastOptions = { ...defaultOptions, ...options };

    window.ToastManager.show('error', message, toastOptions);
  }

  /**
   * Shows an error toast with a retry button.
   *
   * @public
   * @param {string} message - Error message to display
   * @param {Function} retryCallback - Function to call when retry is clicked
   * @param {string} [operationId] - Optional operation ID for retry tracking
   * @example
   * CommandErrorHandler.showRetryOption(
   *   'Failed to load data',
   *   () => fetchData(),
   *   'fetch-data-operation'
   * );
   */
  function showRetryOption(message, retryCallback, operationId) {
    if (!checkToastManager()) {
      // Fallback to console if ToastManager unavailable
      console.error('[CommandErrorHandler]', message);
      if (typeof retryCallback === 'function') {
        console.log('[CommandErrorHandler] Retry callback available but cannot display retry button');
      }
      return;
    }

    if (typeof retryCallback !== 'function') {
      console.warn('[CommandErrorHandler] Retry callback is not a function');
      showErrorToast(message);
      return;
    }

    // Wrap retry callback to track attempts
    const wrappedRetryCallback = function() {
      // Increment retry counter
      if (operationId) {
        const currentAttempts = state.retryAttempts.get(operationId) || 0;
        state.retryAttempts.set(operationId, currentAttempts + 1);
        console.log(`[CommandErrorHandler] Retry attempt ${currentAttempts + 1} for operation: ${operationId}`);
      }

      // Execute original callback
      retryCallback();
    };

    window.ToastManager.show('error', message, {
      title: 'Error',
      duration: 0, // Don't auto-dismiss - user must take action
      action: {
        label: 'Retry',
        onClick: wrappedRetryCallback
      }
    });
  }

  /**
   * Main error handler for AJAX operations.
   * Detects error type, shows appropriate message, and offers retry if applicable.
   *
   * @public
   * @param {Error|Response} error - Error object or fetch Response
   * @param {Object} [context={}] - Error context
   * @param {Function} [context.retryCallback] - Function to call for retry
   * @param {string} [context.operationType] - Operation identifier for retry tracking
   * @param {number} [context.maxRetries] - Override default max retries
   * @example
   * try {
   *   const response = await fetch('/api/commands');
   *   if (!response.ok) throw response;
   * } catch (error) {
   *   CommandErrorHandler.handleAjaxError(error, {
   *     retryCallback: () => loadCommands(),
   *     operationType: 'load-commands'
   *   });
   * }
   */
  function handleAjaxError(error, context = {}) {
    // Silent return for AbortError (user-initiated cancellation)
    if (error && error.name === 'AbortError' && !isTimeoutError(error)) {
      console.log('[CommandErrorHandler] Request aborted by user');
      return;
    }

    // Log error for debugging
    console.error('[CommandErrorHandler] Error occurred:', error);
    if (context.operationType) {
      console.error('[CommandErrorHandler] Operation:', context.operationType);
    }

    // Get user-friendly message
    const message = getErrorMessage(error);
    if (!message) {
      // Silent error (e.g., AbortError)
      return;
    }

    // Get operation ID for retry tracking
    const operationId = getOperationId(context);

    // Check retry eligibility
    const maxRetries = context.maxRetries ?? state.maxRetries;
    const currentAttempts = state.retryAttempts.get(operationId) || 0;
    const canRetry = isRetryable(error) && currentAttempts < maxRetries && context.retryCallback;

    if (canRetry) {
      console.log(`[CommandErrorHandler] Offering retry (${currentAttempts}/${maxRetries})`);
      showRetryOption(message, context.retryCallback, operationId);
    } else {
      if (currentAttempts >= maxRetries) {
        console.warn(`[CommandErrorHandler] Max retries (${maxRetries}) exceeded for operation: ${operationId}`);
        // Clean up retry counter
        state.retryAttempts.delete(operationId);
      }
      showErrorToast(message);
    }
  }

  /**
   * Creates a timeout checker for fetch operations.
   * Returns an object with AbortController signal and timeout management functions.
   *
   * @public
   * @param {number} [timeoutMs=15000] - Timeout duration in milliseconds
   * @returns {Object} Timeout checker object
   * @returns {AbortSignal} return.signal - AbortController signal for fetch
   * @returns {Function} return.isTimedOut - Function that returns true if timeout occurred
   * @returns {Function} return.clear - Function to clear the timeout
   * @example
   * const { signal, isTimedOut, clear } = CommandErrorHandler.createTimeoutChecker(10000);
   * try {
   *   const response = await fetch('/api/data', { signal });
   *   clear();
   *   // process response
   * } catch (error) {
   *   if (isTimedOut()) {
   *     console.error('Request timed out');
   *   }
   * }
   */
  function createTimeoutChecker(timeoutMs = state.defaultTimeout) {
    const controller = new AbortController();
    let timedOut = false;
    let timeoutId = null;

    // Set timeout to abort request
    timeoutId = setTimeout(() => {
      timedOut = true;
      // Create custom error with timeout flag
      const error = new Error('Request timed out');
      error.isTimeout = true;
      controller.abort(error);
    }, timeoutMs);

    return {
      signal: controller.signal,
      isTimedOut: () => timedOut,
      clear: () => {
        if (timeoutId) {
          clearTimeout(timeoutId);
          timeoutId = null;
        }
      }
    };
  }

  /**
   * Resets retry attempt counter for an operation.
   * Useful when an operation succeeds after retries.
   * @private
   * @param {string} operationId - Operation identifier
   */
  function resetRetryCount(operationId) {
    if (operationId) {
      state.retryAttempts.delete(operationId);
    }
  }

  // Public API
  window.CommandErrorHandler = {
    handleAjaxError: handleAjaxError,
    showErrorToast: showErrorToast,
    showRetryOption: showRetryOption,
    isNetworkError: isNetworkError,
    getErrorMessage: getErrorMessage,
    createTimeoutChecker: createTimeoutChecker,

    // Utility method for successful operations (cleanup)
    resetRetryCount: resetRetryCount
  };

  // Log module initialization
  if (window.console && console.log) {
    console.log('[CommandErrorHandler] Module initialized');
  }

})();
