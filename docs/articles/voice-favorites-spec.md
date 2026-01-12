# Voice Favorites System Specification

## Overview

Allow users to favorite TTS voices in the TTS Portal. Favorites are stored in browser localStorage and displayed as interactive tags/chips above the voice dropdown for quick access.

## User Experience

### Visual Layout

```
┌─────────────────────────────────────────────────┐
│ Voice                                           │
│ ┌─────────────────────────────────────────────┐│
│ │ ⭐ Jenny (Female)  ⭐ Mayu (Female)  × ...  ││  ← Favorite tags (max 3 visible + "show more")
│ └─────────────────────────────────────────────┘│
│ ┌─────────────────────────────────────────────┐│
│ │ Select a voice...                        ⌄ ││  ← Dropdown (with star icons)
│ └─────────────────────────────────────────────┘│
└─────────────────────────────────────────────────┘
```

### Interactions

1. **Adding a Favorite**
   - Click the star icon next to any voice in the dropdown
   - Star turns solid/filled
   - Voice appears as a chip in the favorites row above
   - Maximum 10 favorites allowed

2. **Removing a Favorite**
   - Click the star icon again in the dropdown (un-star)
   - OR click the × button on the favorite chip
   - Chip is removed from favorites row
   - Star becomes outline/hollow in dropdown

3. **Using a Favorite**
   - Click a favorite chip
   - Voice is automatically selected in the dropdown
   - Dropdown closes if it was open

4. **Overflow Handling**
   - Display first 3 favorites as chips
   - If more than 3 favorites exist, show "+N more" button
   - Click "+N more" to expand and show all favorites
   - Click "Show less" to collapse back to 3

## Technical Implementation

### Data Storage

**localStorage Key**: `tts_favorite_voices`

**Data Structure**:
```json
{
  "favorites": [
    "en-US-JennyNeural",
    "ja-JP-MayuNeural",
    "es-ES-AlvaroNeural"
  ]
}
```

**Persistence Rules**:
- Stored per-browser, not per-guild or per-user
- No expiration (persist indefinitely)
- Maximum 10 favorites (enforced on add)
- Order preserved (insertion order = display order)

### HTML Structure

**Favorites Container** (add above voice dropdown):
```html
<!-- Favorites Row -->
<div id="favoriteVoicesContainer" class="favorite-voices-container hidden">
    <div id="favoriteVoicesList" class="favorite-voices-list">
        <!-- Chips rendered here dynamically -->
    </div>
    <button id="showMoreFavorites" class="show-more-btn hidden">
        +<span id="moreCount">0</span> more
    </button>
</div>
```

**Favorite Chip Template**:
```html
<button class="favorite-chip" data-voice-id="en-US-JennyNeural">
    <span class="favorite-chip-text">Jenny (Female)</span>
    <span class="favorite-chip-remove" aria-label="Remove favorite">×</span>
</button>
```

**Voice Dropdown Option** (add star icon):
```html
<optgroup label="English (US)">
    <option value="en-US-JennyNeural">
        ⭐ Jenny (Female)  <!-- Star if favorited -->
    </option>
    <option value="en-US-GuyNeural">
        ☆ Guy (Male)      <!-- Hollow star if not favorited -->
    </option>
</optgroup>
```

**Note on Star Icons**:
Since `<option>` elements have limited styling support and emoji can be inconsistent:
- Alternative 1: Add custom dropdown with star buttons (more complex)
- Alternative 2: Add "Favorite" button next to dropdown (simpler)
- **Recommended**: Use Alternative 2 for MVP

**Revised Layout with Favorite Button**:
```html
<div class="voice-selector-group">
    <label for="voiceSelect">Voice</label>

    <!-- Favorites Row -->
    <div id="favoriteVoicesContainer" class="favorite-voices-container hidden">
        <!-- Chips here -->
    </div>

    <!-- Dropdown + Favorite Button -->
    <div class="voice-controls">
        <select id="voiceSelect">
            <!-- Options -->
        </select>
        <button id="toggleFavoriteBtn"
                class="favorite-btn"
                title="Add to favorites"
                type="button">
            <svg class="star-icon"><!-- Star SVG --></svg>
        </button>
    </div>
</div>
```

### CSS Styling

```css
/* Favorites Container */
.favorite-voices-container {
    margin-bottom: 0.75rem;
    padding: 0.75rem;
    background-color: rgba(88, 101, 242, 0.05);
    border: 1px solid rgba(88, 101, 242, 0.2);
    border-radius: 0.375rem;
}

.favorite-voices-container.hidden {
    display: none;
}

/* Favorites List */
.favorite-voices-list {
    display: flex;
    flex-wrap: wrap;
    gap: 0.5rem;
    margin-bottom: 0;
}

.favorite-voices-list.collapsed .favorite-chip:nth-child(n+4) {
    display: none;
}

/* Favorite Chip */
.favorite-chip {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.375rem 0.75rem;
    background-color: #5865f2;
    color: white;
    border: none;
    border-radius: 9999px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
}

.favorite-chip:hover {
    background-color: #4752c4;
    transform: translateY(-1px);
}

.favorite-chip-text {
    user-select: none;
}

.favorite-chip-remove {
    font-size: 1.25rem;
    line-height: 1;
    opacity: 0.8;
    transition: opacity 0.2s;
}

.favorite-chip-remove:hover {
    opacity: 1;
}

/* Show More Button */
.show-more-btn {
    display: inline-flex;
    align-items: center;
    padding: 0.375rem 0.75rem;
    background-color: transparent;
    color: #5865f2;
    border: 1px solid #5865f2;
    border-radius: 9999px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
    margin-top: 0.5rem;
}

.show-more-btn:hover {
    background-color: rgba(88, 101, 242, 0.1);
}

.show-more-btn.hidden {
    display: none;
}

/* Voice Controls Row */
.voice-controls {
    display: flex;
    gap: 0.5rem;
    align-items: stretch;
}

.voice-controls select {
    flex: 1;
}

/* Favorite Button */
.favorite-btn {
    width: 42px;
    height: 42px;
    padding: 0;
    background-color: #262a2d;
    border: 1px solid #3f4447;
    border-radius: 0.375rem;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: all 0.2s;
    flex-shrink: 0;
}

.favorite-btn:hover {
    background-color: #2f3338;
    border-color: #5865f2;
}

.favorite-btn.favorited {
    background-color: rgba(88, 101, 242, 0.1);
    border-color: #5865f2;
}

.favorite-btn .star-icon {
    width: 20px;
    height: 20px;
    fill: #949ba4;
    transition: fill 0.2s;
}

.favorite-btn.favorited .star-icon {
    fill: #5865f2;
}

.favorite-btn:hover .star-icon {
    fill: #5865f2;
}
```

### JavaScript Implementation

**Location**: `src/DiscordBot.Bot/wwwroot/js/portal-tts.js`

**Module Structure**:
```javascript
const VoiceFavorites = (function() {
    const STORAGE_KEY = 'tts_favorite_voices';
    const MAX_FAVORITES = 10;
    const VISIBLE_COUNT = 3;

    let favorites = [];
    let isExpanded = false;

    // Load favorites from localStorage
    function load() {
        try {
            const data = localStorage.getItem(STORAGE_KEY);
            if (data) {
                const parsed = JSON.parse(data);
                favorites = parsed.favorites || [];
            }
        } catch (error) {
            console.error('[VoiceFavorites] Failed to load:', error);
            favorites = [];
        }
    }

    // Save favorites to localStorage
    function save() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify({
                favorites: favorites
            }));
        } catch (error) {
            console.error('[VoiceFavorites] Failed to save:', error);
        }
    }

    // Add a voice to favorites
    function add(voiceId) {
        if (favorites.includes(voiceId)) {
            return false; // Already favorited
        }
        if (favorites.length >= MAX_FAVORITES) {
            alert(`Maximum ${MAX_FAVORITES} favorites allowed`);
            return false;
        }
        favorites.push(voiceId);
        save();
        return true;
    }

    // Remove a voice from favorites
    function remove(voiceId) {
        const index = favorites.indexOf(voiceId);
        if (index === -1) return false;
        favorites.splice(index, 1);
        save();
        return true;
    }

    // Toggle a voice favorite status
    function toggle(voiceId) {
        if (favorites.includes(voiceId)) {
            return remove(voiceId);
        } else {
            return add(voiceId);
        }
    }

    // Check if a voice is favorited
    function isFavorited(voiceId) {
        return favorites.includes(voiceId);
    }

    // Get all favorites
    function getAll() {
        return [...favorites];
    }

    // Render the favorites UI
    function render() {
        const container = document.getElementById('favoriteVoicesContainer');
        const list = document.getElementById('favoriteVoicesList');
        const showMoreBtn = document.getElementById('showMoreFavorites');
        const moreCount = document.getElementById('moreCount');
        const voiceSelect = document.getElementById('voiceSelect');

        if (!container || !list || !voiceSelect) return;

        // Clear list
        list.innerHTML = '';

        // Hide container if no favorites
        if (favorites.length === 0) {
            container.classList.add('hidden');
            return;
        }

        // Show container
        container.classList.remove('hidden');

        // Render chips
        favorites.forEach((voiceId, index) => {
            const option = voiceSelect.querySelector(`option[value="${voiceId}"]`);
            if (!option) return; // Voice no longer exists

            const displayName = option.textContent.trim();

            const chip = document.createElement('button');
            chip.className = 'favorite-chip';
            chip.dataset.voiceId = voiceId;
            chip.type = 'button';

            chip.innerHTML = `
                <span class="favorite-chip-text">${displayName}</span>
                <span class="favorite-chip-remove" aria-label="Remove favorite">×</span>
            `;

            // Click chip text = select voice
            chip.querySelector('.favorite-chip-text').addEventListener('click', (e) => {
                e.stopPropagation();
                voiceSelect.value = voiceId;
                voiceSelect.dispatchEvent(new Event('change'));
            });

            // Click remove = unfavorite
            chip.querySelector('.favorite-chip-remove').addEventListener('click', (e) => {
                e.stopPropagation();
                remove(voiceId);
                render();
                updateFavoriteButton();
            });

            list.appendChild(chip);
        });

        // Handle overflow
        if (favorites.length > VISIBLE_COUNT) {
            list.classList.add('collapsed');
            showMoreBtn.classList.remove('hidden');
            moreCount.textContent = favorites.length - VISIBLE_COUNT;
        } else {
            list.classList.remove('collapsed');
            showMoreBtn.classList.add('hidden');
        }
    }

    // Toggle expanded state
    function toggleExpanded() {
        isExpanded = !isExpanded;
        const list = document.getElementById('favoriteVoicesList');
        const showMoreBtn = document.getElementById('showMoreFavorites');

        if (isExpanded) {
            list.classList.remove('collapsed');
            showMoreBtn.textContent = 'Show less';
        } else {
            list.classList.add('collapsed');
            const moreCount = favorites.length - VISIBLE_COUNT;
            showMoreBtn.innerHTML = `+<span id="moreCount">${moreCount}</span> more`;
        }
    }

    // Update the favorite button state
    function updateFavoriteButton() {
        const voiceSelect = document.getElementById('voiceSelect');
        const favoriteBtn = document.getElementById('toggleFavoriteBtn');
        if (!voiceSelect || !favoriteBtn) return;

        const selectedVoice = voiceSelect.value;
        if (isFavorited(selectedVoice)) {
            favoriteBtn.classList.add('favorited');
            favoriteBtn.title = 'Remove from favorites';
        } else {
            favoriteBtn.classList.remove('favorited');
            favoriteBtn.title = 'Add to favorites';
        }
    }

    // Initialize
    function init() {
        load();
        render();
        updateFavoriteButton();

        // Event listeners
        const voiceSelect = document.getElementById('voiceSelect');
        const favoriteBtn = document.getElementById('toggleFavoriteBtn');
        const showMoreBtn = document.getElementById('showMoreFavorites');

        if (voiceSelect) {
            voiceSelect.addEventListener('change', updateFavoriteButton);
        }

        if (favoriteBtn) {
            favoriteBtn.addEventListener('click', () => {
                const voiceId = voiceSelect.value;
                if (!voiceId) return;

                toggle(voiceId);
                render();
                updateFavoriteButton();
            });
        }

        if (showMoreBtn) {
            showMoreBtn.addEventListener('click', toggleExpanded);
        }
    }

    return {
        init,
        add,
        remove,
        toggle,
        isFavorited,
        getAll,
        render,
        updateFavoriteButton
    };
})();

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => VoiceFavorites.init());
} else {
    VoiceFavorites.init();
}
```

**Integration with existing code**:
Add to the module initialization section:
```javascript
// In init() function, after loading saved voice:
VoiceFavorites.init();
```

### Star Icon SVG

```html
<svg class="star-icon" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
    <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
</svg>
```

## Implementation Phases

### Phase 1: Core Functionality (MVP)
- [ ] Add favorites container HTML above dropdown
- [ ] Add favorite button next to dropdown
- [ ] Implement localStorage read/write
- [ ] Add/remove favorites via button
- [ ] Update button state when dropdown changes
- [ ] Render favorite chips
- [ ] Click chip to select voice
- [ ] Click × to remove favorite

### Phase 2: Polish
- [ ] Add CSS animations (fade in/out, slide)
- [ ] Implement overflow handling (+N more)
- [ ] Add keyboard navigation (Tab, Enter, Escape)
- [ ] Add ARIA labels for accessibility
- [ ] Add empty state message ("No favorites yet")
- [ ] Add success toast on favorite added
- [ ] Add confirmation prompt if at max favorites

### Phase 3: Enhancements (Future)
- [ ] Drag to reorder favorites
- [ ] Import/export favorites
- [ ] Sync favorites across devices (requires backend)
- [ ] Voice preview on chip hover
- [ ] Search/filter within favorites

## Testing Checklist

- [ ] Favorites persist after page reload
- [ ] Favorites persist after browser restart
- [ ] Maximum 10 favorites enforced
- [ ] Removing favorite updates UI immediately
- [ ] Selecting favorite updates dropdown
- [ ] Button state matches current selection
- [ ] Works with all 37 voices
- [ ] Works when dropdown is empty
- [ ] Works when voice is removed from list
- [ ] No console errors
- [ ] Mobile responsive (chips wrap properly)
- [ ] Keyboard accessible
- [ ] Screen reader accessible

## Edge Cases

1. **Favorited voice removed from list**: Skip rendering that chip
2. **localStorage quota exceeded**: Show error, fail gracefully
3. **localStorage disabled**: Show message, disable feature
4. **Corrupt localStorage data**: Clear and start fresh
5. **Race condition on rapid clicks**: Debounce toggle action
6. **Empty voice selection**: Disable favorite button
7. **Mobile narrow viewport**: Chips stack vertically

## Accessibility (WCAG 2.1 AA)

- Star button has descriptive `aria-label`
- Chips have `role="button"` and keyboard support
- Remove × button has `aria-label="Remove {voice} from favorites"`
- Favorites container has `aria-label="Favorite voices"`
- Focus visible styles on all interactive elements
- Color contrast ratio ≥ 4.5:1 for text
- Touch targets ≥ 44x44px for mobile

## Files to Modify

1. **HTML**: `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml`
   - Add favorites container
   - Add favorite button next to dropdown

2. **CSS**: `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` (inline styles section)
   - Add favorites styling

3. **JS**: `src/DiscordBot.Bot/wwwroot/js/portal-tts.js`
   - Add VoiceFavorites module
   - Integrate with existing init()

## Future Considerations

- **Backend integration**: Store favorites in user profile for cross-device sync
- **Analytics**: Track which voices are most favorited
- **Recommendations**: Suggest voices based on favorites
- **Sharing**: Generate shareable favorite voice packs
