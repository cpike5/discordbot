/**
 * Guild Sync Functionality
 * Handles AJAX calls for syncing guild data from Discord to the database.
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

  const icon = buttonElement.querySelector('.sync-icon');
  const spinner = buttonElement.querySelector('.sync-spinner');
  const text = buttonElement.querySelector('.sync-text');

  try {
    // Show loading state
    buttonElement.disabled = true;
    if (icon) icon.classList.add('hidden');
    if (spinner) spinner.classList.remove('hidden');
    if (text) text.textContent = 'Syncing...';

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
      resetSyncButton(buttonElement, icon, spinner, text);
    }
  } catch (error) {
    console.error('Error syncing guild:', error);
    ToastManager.show('error', 'An error occurred while syncing the guild');

    // Reset button state
    resetSyncButton(buttonElement, icon, spinner, text);
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

  const icon = buttonElement.querySelector('.sync-icon');
  const spinner = buttonElement.querySelector('.sync-spinner');
  const text = buttonElement.querySelector('.sync-text');

  try {
    // Show loading state
    buttonElement.disabled = true;
    if (icon) icon.classList.add('hidden');
    if (spinner) spinner.classList.remove('hidden');
    if (text) text.textContent = 'Syncing All...';

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
      resetSyncButton(buttonElement, icon, spinner, text, 'Sync All');
    }
  } catch (error) {
    console.error('Error syncing all guilds:', error);
    ToastManager.show('error', 'An error occurred while syncing guilds');

    // Reset button state
    resetSyncButton(buttonElement, icon, spinner, text, 'Sync All');
  }
}

/**
 * Reset sync button to normal state
 * @private
 */
function resetSyncButton(buttonElement, icon, spinner, text, defaultText = 'Sync') {
  buttonElement.disabled = false;
  if (icon) icon.classList.remove('hidden');
  if (spinner) spinner.classList.add('hidden');
  if (text) text.textContent = defaultText;
}
