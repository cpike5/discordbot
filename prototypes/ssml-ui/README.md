# SSML Enhancement Interface - TTS Portal Prototypes

A comprehensive collection of HTML prototypes for the Text-to-Speech (TTS) Portal with advanced SSML (Speech Synthesis Markup Language) enhancement capabilities.

## Overview

This suite of prototypes demonstrates a complete TTS message creation and management system designed for Discord bot administration. The interface supports three progressive modes (Simple, Standard, and Pro) with increasing levels of complexity and control.

## Design System

All prototypes adhere to the established Discord Bot Admin design system with these key tokens:

- **Primary Background:** `#1d2022`
- **Secondary Background:** `#262a2d`
- **Tertiary Background:** `#2f3336`
- **Primary Text:** `#d7d3d0`
- **Secondary Text:** `#a8a5a3`
- **Accent Orange:** `#cb4e1b`
- **Accent Blue:** `#098ecf`

## Prototypes

### 1. TTS Portal Desktop (`tts-portal-desktop.html`)

Complete desktop TTS interface with all three modes side-by-side capability.

**Features:**
- Mode toggle (Simple, Standard, Pro) with segmented control
- Message textarea with real-time character counter (0/500)
- Quick preset buttons (8 presets): 🎉 Excited, 📢 Announcer, 🤖 Robot, 😊 Friendly, 😡 Angry, 🎭 Narrator, 🤫 Whisper, 🗣️ Shouting
- Voice selection dropdown with multiple voice options
- Speaking Style dropdown (Standard/Pro only) with voice-specific filtering
- Style Intensity slider (Subtle 0.5 to Intense 2.0) with labels
- Speed slider (0.5x - 2.0x) with real-time value display
- Pitch slider (0.5x - 2.0x) with real-time value display
- SSML Preview panel (Pro only) with syntax highlighting
- Copy SSML button
- Sidebar with Voice Channel status and Now Playing section
- Toast notifications for user feedback
- Responsive design with 300px fixed sidebar

**Layout:**
- Fixed sidebar (300px): Voice Channel + Now Playing sections
- Main content area: Mode toggle, message input, voice settings, SSML preview

**Interactions:**
- Mode switching shows/hides relevant sections
- Preset buttons apply speaking styles and show toast confirmation
- Sliders update displayed values in real-time
- Character counter changes color (yellow at 80%, red at 100%)
- SSML preview expands/collapses with smooth animation
- Send/Preview buttons with visual feedback

### 2. Emphasis Toolbar (`emphasis-toolbar.html`)

Standalone prototype demonstrating floating text emphasis toolbar for inline SSML formatting.

**Features:**
- Textarea with sample text
- Floating toolbar appears on text selection
- Formatting buttons:
  - **B** (Bold)
  - **⚡ Strong** (Orange emphasis)
  - **⚡ Moderate** (Blue emphasis)
  - **⏸️ Pause** (Insert pause marker)
  - **❌ Clear** (Remove formatting)
- Visual formatting indicators:
  - Strong emphasis: Orange underline + light orange background
  - Moderate emphasis: Blue underline + light blue background
  - Pause markers: Gray inline indicator `[⏸️ 500ms]`
- Formatted preview panel showing applied styling
- Toolbar animation: slides up from selection
- Touch-friendly button sizing

**Interactions:**
- Select text to show toolbar above selection
- Click formatting button to apply styling
- Toolbar closes on formatting applied or click outside
- Preview updates in real-time to show formatting

### 3. Pause Insertion Modal (`pause-modal.html`)

Modal dialog for inserting precise pause markers in TTS messages.

**Features:**
- Modal header with title and close button
- Duration slider (100ms - 3000ms)
- Quick duration buttons:
  - Short (250ms)
  - Medium (500ms, default)
  - Long (1000ms)
- Large value display (2rem font size)
- Live preview showing `[⏸️ 500ms]` example
- Cancel and Insert buttons
- Keyboard support (Escape to close)
- Backdrop click to close
- Smooth animations

**Interactions:**
- Open modal with button click
- Adjust slider or click quick buttons
- Preview updates instantly
- Active quick button highlights
- Insert Pause button confirms selection

### 4. Mobile TTS Portal (`tts-portal-mobile.html`)

Fully responsive mobile-optimized TTS interface (max-width: 768px focus, but responsive up).

**Features:**
- Stacked vertical layout (no sidebar)
- Collapsible sections:
  - Voice Settings accordion with collapse/expand animation
  - All controls accessible when expanded
- Horizontal scrolling preset carousel with snap scrolling
- Touch-optimized controls (min 44px height)
- Proper viewport scaling
- iOS-style scrolling (momentum scrolling enabled)
- Collapsible "Voice Channel" status section
- Now Playing mini-panel at top
- Bottom safe area padding for notched devices
- Toast notifications positioned for mobile
- Mode toggle on all screen sizes

**Responsive Breakpoints:**
- Mobile first (< 768px): Stacked layout, scrollable carousel
- Tablet/Desktop (≥ 768px): Enhanced spacing and layout

**Touch Interactions:**
- Tap mode buttons to switch
- Tap preset buttons to apply styles
- Slide presets carousel horizontally
- Tap collapsible headers to expand/collapse
- Tap send/preview buttons with active states

### 5. Style Selector Component (`style-selector.html`)

Comprehensive speaking style selector with voice-specific filtering.

**Features:**
- Voice dropdown with 5 options (Jenny, Nova, Guy, Sonia, Ryan)
- Speaking Style list showing:
  - Style name
  - Description/example
  - Availability badge (Available/Unavailable)
  - Voice-specific compatibility filtering
- Style Intensity slider (0.5 to 2.0):
  - Subtle label (0.5)
  - Moderate label (1.0, default)
  - Intense label (2.0)
- Live preview text showing current configuration
- Hover tooltips on style options
- Info box explaining functionality
- Disabled state styling for unsupported styles
- Voice compatibility matrix:
  - Jenny: Standard, Cheerful, Excited, Friendly, Sad, Whispering
  - Nova: All styles supported
  - Guy: All except Sad, Whispering
  - Sonia: All except Shouting
  - Ryan: All except Sad

**Interactions:**
- Select voice to update available styles
- Click style option to select (disabled styles not clickable)
- Adjust intensity slider (0.5, 1.0, 2.0 with labels)
- Hover style for tooltip with example phrase
- Preview updates with voice, style, and intensity

## Technical Specifications

### HTML Standards
- HTML5 semantic markup
- UTF-8 character encoding
- Responsive viewport meta tag
- Accessible form labels and ARIA attributes

### CSS
- CSS custom properties for design tokens
- Flexbox and Grid layouts
- CSS transitions (200ms standard, 150ms fast)
- Custom styled form controls (sliders, selects, buttons)
- Smooth animations and hover states
- Mobile-first responsive design

### JavaScript
- Vanilla JavaScript (no dependencies)
- Event delegation for dynamic content
- Real-time updates for sliders and text inputs
- Modal state management
- Toast notification system
- Collapsible accordion functionality
- Voice-style compatibility filtering

### Accessibility
- Semantic HTML structure
- Form labels associated with inputs
- Keyboard navigation support
- Focus visible states (2px outline)
- Proper color contrast ratios
- ARIA labels where applicable
- Min 44px touch targets on mobile

### Performance
- Self-contained files (no external dependencies)
- Inline CSS and JavaScript
- Minimal DOM manipulation
- Efficient event handling
- CSS animations (GPU accelerated where possible)

## Color Palette

| Token | Value | Usage |
|-------|-------|-------|
| `--color-bg-primary` | `#1d2022` | Page background |
| `--color-bg-secondary` | `#262a2d` | Section backgrounds, cards |
| `--color-bg-tertiary` | `#2f3336` | Form inputs, tertiary backgrounds |
| `--color-bg-hover` | `#363a3e` | Hover states |
| `--color-text-primary` | `#d7d3d0` | Main text |
| `--color-text-secondary` | `#a8a5a3` | Secondary text, labels |
| `--color-text-tertiary` | `#7a7876` | Tertiary text, hints |
| `--color-accent-orange` | `#cb4e1b` | Primary accent, CTA buttons |
| `--color-accent-blue` | `#098ecf` | Secondary accent, links, sliders |
| `--color-success` | `#10b981` | Success states, status indicators |
| `--color-warning` | `#f59e0b` | Warning states, character counter warning |
| `--color-error` | `#ef4444` | Error states, destructive actions |
| `--color-border-primary` | `#3f4447` | Primary borders |
| `--color-border-secondary` | `#2f3336` | Secondary borders |
| `--color-border-focus` | `#098ecf` | Focus states (accent blue) |

## File Structure

```
prototypes/ssml-ui/
├── README.md                        # This file
├── tts-portal-desktop.html         # Desktop TTS interface
├── emphasis-toolbar.html           # Floating text toolbar
├── pause-modal.html               # Pause insertion dialog
├── tts-portal-mobile.html         # Mobile-optimized TTS
└── style-selector.html            # Voice/style selector
```

## Mode Specifications

### Simple Mode
- Basic message textarea
- Voice selection only
- Speed and pitch sliders
- No presets, styles, or SSML

### Standard Mode
- Message textarea with presets
- Voice selection
- Speaking style dropdown
- Style intensity slider
- Speed and pitch sliders
- Character counter
- Toast notifications

### Pro Mode
- All Standard mode features
- SSML preview panel (collapsible)
- Syntax-highlighted SSML code
- Copy SSML button
- Advanced SSML generation

## Usage Examples

### Open Desktop Portal
```html
<!-- In production, you would navigate to: -->
https://your-domain/tts-portal/desktop
```

### Mobile Detection
The mobile prototype uses viewport-based responsive design. Test with:
- Mobile device at 375px-480px width
- Tablet at 768px width
- Desktop at 1200px+ width

### Integration Notes
1. These prototypes use inline CSS and JavaScript for standalone functionality
2. In production, extract CSS to shared stylesheets
3. Extract JavaScript to modular components
4. Add backend API calls for actual TTS generation
5. Implement WebSocket for real-time Now Playing updates

## Browser Support
- Chrome/Edge 90+
- Firefox 88+
- Safari 14+
- Mobile Safari iOS 14+
- Chrome Mobile 90+

## Known Limitations
- SSML preview is static (not actually generated)
- No real audio playback
- No actual TTS API integration
- Preview text is mock data
- No state persistence between page reloads

## Future Enhancements
- Real SSML code generation based on user input
- Integration with Azure Cognitive Services or similar
- Saved presets and templates
- Message history and replay functionality
- Advanced SSML editor with syntax validation
- Multi-language support
- Custom voice profiles
- Analytics and usage metrics

## Questions or Issues?
Refer to the design system documentation at:
`docs/articles/design-system.md`

Component library API:
`docs/articles/component-api.md`

Interactive components guide:
`docs/articles/interactive-components.md`
