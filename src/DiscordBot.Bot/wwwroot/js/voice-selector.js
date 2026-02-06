(function() {
    'use strict';

    console.log('[VoiceSelector] Module loading...');

    window.voiceSelector_getValue = function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return '';
        return container.dataset.selectedVoice || '';
    };

    window.voiceSelector_setValue = function(containerId, voiceName, suppressCallback) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const option = container.querySelector('[data-voice-value="' + voiceName + '"]');
        if (!option) return;

        const displayName = option.querySelector('.voice-selector__option-name')?.textContent || '';
        const gender = option.querySelector('.voice-selector__option-gender')?.textContent?.replace(/[()]/g, '') || '';
        const localeDisplayName = option.closest('.voice-selector__group')?.querySelector('.voice-selector__group-name')?.textContent || '';

        voiceSelector_updateSelection(containerId, voiceName, displayName, gender, localeDisplayName, suppressCallback);
    };

    window.voiceSelector_toggle = function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const trigger = container.querySelector('.voice-selector__trigger');
        const dropdown = container.querySelector('.voice-selector__dropdown');
        if (!trigger || !dropdown) return;

        const isExpanded = trigger.getAttribute('aria-expanded') === 'true';

        if (isExpanded) {
            voiceSelector_close(containerId);
        } else {
            voiceSelector_open(containerId);
        }
    };

    window.voiceSelector_open = function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const trigger = container.querySelector('.voice-selector__trigger');
        const dropdown = container.querySelector('.voice-selector__dropdown');
        const searchInput = container.querySelector('.voice-selector__search');
        if (!trigger || !dropdown) return;

        document.querySelectorAll('.voice-selector__trigger[aria-expanded="true"]').forEach(function(otherTrigger) {
            if (otherTrigger !== trigger) {
                const otherContainer = otherTrigger.closest('.voice-selector');
                if (otherContainer) {
                    voiceSelector_close(otherContainer.id);
                }
            }
        });

        trigger.setAttribute('aria-expanded', 'true');
        dropdown.style.display = 'flex';

        if (searchInput) {
            setTimeout(function() { searchInput.focus(); }, 50);
        }

        setTimeout(function() {
            document.addEventListener('click', voiceSelector_handleClickOutside);
        }, 0);

        if (!dropdown._keydownListenerAttached) {
            dropdown.addEventListener('keydown', voiceSelector_handleKeyDown);
            dropdown._keydownListenerAttached = true;
        }
    };

    window.voiceSelector_close = function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const trigger = container.querySelector('.voice-selector__trigger');
        const dropdown = container.querySelector('.voice-selector__dropdown');
        const searchInput = container.querySelector('.voice-selector__search');
        if (!trigger || !dropdown) return;

        trigger.setAttribute('aria-expanded', 'false');
        dropdown.style.display = 'none';

        if (searchInput) {
            searchInput.value = '';
            voiceSelector_search(containerId);
        }

        trigger.focus();
        document.removeEventListener('click', voiceSelector_handleClickOutside);
        dropdown.removeEventListener('keydown', voiceSelector_handleKeyDown);
        dropdown._keydownListenerAttached = false;
    };

    window.voiceSelector_handleClickOutside = function(event) {
        const openTrigger = document.querySelector('.voice-selector__trigger[aria-expanded="true"]');
        if (!openTrigger) return;

        const container = openTrigger.closest('.voice-selector');
        if (!container) return;

        if (!container.contains(event.target)) {
            voiceSelector_close(container.id);
        }
    };

    window.voiceSelector_handleKeyDown = function(event) {
        const dropdown = event.currentTarget;
        const container = dropdown.closest('.voice-selector');
        if (!container) return;

        const searchInput = container.querySelector('.voice-selector__search');
        const visibleOptions = Array.from(container.querySelectorAll('.voice-selector__option:not(.voice-selector__option--hidden)'));

        if (event.key === 'Escape') {
            event.preventDefault();
            voiceSelector_close(container.id);
        } else if (event.key === 'ArrowDown') {
            event.preventDefault();
            const currentIndex = visibleOptions.findIndex(function(opt) { return opt === document.activeElement; });
            const nextIndex = currentIndex + 1;
            if (nextIndex < visibleOptions.length) {
                visibleOptions[nextIndex].focus();
            } else if (visibleOptions.length > 0) {
                visibleOptions[0].focus();
            }
        } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            const currentIndex = visibleOptions.findIndex(function(opt) { return opt === document.activeElement; });
            const prevIndex = currentIndex - 1;
            if (prevIndex >= 0) {
                visibleOptions[prevIndex].focus();
            } else if (visibleOptions.length > 0) {
                visibleOptions[visibleOptions.length - 1].focus();
            }
        } else if (event.key === 'Home') {
            event.preventDefault();
            if (visibleOptions.length > 0) {
                visibleOptions[0].focus();
            }
        } else if (event.key === 'End') {
            event.preventDefault();
            if (visibleOptions.length > 0) {
                visibleOptions[visibleOptions.length - 1].focus();
            }
        } else if (event.key === 'Enter') {
            if (document.activeElement !== searchInput && visibleOptions.includes(document.activeElement)) {
                event.preventDefault();
                document.activeElement.click();
            }
        }
    };

    window.voiceSelector_select = function(containerId, voiceValue, displayName, gender, localeDisplayName) {
        console.log('[VoiceSelector] select called:', { containerId, voiceValue, displayName, gender, localeDisplayName });
        voiceSelector_updateSelection(containerId, voiceValue, displayName, gender, localeDisplayName, false);
        voiceSelector_close(containerId);
        console.log('[VoiceSelector] select completed');
    };

    /**
     * Selects a voice from a button element's data attributes.
     * This is safer than inline onclick with interpolated values since data attributes are HTML-escaped.
     */
    window.voiceSelector_selectFromButton = function(button) {
        if (!button) return;

        // Find container by traversing DOM instead of relying on data attribute
        var container = button.closest('.voice-selector');
        if (!container || !container.id) {
            console.warn('[VoiceSelector] Could not find container for button');
            return;
        }

        var containerId = container.id;
        var voiceValue = button.dataset.voiceValue;
        var displayName = button.dataset.voiceDisplay;
        var gender = button.dataset.voiceGender;
        var localeDisplayName = button.dataset.voiceLocale;

        console.log('[VoiceSelector] selectFromButton:', { containerId, voiceValue, displayName, gender, localeDisplayName });

        if (voiceValue) {
            voiceSelector_select(containerId, voiceValue, displayName, gender, localeDisplayName);
        }
    };

    window.voiceSelector_updateSelection = function(containerId, voiceValue, displayName, gender, localeDisplayName, suppressCallback) {
        console.log('[VoiceSelector] updateSelection called:', { containerId, voiceValue });
        const container = document.getElementById(containerId);
        if (!container) {
            console.warn('[VoiceSelector] Container not found:', containerId);
            return;
        }

        const trigger = container.querySelector('.voice-selector__trigger');
        const triggerText = container.querySelector('.voice-selector__trigger-text');
        if (!trigger || !triggerText) {
            console.warn('[VoiceSelector] Trigger elements not found');
            return;
        }

        container.dataset.selectedVoice = voiceValue;
        var newText = displayName + ' (' + gender + ') - ' + localeDisplayName;
        console.log('[VoiceSelector] Updating trigger text to:', newText);
        triggerText.textContent = newText;

        container.querySelectorAll('.voice-selector__option').forEach(function(option) {
            const isSelected = option.dataset.voiceValue === voiceValue;
            option.setAttribute('aria-selected', isSelected.toString());

            if (isSelected) {
                option.classList.add('voice-selector__option--selected');
                if (!option.querySelector('.voice-selector__option-checkmark')) {
                    const checkmark = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                    checkmark.setAttribute('class', 'voice-selector__option-checkmark');
                    checkmark.setAttribute('fill', 'none');
                    checkmark.setAttribute('viewBox', '0 0 24 24');
                    checkmark.setAttribute('stroke', 'currentColor');
                    checkmark.setAttribute('aria-hidden', 'true');
                    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
                    path.setAttribute('stroke-linecap', 'round');
                    path.setAttribute('stroke-linejoin', 'round');
                    path.setAttribute('stroke-width', '2');
                    path.setAttribute('d', 'M4.5 12.75l6 6 9-13.5');
                    checkmark.appendChild(path);
                    option.appendChild(checkmark);
                }
            } else {
                option.classList.remove('voice-selector__option--selected');
                const checkmark = option.querySelector('.voice-selector__option-checkmark');
                if (checkmark) {
                    checkmark.remove();
                }
            }
        });

        if (!suppressCallback) {
            const callback = container.dataset.callbackVoice;
            if (callback && typeof window[callback] === 'function') {
                window[callback](voiceValue);
            }
        }
    };

    window.voiceSelector_search = function(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const searchInput = container.querySelector('.voice-selector__search');
        const groups = container.querySelectorAll('.voice-selector__group');
        const emptyState = container.querySelector('.voice-selector__dropdown > .voice-selector__empty');
        if (!searchInput) return;

        const query = searchInput.value.toLowerCase().trim();
        let hasVisibleResults = false;

        groups.forEach(function(group) {
            const groupName = group.querySelector('.voice-selector__group-name')?.textContent?.toLowerCase() || '';
            const options = group.querySelectorAll('.voice-selector__option');
            let hasVisibleOptions = false;

            const groupMatches = groupName.includes(query);

            options.forEach(function(option) {
                const searchText = option.dataset.searchText || '';
                const matches = searchText.includes(query) || groupMatches;

                if (matches) {
                    option.classList.remove('voice-selector__option--hidden');
                    hasVisibleOptions = true;
                    hasVisibleResults = true;
                } else {
                    option.classList.add('voice-selector__option--hidden');
                }
            });

            if (hasVisibleOptions) {
                group.classList.remove('voice-selector__group--hidden');
                if (query) {
                    const header = group.querySelector('.voice-selector__group-header');
                    if (header) {
                        header.setAttribute('aria-expanded', 'true');
                    }
                }
            } else {
                group.classList.add('voice-selector__group--hidden');
            }
        });

        if (emptyState) {
            if (hasVisibleResults || !query) {
                emptyState.style.display = 'none';
            } else {
                emptyState.style.display = 'block';
            }
        }
    };

    window.voiceSelector_toggleGroup = function(containerId, locale) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const group = container.querySelector('[data-locale="' + locale + '"]');
        if (!group) return;

        const header = group.querySelector('.voice-selector__group-header');
        if (!header) return;

        const isExpanded = header.getAttribute('aria-expanded') === 'true';
        header.setAttribute('aria-expanded', (!isExpanded).toString());
    };

    function initVoiceSelectors() {
        const options = document.querySelectorAll('.voice-selector__option');
        console.log('[VoiceSelector] initVoiceSelectors, found', options.length, 'options');

        options.forEach(function(option) {
            option.addEventListener('click', function(e) {
                e.preventDefault();
                e.stopPropagation();

                const container = this.closest('.voice-selector');
                if (!container) return;

                const voiceValue = this.dataset.voiceValue;
                const displayName = this.dataset.voiceDisplay;
                const gender = this.dataset.voiceGender;
                const localeDisplayName = this.dataset.voiceLocale;

                if (voiceValue) {
                    voiceSelector_select(container.id, voiceValue, displayName, gender, localeDisplayName);
                }
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initVoiceSelectors);
    } else {
        initVoiceSelectors();
    }

    console.log('[VoiceSelector] Module loaded');
})();
