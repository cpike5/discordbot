# Discord Bot Verification UI Specifications

**Issue:** #118
**Component:** LinkDiscord.cshtml
**Status:** Specification
**Last Updated:** 2025-12-09

---

## Overview

This document specifies the UI design for the "Link via Discord Bot" verification alternative on the LinkDiscord page. Users can authenticate by running a `/verify-account` command in Discord instead of using OAuth, providing an alternative verification method when OAuth is unavailable.

The design integrates seamlessly with the existing LinkDiscord page styling while introducing new state transitions for the verification workflow.

---

## Design Tokens Reference

All values reference the design system at `docs/articles/design-system.md`.

### Color Palette

| Token | Value | Usage |
|-------|-------|-------|
| `bg-primary` | `#1d2022` | Main background |
| `bg-secondary` | `#262a2d` | Card backgrounds |
| `bg-tertiary` | `#2f3336` | Elevated elements |
| `text-primary` | `#d7d3d0` | Primary text |
| `text-secondary` | `#a8a5a3` | Secondary text |
| `text-tertiary` | `#7a7876` | Tertiary text (disabled, muted) |
| `accent-orange` | `#cb4e1b` | Primary CTA buttons |
| `accent-orange-hover` | `#e5591f` | Orange button hover |
| `accent-blue` | `#098ecf` | Secondary actions |
| `accent-blue-hover` | `#0ba3ea` | Blue button hover |
| `success` | `#10b981` | Success states |
| `success-bg` | `#10b98120` | Success background (20% opacity) |
| `info` | `#06b6d4` | Informational content |
| `info-bg` | `#06b6d420` | Info background |
| `error` | `#ef4444` | Error states |
| `error-bg` | `#ef444420` | Error background |
| `border-primary` | `#3f4447` | Default borders |

### Typography

| Element | Size | Weight | Line Height | Usage |
|---------|------|--------|-------------|-------|
| Card Title (H3) | 1.5rem (24px) | 600 | 1.35 | Section headers |
| Label | 0.875rem (14px) | 600 | 1.4 | Form labels |
| Body | 1rem (16px) | 400 | 1.5 | Main content |
| Small | 0.875rem (14px) | 400 | 1.4 | Helper text, hints |
| Extra Small | 0.75rem (12px) | 400 | 1.3 | Captions, timestamps |
| Monospace (Code) | 0.875rem (14px) | 400 | 1.4 | Verification code display |

### Spacing

| Value | Pixels | Usage |
|-------|--------|-------|
| `space-2` | 8px | Small gaps, inline spacing |
| `space-3` | 12px | Spacing within components |
| `space-4` | 16px | Default padding/margins |
| `space-6` | 24px | Large spacing, section gaps |
| `space-8` | 32px | Major spacing between sections |

### Border Radius

| Value | Pixels | Usage |
|-------|--------|-------|
| `radius-md` | 6px | Button radius |
| `radius-lg` | 8px | Card radius |

### Shadows

| Value | Usage |
|-------|-------|
| `shadow-sm` | Card/input subtle elevation |
| `shadow-md` | Modal/dropdown elevation |

---

## Component Structure

The verification section is added to the LinkDiscord page below the primary OAuth button or as an alternative within the card.

### Layout Hierarchy

```
LinkDiscord Card (bg-secondary)
├── OAuth Button Section
│   ├── Primary Button: "Link Discord Account"
│   └── Info Note (info-bg)
├── [Optional Divider: "OR"]
└── Bot Verification Section (New)
    ├── Heading & Description
    ├── Initial State / Pending State / Success State / Error State
```

---

## 1. Initial State (No Pending Verification)

### Purpose
Display the bot verification option when no verification is in progress.

### Visual Hierarchy

**Container:**
- Background: `bg-secondary`
- Border: 1px solid `border-primary`
- Padding: 24px (`space-6`)
- Border Radius: 8px (`radius-lg`)
- Margin Top: 24px (separator from OAuth section)

**Divider (Optional, if OAuth is present):**
- Text: "OR"
- Layout: Centered with horizontal lines
- Color: `text-tertiary`
- Margin: 24px top/bottom

```
═══════════════════════════════════════════════════════════════════
              OR
═══════════════════════════════════════════════════════════════════
```

**Heading & Description:**
- Heading: "Link via Discord Bot" (H3, 24px, weight 600)
- Color: `text-primary`
- Description Text: (14px, weight 400)
- Color: `text-secondary`
- Max Width: 600px

**Description Content:**
```
Use your Discord username to verify your account. Run the /verify-account
command in any Discord server where our bot is present, and follow the
on-screen instructions to link your account.
```

**Button:**
- Style: Primary button (btn btn-primary)
- Text: "Start Verification"
- Icon: Optional chevron-right (Hero Icons, 16px)
- Padding: 10px 20px (`py-2.5 px-6`)
- Font Size: 14px, weight 600
- Color: White on `accent-orange`
- Hover: `accent-orange-hover`
- Focus: 2px solid `accent-blue` outline, 2px offset

### HTML Structure (Razor)

```html
@if (!Model.HasPendingVerification && !Model.IsVerificationComplete)
{
    <!-- Divider (if OAuth is present) -->
    @if (Model.IsDiscordOAuthConfigured)
    {
        <div class="flex items-center gap-4 my-6">
            <div class="flex-1 h-px bg-border-primary"></div>
            <span class="text-xs text-text-tertiary font-semibold uppercase">OR</span>
            <div class="flex-1 h-px bg-border-primary"></div>
        </div>
    }

    <!-- Initial State Section -->
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-6 shadow-sm">
        <h3 class="text-h3 font-semibold text-text-primary mb-2">
            Link via Discord Bot
        </h3>

        <p class="text-base text-text-secondary mb-6 max-w-2xl">
            Use your Discord username to verify your account. Run the
            <code class="font-mono bg-bg-tertiary px-2 py-1 rounded text-sm">/verify-account</code>
            command in any Discord server where our bot is present, and follow
            the on-screen instructions to link your account.
        </p>

        <!-- Button -->
        <button
            type="button"
            onclick="initiateVerification()"
            class="btn btn-primary inline-flex items-center gap-2.5">
            <span>Start Verification</span>
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
            </svg>
        </button>
    </div>
}
```

---

## 2. Pending Verification State (Awaiting Discord Command)

### Purpose
Guide the user through the verification process after initiating the flow.

### Visual Hierarchy

**Container:**
- Background: `bg-secondary`
- Border: 1px solid `accent-blue` (emphasized for active state)
- Padding: 24px (`space-6`)
- Border Radius: 8px (`radius-lg`)
- Shadow: `shadow-md`

**Info Box:**
- Background: `info-bg` (#06b6d420)
- Border: 1px solid `info-border` (#06b6d450)
- Border Radius: 6px (`radius-md`)
- Padding: 16px (`space-4`)
- Margin Bottom: 24px (`space-6`)
- Icon: Info circle (Hero Icons, 20px, color `info`)

**Instructions:**
- Heading: "Next Step" (14px, weight 600, color `text-primary`)
- Body: Instruction text (14px, weight 400, color `text-secondary`)
- Code Badge: Monospace, background `bg-tertiary`, padding 8px 12px

**Expiry Countdown:**
- Label: "Expires in:" (12px, weight 600, color `text-secondary`)
- Time Display: "15:42" (16px, weight 700, monospace, color `accent-blue`)
- Warning Text: Appears at < 5 minutes remaining (12px, color `warning`)

**Input Group:**
- Label: "Verification Code" (14px, weight 600)
- Input Field:
  - Width: 100% (max 400px)
  - Padding: 10px 14px
  - Font Family: Monospace (`font-mono`)
  - Font Size: 14px
  - Text Transform: Uppercase
  - Letter Spacing: 2px
  - Placeholder: "ABC-123"
  - Border: 1px solid `border-primary`
  - Border Radius: 6px (`radius-md`)
  - Background: `bg-primary`
  - Color: `text-primary`
  - Focus: Border `accent-blue`, shadow `0 0 0 3px rgba(9, 142, 207, 0.15)`
  - Error State: Border `error`, red focus shadow

**Button Group:**
- Primary Button: "Verify & Link" (btn btn-primary)
- Secondary Button: "Cancel" (btn btn-secondary, ghost style)
- Layout: Flex, gap 12px (`space-3`)

### HTML Structure (Razor)

```html
@if (Model.HasPendingVerification && !Model.IsVerificationComplete)
{
    <!-- Pending Verification Section -->
    <div class="bg-bg-secondary border border-accent-blue rounded-lg p-6 shadow-md">

        <!-- Info Box -->
        <div class="flex gap-3 p-4 bg-info-bg border border-info-border rounded-md mb-6">
            <svg class="w-5 h-5 text-info flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24" aria-hidden="true">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div class="flex-1">
                <h4 class="text-sm font-semibold text-text-primary">Next Step</h4>
                <p class="text-sm text-text-secondary mt-1">
                    Run the verification command in Discord to get your code
                </p>
            </div>
        </div>

        <!-- Instructions -->
        <div class="mb-6 p-4 bg-bg-primary rounded-md border border-border-secondary">
            <p class="text-sm text-text-secondary mb-3">
                Run this command in any Discord server:
            </p>
            <div class="flex items-center justify-between p-3 bg-bg-tertiary rounded border border-border-primary">
                <code class="font-mono text-sm text-accent-blue">/verify-account</code>
                <button
                    type="button"
                    class="text-xs text-text-secondary hover:text-text-primary transition"
                    onclick="copyToClipboard('/verify-account')">
                    Copy
                </button>
            </div>
        </div>

        <!-- Expiry Countdown -->
        <div class="flex items-baseline gap-2 mb-6 p-3 bg-bg-primary rounded-md border border-border-secondary">
            <span class="text-xs font-semibold text-text-secondary uppercase">Expires in:</span>
            <span class="text-lg font-mono font-bold text-accent-blue">@(Model.ExpiryMinutes):@(Model.ExpirySeconds:D2)</span>
            @if (Model.ExpiryMinutes < 5)
            {
                <span class="text-xs text-warning ml-auto">
                    <svg class="w-3.5 h-3.5 inline mr-1" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                        <path fill-rule="evenodd" d="M8.257 3.099c.765-1.36 2.722-1.36 3.486 0l5.58 9.92c.75 1.334-.213 2.98-1.742 2.98H4.42c-1.53 0-2.493-1.646-1.743-2.98l5.58-9.92zM11 13a1 1 0 11-2 0 1 1 0 012 0zm-1-8a1 1 0 00-1 1v3a1 1 0 002 0V6a1 1 0 00-1-1z" clip-rule="evenodd" />
                    </svg>
                    Expiring soon
                </span>
            }
        </div>

        <!-- Code Input -->
        <div class="mb-6">
            <label for="verification-code" class="form-label">Verification Code</label>
            <p class="text-xs text-text-tertiary mt-1 mb-3">
                Enter the code shown in Discord (format: ABC-123)
            </p>
            <input
                type="text"
                id="verification-code"
                name="verificationCode"
                class="form-input font-mono text-center tracking-widest uppercase w-full max-w-sm"
                placeholder="ABC-123"
                maxlength="7"
                required
                @if (!string.IsNullOrEmpty(Model.VerificationError))
                {
                    <text> aria-describedby="code-error" aria-invalid="true"</text>
                }
            />
            @if (!string.IsNullOrEmpty(Model.VerificationError))
            {
                <div id="code-error" class="form-error mt-2">
                    <svg class="w-4 h-4 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                        <path fill-rule="evenodd" d="M18.101 12.93a1 1 0 00-1.414-1.414l-3.85 3.85-1.414-1.414a1 1 0 00-1.414 1.414l2.828 2.828a1 1 0 001.414 0l5.656-5.656z" clip-rule="evenodd" />
                    </svg>
                    <span>@Model.VerificationError</span>
                </div>
            }
        </div>

        <!-- Buttons -->
        <div class="flex gap-3 pt-2">
            <form method="post" asp-page-handler="VerifyCode" class="flex-1">
                <input type="hidden" name="verificationCode" />
                <button type="submit" class="btn btn-primary w-full">
                    Verify & Link
                </button>
            </form>
            <button
                type="button"
                onclick="cancelVerification()"
                class="btn btn-secondary">
                Cancel
            </button>
        </div>

        <!-- Helper Text -->
        <p class="text-xs text-text-tertiary text-center mt-4">
            Not receiving the code? Make sure the bot is in the Discord server
            and you have permission to use slash commands.
        </p>
    </div>
}
```

### Validation States

#### Valid Input
- Input has correct format (3 letters - 3 numbers)
- Border: `border-success`
- No error message displayed
- Button: Enabled (primary)

#### Invalid Input
- Input does not match format
- Border: `border-error`
- Error Message: "Invalid code format. Use ABC-123"
- Color: `error`
- Icon: Error triangle (Hero Icons, 16px)
- Button: Remains enabled (allows retry)

#### Code Mismatch Error
- User enters code but server rejects it
- Error Message: "Code does not match. Please try again or request a new code."
- Input border: `border-error` with error focus shadow
- User can retry or cancel to get a new code

### Interaction States

**Countdown Timer:**
- Updates every 1 second
- When < 5 minutes: Show warning icon and text "Expiring soon"
- When < 1 minute: Change color to `error`, text "Expiring very soon"
- When 0:00 reached: Disable input and show "Code expired" message

**Copy Button:**
- Hover: Color `text-primary`
- Click: Show "Copied!" feedback (1.5 second duration)
- Assistive: `aria-label="Copy command to clipboard"`

---

## 3. Success State (Account Linked)

### Purpose
Confirm successful verification and account linkage.

### Visual Hierarchy

**Container:**
- Use existing status message pattern from LinkDiscord.cshtml
- Background: `success-bg` (#10b98120)
- Border: 1px solid `success-border` (#10b98150)
- Border Radius: 8px (`radius-lg`)
- Padding: 16px (`space-4`)
- Display: Flex with gap 12px

**Success Message:**
- Icon: Checkmark circle (Hero Icons, 20px, solid, color `success`)
- Title: "Success" (14px, weight 600, color `success`)
- Message: "Discord account linked successfully!" (14px, weight 400, color `success`)
- Icon Position: Left, flex-shrink-0

**Post-Success Actions:**
- Show connected guild information (if OAuth available)
- Auto-dismiss after 5 seconds (with countdown option)
- Provide link to dashboard or next step

### HTML Structure (Razor)

```html
@if (Model.IsVerificationComplete && Model.IsSuccess)
{
    <!-- Success Alert (Replaces pending section) -->
    <div class="mb-6 p-4 rounded-lg flex items-start gap-3 bg-success-bg border border-success-border" role="alert">
        <svg class="w-5 h-5 text-success flex-shrink-0 mt-0.5" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
        </svg>
        <div>
            <p class="text-sm font-semibold text-success">
                Success
            </p>
            <p class="text-sm text-success mt-1">
                Discord account linked successfully via bot verification!
            </p>
        </div>
    </div>

    <!-- Next Steps (Optional) -->
    <div class="bg-bg-secondary border border-border-primary rounded-lg p-6">
        <h3 class="text-base font-semibold text-text-primary mb-4">
            What's Next?
        </h3>
        <div class="space-y-3">
            @if (Model.UserGuilds.Any())
            {
                <div class="flex items-start gap-3">
                    <svg class="w-4 h-4 text-accent-blue flex-shrink-0 mt-1" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                    </svg>
                    <span class="text-sm text-text-secondary">
                        You can now manage <strong>@(Model.UserGuilds.Count) guild(s)</strong> through the admin panel
                    </span>
                </div>
            }
            <div class="flex items-start gap-3">
                <svg class="w-4 h-4 text-accent-blue flex-shrink-0 mt-1" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
                </svg>
                <span class="text-sm text-text-secondary">
                    Visit the <a href="/dashboard" class="text-accent-blue hover:text-accent-blue-hover font-medium">Dashboard</a> to explore your servers
                </span>
            </div>
        </div>
    </div>
}
```

---

## 4. Error State (Verification Failed)

### Purpose
Display errors during verification and guide recovery.

### Visual Hierarchy

**Error Alert:**
- Background: `error-bg` (#ef444420)
- Border: 1px solid `error-border` (#ef444450)
- Border Radius: 8px (`radius-lg`)
- Padding: 16px (`space-4`)
- Display: Flex with gap 12px
- Icon: Exclamation circle (Hero Icons, 20px, solid, color `error`)

**Error Messages:**
- Title: "Verification Failed" (14px, weight 600, color `error`)
- Message: Specific error description (14px, weight 400, color `error`)

**Recovery Actions:**
- "Retry" button: btn btn-primary
- "Cancel" button: btn btn-secondary
- "Request New Code" link: btn btn-accent (if applicable)

### Error Types & Messages

| Error Type | Message | Action |
|------------|---------|--------|
| Invalid Code Format | "The code format is incorrect. Use ABC-123." | Retry with correct format |
| Code Mismatch | "The code does not match. Please try again." | Retry with correct code |
| Code Expired | "Your code has expired. Request a new one to continue." | Start new verification |
| Discord User Not Found | "Could not find your Discord account. Please try again." | Retry or contact support |
| Account Already Linked | "This Discord account is already linked to another user." | Contact support |
| Server Error | "An error occurred. Please try again later." | Retry or contact support |

### HTML Structure (Razor)

```html
@if (Model.HasVerificationError && !string.IsNullOrEmpty(Model.ErrorMessage))
{
    <!-- Error Alert -->
    <div class="mb-6 p-4 rounded-lg flex items-start gap-3 bg-error-bg border border-error-border" role="alert">
        <svg class="w-5 h-5 text-error flex-shrink-0 mt-0.5" fill="currentColor" viewBox="0 0 20 20" aria-hidden="true">
            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
        </svg>
        <div>
            <p class="text-sm font-semibold text-error">
                Verification Failed
            </p>
            <p class="text-sm text-error mt-1">
                @Model.ErrorMessage
            </p>
            @if (!string.IsNullOrEmpty(Model.ErrorHint))
            {
                <p class="text-xs text-error mt-2">
                    <strong>Hint:</strong> @Model.ErrorHint
                </p>
            }
        </div>
    </div>

    <!-- Recovery Actions -->
    <div class="flex gap-3 items-center">
        <button
            type="button"
            onclick="retryVerification()"
            class="btn btn-primary">
            Retry
        </button>

        @if (Model.ErrorType == VerificationErrorType.CodeExpired)
        {
            <button
                type="button"
                onclick="startNewVerification()"
                class="btn btn-accent">
                Request New Code
            </button>
        }

        <button
            type="button"
            onclick="cancelVerification()"
            class="btn btn-secondary">
            Cancel
        </button>
    </div>

    <!-- Support Link -->
    @if (Model.IsContactSupportRequired)
    {
        <p class="text-xs text-text-tertiary mt-4">
            Still having issues?
            <a href="/support" class="text-accent-blue hover:text-accent-blue-hover font-medium">Contact support</a>
        </p>
    }
}
```

---

## State Transition Diagram

```
┌─────────────────────┐
│  Initial State      │
│ No Verification    │
└──────────┬──────────┘
           │
      [Start Verification Button]
           │
           v
┌─────────────────────┐
│ Pending State       │
│ Code Input Active  │
│ Timer Counting Down │
└──────────┬──────────┘
           │
      ┌────┴────┬──────────┬─────────────┐
      │          │          │              │
   [User        [Expires]  [Invalid Code] [Cancel]
    Submits     (>5 min)   Error Display  Button]
    Code]       Auto-      Re-entry
      │         Expire     allowed
      │          │
      v          v
┌─────────────────────┐
│ Success State       │  ┌──────────────────┐
│ Account Linked      ├──┤ Return to Initial │
│ Auto-dismiss        │  │ Show New Button   │
└─────────────────────┘  └──────────────────┘
      │
      v
[Redirect to Dashboard or Guild Page]
```

---

## Accessibility Guidelines

### WCAG 2.1 AA Compliance

#### Color Contrast
- Text on error background: **4.5:1** (exceeds AA minimum)
- Text on success background: **4.5:1** (exceeds AA minimum)
- Text on info background: **4.5:1** (exceeds AA minimum)

#### Keyboard Navigation
- All buttons: Tab-accessible, visible focus indicator (2px blue outline)
- Input field: Tab to field, enter to submit
- Tab order: Instructions > Code Input > Verify Button > Cancel Button
- Escape key: Close pending verification (optional, clear user intent)

#### Focus Management
- Initial focus: Code input field (when pending state)
- After action: Focus moved to result message or button
- Focus visible: 2px solid `accent-blue` outline with 2px offset

#### ARIA Attributes
- Alert containers: `role="alert"` for error/success messages
- Form labels: `<label for="verification-code">` linked to input
- Input validation: `aria-invalid="true"` and `aria-describedby="code-error"`
- Icon-only buttons: `aria-label="Copy command"`, etc.
- Decorative icons: `aria-hidden="true"`

#### Semantic HTML
- Form elements: `<input type="text">`, `<label>`, proper `<form>`
- Headings: Use semantic `<h3>`, `<h4>` for hierarchy
- Landmarks: Wrap in `<section>` or `<div role="region">`
- Status messages: Use `<div role="alert">` for alerts

#### Screen Reader Support
- "Enter verification code (format: ABC-123)" - input hint
- "Code expires in 15 minutes 42 seconds" - countdown update (aria-live)
- Error messages announced as alerts
- Success confirmed with role="alert" + sound/visual cue

### Implementation Checklist

- [ ] All text has 4.5:1 contrast minimum
- [ ] Focus indicators visible on all interactive elements
- [ ] Keyboard navigation tested (Tab, Shift+Tab, Enter, Escape)
- [ ] Form labels associated with inputs via `for` attribute
- [ ] Error messages linked to inputs via `aria-describedby`
- [ ] Icons have `aria-hidden="true"` if decorative
- [ ] Icon buttons have `aria-label`
- [ ] Countdown timer uses `aria-live="polite"` for updates
- [ ] Tested with screen reader (NVDA, JAWS, VoiceOver)
- [ ] No keyboard traps
- [ ] Logical tab order without tabindex manipulation

---

## Responsive Design

### Breakpoints (Mobile-First)

#### Mobile (< 768px)
- Full width input field
- Button: Full width stacked
- Font sizes: Maintained
- Padding: 16px (`space-4`) instead of 24px
- Info box: Single column layout
- Countdown: Vertical alignment

#### Tablet (768px - 1024px)
- Input max-width: 400px
- Buttons: Side-by-side if space allows
- Padding: 24px (`space-6`)
- Grid layout for guild list (if shown): 2 columns

#### Desktop (1024px+)
- Input max-width: 500px
- Buttons: Horizontal layout with proper spacing
- Padding: 24px (`space-6`)
- Guild list: 3 columns or full-width table

### CSS Media Queries

```css
/* Mobile: Default styles */
.verification-input {
  width: 100%;
}

.verification-buttons {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Tablet: 768px and up */
@media (min-width: 768px) {
  .verification-input {
    max-width: 400px;
  }
}

/* Desktop: 1024px and up */
@media (min-width: 1024px) {
  .verification-buttons {
    flex-direction: row;
  }

  .verification-input {
    max-width: 500px;
  }
}
```

---

## Interaction & Behavior

### Code Input Behavior

**Format Enforcement:**
- Accept only alphanumeric characters and hyphens
- Auto-format as user types: "ABC-123"
- Maximum length: 7 characters (3 letters + hyphen + 3 digits)
- Convert to uppercase in real-time

**Validation:**
- Real-time format validation (not submit-blocking)
- On blur: Validate against server (optional, show loading state)
- On submit: Full validation + server verification

**Example JavaScript:**
```javascript
function formatVerificationCode(input) {
  // Remove non-alphanumeric
  let cleaned = input.value.replace(/[^A-Z0-9]/gi, '').toUpperCase();

  // Add hyphen after 3 characters
  if (cleaned.length > 3) {
    cleaned = cleaned.substring(0, 3) + '-' + cleaned.substring(3, 6);
  }

  input.value = cleaned;
}

function initiateVerification() {
  // Show pending state
  document.getElementById('initial-state').style.display = 'none';
  document.getElementById('pending-state').style.display = 'block';

  // Focus code input
  document.getElementById('verification-code').focus();

  // Start countdown timer
  startCountdown();
}

function startCountdown() {
  let expiryTime = Date.now() + (15 * 60 * 1000); // 15 minutes

  const timer = setInterval(() => {
    const now = Date.now();
    const remaining = Math.max(0, expiryTime - now);

    const minutes = Math.floor(remaining / 60000);
    const seconds = Math.floor((remaining % 60000) / 1000);

    updateDisplay(`${minutes}:${seconds.toString().padStart(2, '0')}`);

    if (remaining === 0) {
      clearInterval(timer);
      showCodeExpiredError();
    }
  }, 1000);
}
```

### Countdown Timer Updates

- Updates every 1 second
- Uses `aria-live="polite"` region for screen reader announcements
- Displays minutes:seconds format (MM:SS)
- Warning threshold: < 5 minutes (change color to warning)
- Critical threshold: < 1 minute (change color to error)
- Auto-disable form submission at 0:00

### Cancel Button Behavior

**On Click:**
1. Confirm: "Cancel verification? You'll need to run the command again."
2. Hide pending state
3. Show initial state
4. Clear code input
5. Reset any error messages

---

## Component Integration Points

### Page Model Properties (LinkDiscord.cshtml.cs)

```csharp
public class LinkDiscordModel
{
    // Verification Properties
    public bool HasPendingVerification { get; set; }
    public bool IsVerificationComplete { get; set; }
    public bool IsSuccess { get; set; }

    // Code Entry
    public string VerificationCode { get; set; }
    public string VerificationError { get; set; }

    // Countdown
    public int ExpiryMinutes { get; set; }
    public int ExpirySeconds { get; set; }

    // Error Handling
    public bool HasVerificationError { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorHint { get; set; }
    public VerificationErrorType ErrorType { get; set; }

    // Support
    public bool IsContactSupportRequired { get; set; }
}

public enum VerificationErrorType
{
    InvalidFormat,
    CodeMismatch,
    CodeExpired,
    DiscordUserNotFound,
    AccountAlreadyLinked,
    ServerError
}
```

### API Endpoints

The verification backend should expose:

1. `POST /api/account/verify/initiate`
   - Response: `{ code: string, expiresAt: DateTime }`

2. `POST /api/account/verify/submit`
   - Body: `{ verificationCode: string }`
   - Response: `{ success: bool, message: string, error?: string }`

3. `GET /api/account/verify/status/:sessionId`
   - Response: `{ status: string, expirySeconds: int, verified: bool }`

---

## Visual Examples

### Example 1: Initial State Layout

```
┌─────────────────────────────────────────────────────────────┐
│                   Link Discord Account                      │
│  Link your Discord account for seamless authentication and  │
│            guild management                                 │
└─────────────────────────────────────────────────────────────┘

                        [Link Discord Account]

                              OR

┌─────────────────────────────────────────────────────────────┐
│                   Link via Discord Bot                      │
│  Use your Discord username to verify your account. Run the  │
│  /verify-account command in any Discord server where our    │
│  bot is present, and follow the on-screen instructions to   │
│  link your account.                                          │
│                                                              │
│                    [Start Verification]                     │
└─────────────────────────────────────────────────────────────┘
```

### Example 2: Pending State Layout

```
┌─────────────────────────────────────────────────────────────┐
│ ⓘ Next Step                                                 │
│ Run the verification command in Discord to get your code    │
│                                                              │
│ Run this command in any Discord server:                     │
│ ┌───────────────────────────────────────────────────────────┐
│ │ /verify-account                              [Copy]       │
│ └───────────────────────────────────────────────────────────┘
│                                                              │
│ Expires in: 14:32                                           │
│                                                              │
│ Verification Code                                           │
│ Enter the code shown in Discord (format: ABC-123)           │
│ ┌───────────────────────────────────────────────────────────┐
│ │      ABC-123      (placeholder text)                      │
│ └───────────────────────────────────────────────────────────┘
│                                                              │
│ [Verify & Link]  [Cancel]                                   │
│                                                              │
│ Not receiving the code? Make sure the bot is in the        │
│ Discord server and you have permission to use slash         │
│ commands.                                                    │
└─────────────────────────────────────────────────────────────┘
```

### Example 3: Success State

```
┌─────────────────────────────────────────────────────────────┐
│ ✓ Success                                                   │
│ Discord account linked successfully via bot verification!   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ What's Next?                                                │
│                                                              │
│ ✓ You can now manage 3 guilds through the admin panel       │
│ ✓ Visit the Dashboard to explore your servers              │
└─────────────────────────────────────────────────────────────┘
```

### Example 4: Error State (Code Expired)

```
┌─────────────────────────────────────────────────────────────┐
│ ✗ Verification Failed                                       │
│ Your code has expired. Request a new one to continue.       │
│ Hint: Codes are valid for 15 minutes.                       │
└─────────────────────────────────────────────────────────────┘

[Retry]  [Request New Code]  [Cancel]

Still having issues? Contact support
```

---

## Testing Checklist

### Functional Testing
- [ ] Initial state displays when no verification in progress
- [ ] Clicking "Start Verification" shows pending state
- [ ] Countdown timer starts and counts down correctly
- [ ] Code input accepts only alphanumeric + hyphen
- [ ] Auto-formatting applies (ABC-123 format)
- [ ] Verify button submits code to backend
- [ ] Cancel button returns to initial state
- [ ] Code expiry auto-dismisses pending state
- [ ] Success state displays on valid code
- [ ] Error messages display on invalid code
- [ ] Error recovery works (retry, request new code)

### Accessibility Testing
- [ ] Tab navigation order is correct
- [ ] Focus indicators visible on all buttons/inputs
- [ ] Screen reader announces alerts with role="alert"
- [ ] Form labels properly associated with inputs
- [ ] Error messages linked via aria-describedby
- [ ] Keyboard-only navigation works (no mouse required)
- [ ] Color contrast passes WCAG AA (4.5:1 minimum)
- [ ] Tested with NVDA, JAWS, and VoiceOver
- [ ] No keyboard traps

### Responsive Testing
- [ ] Mobile (375px): Layout stacks properly
- [ ] Tablet (768px): Layout adjusts for medium screens
- [ ] Desktop (1024px+): Full layout displays correctly
- [ ] Touch targets > 44x44px on mobile
- [ ] Text readable without horizontal scrolling
- [ ] Buttons click-able on touch devices

### Cross-Browser Testing
- [ ] Chrome/Chromium latest
- [ ] Firefox latest
- [ ] Safari latest
- [ ] Edge latest
- [ ] Mobile browsers (iOS Safari, Chrome Mobile)

### User Experience Testing
- [ ] Countdown timer is easy to see
- [ ] Code format requirement clear in UI
- [ ] Error messages helpful and actionable
- [ ] Success feedback clear and celebratory
- [ ] Loading states (if applicable) smooth
- [ ] Copy button functionality works
- [ ] Timestamps accurate

---

## Implementation Notes

### CSS Classes to Create/Use

```css
/* Existing (from design system) */
.btn.btn-primary
.btn.btn-secondary
.btn.btn-accent
.form-input
.form-label
.form-error
.text-h3
.text-text-primary
.text-text-secondary
.text-text-tertiary
.bg-bg-secondary
.bg-bg-primary
.bg-bg-tertiary
.bg-success-bg
.bg-error-bg
.bg-info-bg
.border-border-primary
.border-border-secondary
.border-success-border
.border-error-border
.border-info-border
.text-success
.text-error
.text-warning
.text-info
.shadow-sm
.shadow-md
.rounded-lg
.rounded-md

/* New utility classes (optional) */
.verification-input {
  font-family: var(--font-family-mono);
  letter-spacing: 0.125em;
  text-transform: uppercase;
}

.verification-code-display {
  font-size: 1.125rem;
  font-weight: 700;
  font-family: var(--font-family-mono);
  color: var(--color-accent-blue);
}

.countdown-warning {
  color: var(--color-warning);
}

.countdown-critical {
  color: var(--color-error);
}

.divider-text {
  display: flex;
  align-items: center;
  gap: 1rem;
  color: var(--color-text-tertiary);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.divider-text::before,
.divider-text::after {
  content: '';
  flex: 1;
  height: 1px;
  background-color: var(--color-border-primary);
}
```

### Migration Path from Existing Code

The LinkDiscord page currently has OAuth login. To add bot verification:

1. **Add conditional sections** to LinkDiscord.cshtml (if OAuth not configured OR as alternative)
2. **Add model properties** to LinkDiscord.cshtml.cs for verification state
3. **Add page handlers** for verification flow (Initiate, Submit, Cancel)
4. **Create verification service** in application layer
5. **Implement countdown logic** (server-side or JavaScript)
6. **Add CSS** for new verification components
7. **Test accessibility** and responsiveness

### File Structure

```
src/DiscordBot.Bot/
├── Pages/Account/
│   ├── LinkDiscord.cshtml          (updated with verification section)
│   └── LinkDiscord.cshtml.cs       (updated with verification logic)
├── Services/
│   └── VerificationService.cs      (new)
├── Models/
│   ├── VerificationRequest.cs      (new)
│   └── VerificationResponse.cs     (new)
└── wwwroot/css/
    └── verification.css             (new, or add to existing)
```

---

## Design System Deviations

There are no deviations from the design system. All colors, typography, spacing, and component patterns follow the established design tokens in `docs/articles/design-system.md`.

---

## Related Documentation

- **Design System:** `docs/articles/design-system.md`
- **Interactive Components:** `docs/articles/interactive-components.md`
- **API Endpoints:** `docs/articles/api-endpoints.md`
- **Issue Planning:** `docs/plans/issue-118-verification.md`
- **LinkDiscord Page:** `src/DiscordBot.Bot/Pages/Account/LinkDiscord.cshtml`

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-09 | Initial specification document created |

---

## Sign-Off

**Designed by:** Design & UI Team
**Reviewed by:** [Pending]
**Approved by:** [Pending]
**Implementation Status:** Ready for Development

For questions or clarifications, please reference the specific section above or reach out to the design team.
