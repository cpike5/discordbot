/**
 * Guild Sync Functionality
 * Handles AJAX calls for syncing guild data from Discord to the database.
 * Uses LoadingManager for consistent loading states.
 */

/**
 * Sync a single guild
 * @param {string|number} guildId - The guild ID to sync
 * @param {HTMLElement} buttonElement - The button element that triggered the sync
 */
async function syncGuild(guildId, buttonElement) {
  if (!buttonElement) {
    console.error('Button element is required for syncGuild');
    return;
  }

  try {
    // Show loading state using LoadingManager
    if (typeof LoadingManager !== 'undefined') {
      LoadingManager.setButtonLoading(buttonElement, true, 'Syncing...');
    } else {
      // Fallback to manual loading state
      buttonElement.disabled = true;
      const icon = buttonElement.querySelector('.sync-icon');
      const spinner = buttonElement.querySelector('.sync-spinner');
      const text = buttonElement.querySelector('.sync-text');
      if (icon) icon.classList.add('hidden');
      if (spinner) spinner.classList.remove('hidden');
      if (text) text.textContent = 'Syncing...';
    }

    // Get CSRF token
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    // Make AJAX request
    const response = await fetch(`?handler=Sync&id=${guildId}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'X-Requested-With': 'XMLHttpRequest',
        'RequestVerificationToken': token
      }
    });

    const result = await response.json();

    if (result.success) {
      // Show success toast
      ToastManager.show('success', result.message || 'Guild synced successfully');

      // Reload page after short delay to show updated data
      setTimeout(() => {
        window.location.reload();
      }, 1000);
    } else {
      // Show error toast
      ToastManager.show('error', result.message || 'Failed to sync guild');

      // Reset button state
      if (typeof LoadingManager !== 'undefined') {
        LoadingManager.setButtonLoading(buttonElement, false);
      } else {
        resetSyncButton(buttonElement);
      }
    }
  } catch (error) {
    console.error('Error syncing guild:', error);
    ToastManager.show('error', 'An error occurred while syncing the guild');

    // Reset button state
    if (typeof LoadingManager !== 'undefined') {
      LoadingManager.setButtonLoading(buttonElement, false);
    } else {
      resetSyncButton(buttonElement);
    }
  }
}

/**
 * Sync all guilds (admin only)
 * @param {HTMLElement} buttonElement - The button element that triggered the sync
 */
async function syncAllGuilds(buttonElement) {
  if (!buttonElement) {
    console.error('Button element is required for syncAllGuilds');
    return;
  }

  try {
    // Show page-level loading for sync all operation
    if (typeof LoadingManager !== 'undefined') {
      LoadingManager.showPageLoading('Syncing all guilds...', {
        subMessage: 'This may take a moment',
        timeout: 60000 // 60 second timeout for sync all
      });
      LoadingManager.setButtonLoading(buttonElement, true, 'Syncing All...');
    } else {
      // Fallback to manual loading state
      buttonElement.disabled = true;
      const icon = buttonElement.querySelector('.sync-icon');
      const spinner = buttonElement.querySelector('.sync-spinner');
      const text = buttonElement.querySelector('.sync-text');
      if (icon) icon.classList.add('hidden');
      if (spinner) spinner.classList.remove('hidden');
      if (text) text.textContent = 'Syncing All...';
    }

    // Get CSRF token
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    // Make AJAX request
    const response = await fetch('?handler=SyncAll', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'X-Requested-With': 'XMLHttpRequest',
        'RequestVerificationToken': token
      }
    });

    const result = await response.json();

    // Hide page loading
    if (typeof LoadingManager !== 'undefined') {
      LoadingManager.hidePageLoading();
    }

    if (result.success) {
      // Show success toast with count
      const message = result.message || `Successfully synced ${result.syncedCount || 0} guilds`;
      ToastManager.show('success', message);

      // Reload page after short delay to show updated data
      setTimeout(() => {
        window.location.reload();
      }, 1500);
    } else {
      // Show error toast
      ToastManager.show('error', result.message || 'Failed to sync guilds');

      // Reset button state
      if (typeof LoadingManager !== 'undefined') {
        LoadingManager.setButtonLoading(buttonElement, false);
      } else {
        resetSyncButton(buttonElement);
      }
    }
  } catch (error) {
    console.error('Error syncing all guilds:', error);
    ToastManager.show('error', 'An error occurred while syncing guilds');

    // Hide page loading and reset button state
    if (typeof LoadingManager !== 'undefined') {
      LoadingManager.hidePageLoading();
      LoadingManager.setButtonLoading(buttonElement, false);
    } else {
      resetSyncButton(buttonElement);
    }
  }
}

/**
 * Reset sync button to normal state (fallback when LoadingManager not available)
 * @private
 */
function resetSyncButton(buttonElement) {
  buttonElement.disabled = false;
  const icon = buttonElement.querySelector('.sync-icon');
  const spinner = buttonElement.querySelector('.sync-spinner');
  const text = buttonElement.querySelector('.sync-text');
  if (icon) icon.classList.remove('hidden');
  if (spinner) spinner.classList.add('hidden');
  if (text) text.textContent = 'Sync';
}
