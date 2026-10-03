// Demo wiring for /Components. Nothing here is used by the app: each handler shows a primitive
// in a state a reader would otherwise have to build a page to see.
(function () {
  'use strict';

  function sleep(ms) {
    return new Promise(function (resolve) { setTimeout(resolve, ms); });
  }

  // ---- Toasts
  document.querySelectorAll('[data-demo-toast]').forEach(function (button) {
    button.addEventListener('click', function () {
      switch (button.dataset.demoToast) {
        case 'success': toast.success('Settings saved.'); break;
        case 'info': toast.info('Copied to clipboard.', { title: 'Clipboard' }); break;
        case 'warning': toast.warning('You are close to the rate limit.'); break;
        case 'error': toast.error('Could not save the settings.'); break;
        case 'action': toast.error('Could not reach the server.', { action: { label: 'Retry', onClick: function () { toast.success('Retried.'); } } }); break;
      }
    });
  });

  // ---- Modals
  document.addEventListener('click', function (e) {
    var modalButton = e.target.closest('[data-show-modal]');
    if (modalButton) {
      window.quickActions.showConfirmationModal(modalButton.dataset.showModal);
      return;
    }

    var dialogButton = e.target.closest('[data-show-dialog]');
    if (dialogButton) {
      window.quickActions.openDialog(document.getElementById(dialogButton.dataset.showDialog), {
        initialFocus: '#stack-demo-input'
      });
    }
  });

  var second = document.getElementById('stack-demo-open-second');
  if (second) {
    second.addEventListener('click', function () {
      window.quickActions.confirm({
        title: 'Second dialog',
        message: 'This one is on top. Escape closes it first, then the base dialog.',
        variant: 'info',
        confirmText: 'OK',
        cancelText: 'Back'
      });
    });
  }

  var dialogResult = document.getElementById('js-dialog-result');
  document.querySelectorAll('[data-demo-dialog]').forEach(function (button) {
    button.addEventListener('click', function () {
      var kind = button.dataset.demoDialog;
      var pending;
      if (kind === 'confirm') {
        pending = window.quickActions.confirm({
          title: 'Confirm action',
          message: 'Do you want to proceed with this action?',
          variant: 'warning',
          confirmText: 'Yes, proceed',
          cancelText: 'No, cancel'
        }).then(function (ok) { return ok ? 'Confirmed' : 'Cancelled'; });
      } else if (kind === 'alert') {
        pending = window.quickActions.alert({
          title: 'Operation complete',
          message: 'The export has finished. Your file is ready for download.',
          variant: 'info',
          okText: 'Got it'
        }).then(function () { return 'Dismissed'; });
      } else {
        pending = window.quickActions.typedConfirm({
          title: 'Delete repository',
          message: 'This will permanently delete the repository and all its data.',
          requiredText: 'CONFIRM',
          inputLabel: 'Type CONFIRM to continue',
          variant: 'danger',
          confirmText: 'Delete'
        }).then(function (ok) { return ok ? 'Confirmed' : 'Cancelled'; });
      }
      pending.then(function (text) { if (dialogResult) dialogResult.textContent = text; });
    });
  });

  // ---- Toggle: what a form posts
  var toggleForm = document.getElementById('toggle-demo');
  if (toggleForm) {
    toggleForm.addEventListener('submit', function (e) {
      e.preventDefault();
      var entries = [];
      new FormData(toggleForm).forEach(function (value, key) { entries.push(key + ' = ' + value); });
      document.getElementById('toggle-demo-output').textContent = entries.join('\n');
    });
  }

  // ---- Empty state twin
  var emptyTarget = document.getElementById('empty-js-demo');
  document.querySelectorAll('[data-demo-empty]').forEach(function (button) {
    button.addEventListener('click', function () {
      if (!emptyTarget || !window.EmptyState) return;
      switch (button.dataset.demoEmpty) {
        case 'plain':
          window.EmptyState.render(emptyTarget, {
            type: 'noData', size: 'compact',
            title: 'Nothing here yet',
            description: 'Items you add will appear in this list.',
            action: { text: 'Add an item', onClick: function () { toast.info('Add clicked.'); } }
          });
          break;
        case 'filtered':
          window.EmptyState.filtered(emptyTarget, {
            noun: 'commands', size: 'compact',
            onClear: function () { emptyTarget.textContent = ''; toast.success('Filters cleared.'); }
          });
          break;
        default:
          window.EmptyState.error(emptyTarget, {
            size: 'compact',
            onRetry: function () { toast.info('Retrying...'); }
          });
      }
    });
  });

  document.addEventListener('click', function (e) {
    if (e.target.closest('[data-action="retry-demo"]')) toast.info('Retrying...');
  });

  // ---- Skeleton helper
  var skeletonTarget = document.getElementById('skeleton-js-demo');
  document.querySelectorAll('[data-demo-skeleton]').forEach(function (button) {
    button.addEventListener('click', async function () {
      if (!skeletonTarget || !window.Skeleton) return;
      var mode = button.dataset.demoSkeleton;
      var loading = window.Skeleton.show(skeletonTarget, { kind: 'table', rows: 3, columns: 3, label: 'Loading demo rows' });
      await sleep(mode === 'fast' ? 150 : 1500);
      loading.hide();
      if (mode === 'fail') {
        window.EmptyState.error(skeletonTarget, { size: 'compact', onRetry: function () { button.click(); } });
      } else {
        skeletonTarget.textContent = '';
        var p = document.createElement('p');
        p.className = 'text-sm text-text-primary';
        p.textContent = mode === 'fast'
          ? 'Loaded in 150ms: no skeleton was drawn.'
          : 'Loaded in 1.5s: the skeleton showed after 300ms.';
        skeletonTarget.appendChild(p);
      }
    });
  });

  // ---- Unsaved changes
  var unsavedForm = document.getElementById('unsaved-demo');
  var unsavedState = document.getElementById('unsaved-state');
  if (unsavedForm) {
    unsavedForm.addEventListener('unsavedchange', function (e) {
      if (unsavedState) unsavedState.textContent = String(e.detail.dirty);
    });
    unsavedForm.addEventListener('submit', function (e) {
      // A fetch-based save: stay on the page, then take the saved values as the baseline
      e.preventDefault();
      window.UnsavedChanges.markClean(unsavedForm);
      toast.success('Saved. The form is clean again.');
    });
  }

  var dirtyForm = document.getElementById('unsaved-dirty-demo');
  if (dirtyForm) {
    dirtyForm.addEventListener('submit', function (e) {
      e.preventDefault();
      window.UnsavedChanges.markClean(dirtyForm);
      toast.success('Saved.');
    });
  }
})();
