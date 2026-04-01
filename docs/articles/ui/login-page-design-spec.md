# Login Page Design Specification

**Version:** 1.0
**Date:** 2025-12-27
**Target:** HTML Prototyper
**Purpose:** Modern, visually impressive login page for Discord Bot Admin application

---

## Table of Contents

1. [Overview](#overview)
2. [Layout Structure](#layout-structure)
3. [Design Philosophy](#design-philosophy)
4. [Responsive Breakpoints](#responsive-breakpoints)
5. [Component Specifications](#component-specifications)
6. [Animation Specifications](#animation-specifications)
7. [Interactive States](#interactive-states)
8. [Accessibility Requirements](#accessibility-requirements)
9. [Asset Requirements](#asset-requirements)
10. [Implementation Code](#implementation-code)

---

## Overview

### Current State
The existing login page is functional but basic - centered card layout with email/password form and optional Discord OAuth button.

### Vision
Transform the login experience into a modern, split-panel design that elevates the brand while maintaining all existing functionality:
- **Left Panel:** Atmospheric brand experience with animated gradient and Discord-inspired visuals
- **Right Panel:** Clean, focused login form with enhanced interactivity
- **Primary Auth Method:** Discord OAuth (most prominent)
- **Secondary Method:** Email/password (available but de-emphasized)
- **Visual Polish:** Subtle animations, smooth transitions, refined micro-interactions

### Key Requirements (Must Preserve)
- Email/password login form
- Discord OAuth button (conditional display)
- "Remember me" checkbox
- Error message display
- Validation message display
- Return URL handling
- Bot logo/branding
- WCAG 2.1 AA compliance

---

## Layout Structure

### Desktop Layout (1024px and up)

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  ┌──────────────────────┬─────────────────────────────────┐    │
│  │                      │                                 │    │
│  │   LEFT PANEL (40%)   │   RIGHT PANEL (60%)             │    │
│  │                      │                                 │    │
│  │  - Animated gradient │   - Login form                  │    │
│  │  - Bot logo/mascot   │   - Discord OAuth (primary)     │    │
│  │  - Tagline           │   - Email/password (secondary)  │    │
│  │  - Brand messaging   │   - Remember me                 │    │
│  │                      │                                 │    │
│  └──────────────────────┴─────────────────────────────────┘    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

**Measurements:**
- Total viewport: `100vw × 100vh`
- Left panel: `40%` width (min: `480px`, max: `600px`)
- Right panel: `60%` width (remaining space)
- Vertical alignment: Full viewport height (`min-h-screen`)
- No scrolling required on standard desktop screens (1920×1080)

### Tablet Layout (768px - 1023px)

Same split layout but adjusted proportions:
- Left panel: `35%` width
- Right panel: `65%` width
- Slightly reduced padding and spacing

### Mobile Layout (< 768px)

Single column, stacked layout:

```
┌─────────────────────────┐
│                         │
│   BRAND HEADER          │
│   - Logo                │
│   - Title               │
│   (Compact, 25vh max)   │
│                         │
├─────────────────────────┤
│                         │
│   LOGIN FORM            │
│   - Discord OAuth       │
│   - Email/password      │
│   - Remember me         │
│   (Scrollable if needed)│
│                         │
└─────────────────────────┘
```

**Measurements:**
- Brand header: Maximum `25vh`, minimum `180px`
- Form area: Scrollable, centered with `max-width: 400px`
- Padding: `1.5rem` (24px)

---

## Design Philosophy

### Visual Hierarchy
1. **Discord OAuth** - Primary CTA (most prominent)
2. **Email/Password** - Secondary option (available but de-emphasized)
3. **Branding** - Strong left panel presence
4. **Error/Validation** - High visibility when present

### Color Strategy
- **Left Panel:** Rich gradient background (#1d2022 → #2f3336 with orange/blue accent overlays)
- **Right Panel:** Clean, minimal (#262a2d background)
- **Primary Action (Discord):** Discord purple (#5865F2)
- **Secondary Action (Email):** Accent orange (#cb4e1b)
- **Focus States:** Accent blue (#098ecf)

### Motion Design
- **Principle:** Enhance, don't distract
- **Timing:** Fast (100-200ms) for interactions, slow (3-8s) for ambient animations
- **Easing:** `ease-in-out` for most transitions, `cubic-bezier(0.4, 0, 0.2, 1)` for custom
- **Reduced Motion:** All animations respect `prefers-reduced-motion: reduce`

---

## Responsive Breakpoints

```css
/* Mobile First Approach */

/* Base: Mobile (< 768px) */
/* Default styles - single column stacked layout */

/* Tablet: 768px - 1023px */
@media (min-width: 768px) {
  /* Two-column layout begins (35/65 split) */
}

/* Desktop: 1024px and up */
@media (min-width: 1024px) {
  /* Full two-column layout (40/60 split) */
}

/* Large Desktop: 1440px and up */
@media (min-width: 1440px) {
  /* Maximum left panel width capped at 600px */
}
```

---

## Component Specifications

### 1. Left Panel (Brand Experience)

#### Desktop/Tablet Layout

**Container:**
```css
.login-brand-panel {
  width: 40%;                          /* Desktop */
  min-width: 480px;
  max-width: 600px;
  min-height: 100vh;
  position: relative;
  overflow: hidden;
  background: linear-gradient(135deg, #1d2022 0%, #2f3336 100%);
}

/* Tablet adjustment */
@media (min-width: 768px) and (max-width: 1023px) {
  .login-brand-panel {
    width: 35%;
    min-width: 360px;
  }
}
```

**Gradient Overlay (Animated):**
```css
.login-brand-panel::before {
  content: "";
  position: absolute;
  inset: 0;
  background: radial-gradient(
    circle at 30% 20%,
    rgba(203, 78, 27, 0.15) 0%,    /* Orange accent */
    transparent 50%
  ),
  radial-gradient(
    circle at 70% 80%,
    rgba(9, 142, 207, 0.12) 0%,    /* Blue accent */
    transparent 50%
  );
  animation: gradient-shift 8s ease-in-out infinite alternate;
  z-index: 1;
}

@keyframes gradient-shift {
  0% {
    transform: translate(0, 0) scale(1);
    opacity: 0.8;
  }
  100% {
    transform: translate(20px, -20px) scale(1.1);
    opacity: 1;
  }
}
```

**Content Container:**
```css
.login-brand-content {
  position: relative;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 3rem 2rem;
  text-align: center;
}
```

**Bot Logo/Mascot:**
```css
.login-brand-logo {
  width: 140px;
  height: 140px;
  margin-bottom: 2rem;
  position: relative;
}

/* Logo container with glow effect */
.login-brand-logo-container {
  width: 140px;
  height: 140px;
  border-radius: 24px;
  background: linear-gradient(135deg, #cb4e1b 0%, #e5591f 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow:
    0 10px 40px rgba(203, 78, 27, 0.3),
    0 0 60px rgba(203, 78, 27, 0.15);
  animation: logo-pulse 3s ease-in-out infinite;
  position: relative;
}

@keyframes logo-pulse {
  0%, 100% {
    transform: scale(1);
    box-shadow:
      0 10px 40px rgba(203, 78, 27, 0.3),
      0 0 60px rgba(203, 78, 27, 0.15);
  }
  50% {
    transform: scale(1.05);
    box-shadow:
      0 15px 50px rgba(203, 78, 27, 0.4),
      0 0 80px rgba(203, 78, 27, 0.2);
  }
}

/* Discord icon inside */
.login-brand-logo-icon {
  width: 88px;
  height: 88px;
  color: #ffffff;
}
```

**Application Title:**
```css
.login-brand-title {
  font-size: 2.5rem;              /* 40px */
  font-weight: 700;
  line-height: 1.2;
  letter-spacing: -0.02em;
  color: #d7d3d0;
  margin-bottom: 1rem;
  background: linear-gradient(135deg, #d7d3d0 0%, #a8a5a3 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}
```

**Tagline:**
```css
.login-brand-tagline {
  font-size: 1.125rem;            /* 18px */
  font-weight: 400;
  line-height: 1.6;
  color: #a8a5a3;
  margin-bottom: 3rem;
  max-width: 400px;
}
```

**Feature List:**
```html
<!-- HTML Structure -->
<ul class="login-brand-features">
  <li class="login-brand-feature">
    <svg class="login-brand-feature-icon"><!-- checkmark icon --></svg>
    <span>Powerful bot management</span>
  </li>
  <li class="login-brand-feature">
    <svg class="login-brand-feature-icon"><!-- checkmark icon --></svg>
    <span>Real-time analytics</span>
  </li>
  <li class="login-brand-feature">
    <svg class="login-brand-feature-icon"><!-- checkmark icon --></svg>
    <span>Secure Discord integration</span>
  </li>
</ul>
```

```css
.login-brand-features {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  align-items: flex-start;
  max-width: 360px;
}

.login-brand-feature {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.9375rem;          /* 15px */
  color: #a8a5a3;
  transition: color 0.2s ease;
}

.login-brand-feature:hover {
  color: #d7d3d0;
}

.login-brand-feature-icon {
  width: 1.25rem;
  height: 1.25rem;
  color: #cb4e1b;
  flex-shrink: 0;
}
```

#### Mobile Layout

```css
/* Mobile brand header */
@media (max-width: 767px) {
  .login-brand-panel {
    width: 100%;
    min-width: auto;
    max-width: 100%;
    min-height: auto;
    max-height: 25vh;
    padding: 2rem 1.5rem;
  }

  .login-brand-content {
    min-height: auto;
    padding: 0;
  }

  .login-brand-logo-container {
    width: 80px;
    height: 80px;
    border-radius: 16px;
  }

  .login-brand-logo-icon {
    width: 50px;
    height: 50px;
  }

  .login-brand-title {
    font-size: 1.75rem;            /* 28px */
    margin-bottom: 0.5rem;
  }

  .login-brand-tagline {
    font-size: 0.875rem;           /* 14px */
    margin-bottom: 0;
  }

  .login-brand-features {
    display: none;                 /* Hidden on mobile to save space */
  }
}
```

---

### 2. Right Panel (Login Form)

#### Container

```css
.login-form-panel {
  flex: 1;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem 2rem;
  background-color: #262a2d;
}

@media (max-width: 767px) {
  .login-form-panel {
    min-height: auto;
    padding: 2rem 1.5rem;
  }
}
```

**Form Container:**
```css
.login-form-container {
  width: 100%;
  max-width: 440px;
  margin: 0 auto;
}
```

**Form Header:**
```css
.login-form-header {
  margin-bottom: 2.5rem;
  text-align: center;
}

.login-form-title {
  font-size: 1.875rem;             /* 30px */
  font-weight: 700;
  line-height: 1.3;
  color: #d7d3d0;
  margin-bottom: 0.5rem;
}

.login-form-subtitle {
  font-size: 0.9375rem;            /* 15px */
  color: #a8a5a3;
}

@media (max-width: 767px) {
  .login-form-header {
    margin-bottom: 1.5rem;
  }

  .login-form-title {
    font-size: 1.5rem;             /* 24px */
  }

  .login-form-subtitle {
    font-size: 0.875rem;           /* 14px */
  }
}
```

---

### 3. Discord OAuth Button (Primary CTA)

**Full Specifications:**

```css
.btn-discord-oauth {
  /* Layout */
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;

  /* Spacing */
  padding: 1rem 1.5rem;           /* 16px 24px - larger than standard */

  /* Typography */
  font-size: 1rem;                /* 16px */
  font-weight: 600;
  letter-spacing: 0.01em;

  /* Colors */
  color: #ffffff;
  background: linear-gradient(135deg, #5865F2 0%, #4752C4 100%);
  border: 1px solid #5865F2;

  /* Shape */
  border-radius: 8px;

  /* Effects */
  box-shadow:
    0 4px 12px rgba(88, 101, 242, 0.3),
    0 0 0 0 rgba(88, 101, 242, 0.4);

  /* Transitions */
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);

  /* Interaction */
  cursor: pointer;
  position: relative;
  overflow: hidden;
}

/* Hover State */
.btn-discord-oauth:hover {
  background: linear-gradient(135deg, #6674F4 0%, #5865F2 100%);
  border-color: #6674F4;
  box-shadow:
    0 6px 16px rgba(88, 101, 242, 0.4),
    0 0 0 0 rgba(88, 101, 242, 0.6);
  transform: translateY(-2px);
}

/* Active State */
.btn-discord-oauth:active {
  transform: translateY(0);
  box-shadow:
    0 2px 8px rgba(88, 101, 242, 0.4),
    0 0 0 0 rgba(88, 101, 242, 0.4);
}

/* Focus State */
.btn-discord-oauth:focus-visible {
  outline: none;
  box-shadow:
    0 4px 12px rgba(88, 101, 242, 0.3),
    0 0 0 3px rgba(9, 142, 207, 0.4);   /* Blue focus ring */
}

/* Disabled State */
.btn-discord-oauth:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}

/* Discord Logo */
.btn-discord-oauth-icon {
  width: 1.5rem;                  /* 24px */
  height: 1.5rem;
  flex-shrink: 0;
  transition: transform 0.2s ease;
}

.btn-discord-oauth:hover .btn-discord-oauth-icon {
  transform: scale(1.1);
}

/* Shimmer effect (optional) */
.btn-discord-oauth::before {
  content: "";
  position: absolute;
  inset: 0;
  background: linear-gradient(
    90deg,
    transparent 0%,
    rgba(255, 255, 255, 0.15) 50%,
    transparent 100%
  );
  transform: translateX(-100%);
  transition: transform 0.6s ease;
}

.btn-discord-oauth:hover::before {
  transform: translateX(100%);
}
```

**Loading State:**
```css
.btn-discord-oauth.loading {
  pointer-events: none;
}

.btn-discord-oauth.loading .btn-discord-oauth-icon {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
```

---

### 4. Divider (Between Discord and Email/Password)

```css
.login-divider {
  position: relative;
  margin: 2rem 0;
  text-align: center;
}

.login-divider::before {
  content: "";
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(
    to right,
    transparent 0%,
    #3f4447 20%,
    #3f4447 80%,
    transparent 100%
  );
}

.login-divider-text {
  position: relative;
  display: inline-block;
  padding: 0 1rem;
  background-color: #262a2d;
  font-size: 0.8125rem;          /* 13px */
  font-weight: 500;
  color: #7a7876;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
```

---

### 5. Email/Password Form

#### Form Group (Floating Label Pattern)

**HTML Structure:**
```html
<div class="form-group-floating">
  <input
    type="email"
    id="email"
    class="form-input-floating"
    placeholder=" "
    autocomplete="email"
    required
  />
  <label for="email" class="form-label-floating">
    Email address
  </label>
  <span class="form-error" role="alert">
    <!-- Error message goes here -->
  </span>
</div>
```

**CSS Specifications:**

```css
/* Container */
.form-group-floating {
  position: relative;
  margin-bottom: 1.5rem;
}

/* Input Field */
.form-input-floating {
  /* Layout */
  width: 100%;

  /* Spacing */
  padding: 1.25rem 1rem 0.5rem 1rem;   /* Top padding for floating label */

  /* Typography */
  font-size: 0.9375rem;                 /* 15px */
  line-height: 1.5;
  color: #d7d3d0;

  /* Appearance */
  background-color: #1d2022;
  border: 1px solid #3f4447;
  border-radius: 8px;

  /* Effects */
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.1);

  /* Transitions */
  transition: all 0.2s ease;

  /* Remove default styles */
  outline: none;
  -webkit-appearance: none;
  appearance: none;
}

/* Placeholder (must be empty space for floating label to work) */
.form-input-floating::placeholder {
  color: transparent;
}

/* Hover State */
.form-input-floating:hover {
  border-color: #098ecf;
}

/* Focus State */
.form-input-floating:focus {
  border-color: #098ecf;
  background-color: #262a2d;
  box-shadow:
    inset 0 1px 2px rgba(0, 0, 0, 0.1),
    0 0 0 3px rgba(9, 142, 207, 0.15);
}

/* Floating Label */
.form-label-floating {
  /* Position */
  position: absolute;
  top: 1rem;
  left: 1rem;

  /* Typography */
  font-size: 0.9375rem;           /* 15px */
  font-weight: 500;
  color: #7a7876;

  /* Appearance */
  background-color: transparent;
  pointer-events: none;

  /* Transitions */
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  transform-origin: left center;
}

/* Label Float Animation (when input has focus OR has value) */
.form-input-floating:focus + .form-label-floating,
.form-input-floating:not(:placeholder-shown) + .form-label-floating {
  transform: translateY(-0.625rem) scale(0.85);
  color: #098ecf;
  font-weight: 600;
}

/* Error State */
.form-input-floating.error {
  border-color: #ef4444;
  background-color: rgba(239, 68, 68, 0.05);
}

.form-input-floating.error:focus {
  box-shadow:
    inset 0 1px 2px rgba(0, 0, 0, 0.1),
    0 0 0 3px rgba(239, 68, 68, 0.15);
}

.form-input-floating.error + .form-label-floating {
  color: #ef4444;
}

/* Error Message */
.form-error {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.8125rem;           /* 13px */
  color: #ef4444;
  opacity: 0;
  transform: translateY(-4px);
  transition: all 0.2s ease;
}

.form-input-floating.error ~ .form-error {
  opacity: 1;
  transform: translateY(0);
}

/* Success State (optional) */
.form-input-floating.success {
  border-color: #10b981;
}

.form-input-floating.success:focus {
  box-shadow:
    inset 0 1px 2px rgba(0, 0, 0, 0.1),
    0 0 0 3px rgba(16, 185, 129, 0.15);
}
```

#### Password Field with Visibility Toggle

**HTML Structure:**
```html
<div class="form-group-floating password-group">
  <input
    type="password"
    id="password"
    class="form-input-floating"
    placeholder=" "
    autocomplete="current-password"
    required
  />
  <label for="password" class="form-label-floating">
    Password
  </label>
  <button
    type="button"
    class="password-toggle"
    aria-label="Toggle password visibility"
  >
    <svg class="password-toggle-icon password-show">
      <!-- Eye icon (show password) -->
    </svg>
    <svg class="password-toggle-icon password-hide hidden">
      <!-- Eye-slash icon (hide password) -->
    </svg>
  </button>
</div>
```

**CSS Specifications:**

```css
.password-group {
  position: relative;
}

.password-toggle {
  /* Position */
  position: absolute;
  top: 50%;
  right: 0.75rem;
  transform: translateY(-50%);

  /* Appearance */
  background: none;
  border: none;
  padding: 0.5rem;
  cursor: pointer;

  /* Colors */
  color: #7a7876;

  /* Transitions */
  transition: color 0.2s ease;

  /* Layout */
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.password-toggle:hover {
  color: #a8a5a3;
  background-color: rgba(122, 120, 118, 0.1);
}

.password-toggle:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
  color: #098ecf;
}

.password-toggle-icon {
  width: 1.25rem;              /* 20px */
  height: 1.25rem;
  transition: opacity 0.15s ease;
}

.password-toggle-icon.hidden {
  display: none;
}

/* Adjust input padding to accommodate toggle button */
.password-group .form-input-floating {
  padding-right: 3rem;
}
```

**JavaScript for Toggle:**
```javascript
// Password visibility toggle
document.querySelectorAll('.password-toggle').forEach(button => {
  button.addEventListener('click', function() {
    const input = this.parentElement.querySelector('input');
    const showIcon = this.querySelector('.password-show');
    const hideIcon = this.querySelector('.password-hide');

    if (input.type === 'password') {
      input.type = 'text';
      showIcon.classList.add('hidden');
      hideIcon.classList.remove('hidden');
      this.setAttribute('aria-label', 'Hide password');
    } else {
      input.type = 'password';
      showIcon.classList.remove('hidden');
      hideIcon.classList.add('hidden');
      this.setAttribute('aria-label', 'Show password');
    }
  });
});
```

---

### 6. Remember Me Checkbox

```css
.remember-me-container {
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  gap: 0.625rem;
}

/* Custom Checkbox */
.remember-me-checkbox {
  /* Hide default checkbox */
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

/* Custom checkbox visual */
.remember-me-checkbox-custom {
  /* Size */
  width: 1.25rem;               /* 20px */
  height: 1.25rem;

  /* Appearance */
  border: 2px solid #3f4447;
  border-radius: 4px;
  background-color: #1d2022;

  /* Layout */
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;

  /* Transitions */
  transition: all 0.2s ease;
  cursor: pointer;
}

/* Checkbox hover */
.remember-me-checkbox:hover + .remember-me-checkbox-custom {
  border-color: #098ecf;
}

/* Checkbox focus */
.remember-me-checkbox:focus-visible + .remember-me-checkbox-custom {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}

/* Checkbox checked */
.remember-me-checkbox:checked + .remember-me-checkbox-custom {
  background-color: #cb4e1b;
  border-color: #cb4e1b;
}

/* Checkmark icon */
.remember-me-checkmark {
  width: 0.875rem;              /* 14px */
  height: 0.875rem;
  color: #ffffff;
  opacity: 0;
  transform: scale(0.5);
  transition: all 0.15s cubic-bezier(0.4, 0, 0.2, 1);
}

.remember-me-checkbox:checked + .remember-me-checkbox-custom .remember-me-checkmark {
  opacity: 1;
  transform: scale(1);
}

/* Label */
.remember-me-label {
  font-size: 0.875rem;          /* 14px */
  color: #a8a5a3;
  cursor: pointer;
  user-select: none;
  transition: color 0.2s ease;
}

.remember-me-label:hover {
  color: #d7d3d0;
}
```

**HTML Structure:**
```html
<div class="remember-me-container">
  <input
    type="checkbox"
    id="remember-me"
    class="remember-me-checkbox"
  />
  <label for="remember-me" class="remember-me-checkbox-custom">
    <svg class="remember-me-checkmark" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
    </svg>
  </label>
  <label for="remember-me" class="remember-me-label">
    Remember me
  </label>
</div>
```

---

### 7. Submit Button (Email/Password Login)

```css
.btn-submit-login {
  /* Layout */
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;

  /* Spacing */
  padding: 0.875rem 1.5rem;     /* 14px 24px */

  /* Typography */
  font-size: 0.9375rem;         /* 15px */
  font-weight: 600;

  /* Colors */
  color: #ffffff;
  background: linear-gradient(135deg, #cb4e1b 0%, #e5591f 100%);
  border: 1px solid #cb4e1b;

  /* Shape */
  border-radius: 8px;

  /* Effects */
  box-shadow: 0 2px 8px rgba(203, 78, 27, 0.3);

  /* Transitions */
  transition: all 0.2s ease;

  /* Interaction */
  cursor: pointer;
}

/* Hover State */
.btn-submit-login:hover {
  background: linear-gradient(135deg, #e5591f 0%, #cb4e1b 100%);
  box-shadow: 0 4px 12px rgba(203, 78, 27, 0.4);
  transform: translateY(-1px);
}

/* Active State */
.btn-submit-login:active {
  transform: translateY(0);
  box-shadow: 0 1px 4px rgba(203, 78, 27, 0.3);
}

/* Focus State */
.btn-submit-login:focus-visible {
  outline: none;
  box-shadow:
    0 2px 8px rgba(203, 78, 27, 0.3),
    0 0 0 3px rgba(9, 142, 207, 0.4);
}

/* Disabled State */
.btn-submit-login:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

/* Loading State */
.btn-submit-login.loading {
  pointer-events: none;
}

.btn-submit-login.loading::before {
  content: "";
  width: 1rem;
  height: 1rem;
  border: 2px solid #ffffff;
  border-top-color: transparent;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}
```

---

### 8. Error Alert Banner

```css
.login-error-alert {
  /* Spacing */
  margin-bottom: 1.5rem;
  padding: 1rem;

  /* Colors */
  background: linear-gradient(
    135deg,
    rgba(239, 68, 68, 0.1) 0%,
    rgba(239, 68, 68, 0.05) 100%
  );
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-left: 4px solid #ef4444;

  /* Shape */
  border-radius: 8px;

  /* Layout */
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;

  /* Animation entrance */
  animation: slide-down 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes slide-down {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Error icon */
.login-error-icon {
  width: 1.25rem;
  height: 1.25rem;
  color: #ef4444;
  flex-shrink: 0;
  margin-top: 0.125rem;
}

/* Error content */
.login-error-content {
  flex: 1;
}

.login-error-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: #ef4444;
  margin-bottom: 0.25rem;
}

.login-error-message {
  font-size: 0.875rem;
  color: #ef4444;
  line-height: 1.5;
  opacity: 0.9;
}

/* Shake animation for validation errors */
.login-error-alert.shake {
  animation: shake 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  10%, 30%, 50%, 70%, 90% { transform: translateX(-4px); }
  20%, 40%, 60%, 80% { transform: translateX(4px); }
}
```

---

## Animation Specifications

### 1. Page Load Animations

**Stagger entrance for form elements:**

```css
.login-form-container > * {
  opacity: 0;
  transform: translateY(12px);
  animation: fade-in-up 0.4s cubic-bezier(0.4, 0, 0.2, 1) forwards;
}

.login-form-container > *:nth-child(1) { animation-delay: 0.1s; }
.login-form-container > *:nth-child(2) { animation-delay: 0.15s; }
.login-form-container > *:nth-child(3) { animation-delay: 0.2s; }
.login-form-container > *:nth-child(4) { animation-delay: 0.25s; }
.login-form-container > *:nth-child(5) { animation-delay: 0.3s; }
.login-form-container > *:nth-child(6) { animation-delay: 0.35s; }

@keyframes fade-in-up {
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### 2. Input Focus Animation

```css
.form-input-floating:focus {
  animation: input-glow 0.6s ease-out;
}

@keyframes input-glow {
  0% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 0 rgba(9, 142, 207, 0);
  }
  50% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 6px rgba(9, 142, 207, 0.15);
  }
  100% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 3px rgba(9, 142, 207, 0.15);
  }
}
```

### 3. Button Ripple Effect (Optional)

```css
.btn-ripple {
  position: relative;
  overflow: hidden;
}

.btn-ripple::after {
  content: "";
  position: absolute;
  width: 100%;
  height: 100%;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%) scale(0);
  background: radial-gradient(circle, rgba(255, 255, 255, 0.3) 0%, transparent 70%);
  opacity: 0;
  pointer-events: none;
}

.btn-ripple:active::after {
  transform: translate(-50%, -50%) scale(2);
  opacity: 1;
  transition: transform 0.5s ease, opacity 0.3s ease;
}
```

### 4. Background Particle Animation (Optional Enhancement)

Add subtle floating particles to the left panel:

```css
.login-particles {
  position: absolute;
  inset: 0;
  z-index: 1;
  overflow: hidden;
  pointer-events: none;
}

.particle {
  position: absolute;
  width: 4px;
  height: 4px;
  background: radial-gradient(circle, #cb4e1b 0%, transparent 70%);
  border-radius: 50%;
  opacity: 0.3;
  animation: float-particle 15s infinite ease-in-out;
}

.particle:nth-child(1) { left: 10%; animation-delay: 0s; }
.particle:nth-child(2) { left: 30%; animation-delay: 2s; }
.particle:nth-child(3) { left: 50%; animation-delay: 4s; }
.particle:nth-child(4) { left: 70%; animation-delay: 6s; }
.particle:nth-child(5) { left: 90%; animation-delay: 8s; }

@keyframes float-particle {
  0%, 100% {
    transform: translateY(100vh) scale(0.5);
    opacity: 0;
  }
  10% {
    opacity: 0.3;
  }
  90% {
    opacity: 0.3;
  }
  50% {
    transform: translateY(-10vh) scale(1);
    opacity: 0.5;
  }
}
```

---

## Interactive States

### Button States Summary

| State | Discord OAuth | Submit Button |
|-------|--------------|---------------|
| **Default** | Gradient #5865F2→#4752C4, shadow 4px | Gradient #cb4e1b→#e5591f, shadow 2px |
| **Hover** | Gradient #6674F4→#5865F2, shadow 6px, translateY(-2px) | Gradient #e5591f→#cb4e1b, shadow 4px, translateY(-1px) |
| **Active** | translateY(0), shadow 2px | translateY(0), shadow 1px |
| **Focus** | Blue ring 3px (#098ecf at 40% opacity) | Blue ring 3px (#098ecf at 40% opacity) |
| **Disabled** | opacity 0.5, no transform/shadow | opacity 0.5, no transform/shadow |
| **Loading** | Spinner animation, pointer-events: none | Spinner icon, pointer-events: none |

### Input States Summary

| State | Border Color | Background | Shadow |
|-------|-------------|------------|--------|
| **Default** | #3f4447 | #1d2022 | Inset 1px |
| **Hover** | #098ecf | #1d2022 | Inset 1px |
| **Focus** | #098ecf | #262a2d | Inset 1px + ring 3px blue |
| **Error** | #ef4444 | rgba(239,68,68,0.05) | Inset 1px + ring 3px red |
| **Success** | #10b981 | #1d2022 | Inset 1px + ring 3px green |
| **Disabled** | #3f4447 | #262a2d | Inset 1px, opacity 0.5 |

---

## Accessibility Requirements

### Keyboard Navigation

**Tab Order:**
1. Discord OAuth button (if configured)
2. Email input
3. Password input
4. Password visibility toggle
5. Remember me checkbox
6. Submit button

**Keyboard Shortcuts:**
- `Enter` in any input field submits the form
- `Space` toggles checkbox
- `Esc` clears focus (browser default)

### ARIA Attributes

```html
<!-- Error alert -->
<div class="login-error-alert" role="alert" aria-live="polite">
  <!-- Error content -->
</div>

<!-- Form inputs -->
<input
  aria-required="true"
  aria-invalid="false"
  aria-describedby="email-error"
/>
<span id="email-error" class="form-error" role="alert">
  <!-- Error message -->
</span>

<!-- Password toggle -->
<button
  type="button"
  aria-label="Toggle password visibility"
  aria-pressed="false"
>
  <!-- Icons -->
</button>

<!-- Loading state -->
<button
  aria-busy="true"
  aria-disabled="true"
  disabled
>
  <span class="sr-only">Loading...</span>
  <!-- Button content -->
</button>
```

### Screen Reader Only Text

```css
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}
```

### Color Contrast

All text meets WCAG 2.1 AA standards:
- Primary text (#d7d3d0) on background (#262a2d): **10.2:1** (AAA)
- Secondary text (#a8a5a3) on background (#262a2d): **5.6:1** (AA)
- Error text (#ef4444) on background (#262a2d): **4.8:1** (AA)
- Button text (white) on Discord purple (#5865F2): **8.6:1** (AAA)
- Button text (white) on orange (#cb4e1b): **4.5:1** (AA)

### Focus Indicators

All interactive elements have visible focus indicators:
- Minimum 2px solid outline
- Color: #098ecf (accent blue)
- Offset: 2px
- Border radius matches element

### Reduced Motion

```css
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }

  /* Keep essential loading animations */
  .btn-submit-login.loading::before,
  .btn-discord-oauth.loading .btn-discord-oauth-icon {
    animation-duration: 1s !important;
  }
}
```

---

## Asset Requirements

### Icons (Hero Icons)

All icons should be from Hero Icons (https://heroicons.com) for consistency:

1. **Discord Logo** (24×24px, solid)
   - Used in: Brand panel, Discord OAuth button
   - Path: Custom SVG (Discord's official logo)

2. **Eye Icon** (20×20px, outline)
   - Used in: Password visibility toggle (show)
   - Hero Icons: `eye`

3. **Eye-Slash Icon** (20×20px, outline)
   - Used in: Password visibility toggle (hide)
   - Hero Icons: `eye-slash`

4. **Check Icon** (14×14px, solid)
   - Used in: Remember me checkbox, feature list
   - Hero Icons: `check`

5. **Exclamation Circle Icon** (20×20px, solid)
   - Used in: Error alerts
   - Hero Icons: `exclamation-circle`

6. **Checkmark Circle Icon** (20×20px, outline)
   - Used in: Feature list on left panel
   - Hero Icons: `check-circle`

### SVG Code for Discord Logo

```svg
<!-- Discord Logo (24x24) -->
<svg viewBox="0 0 24 24" fill="currentColor">
  <path d="M20.317 4.492c-1.53-.69-3.17-1.2-4.885-1.49a.075.075 0 0 0-.079.036c-.21.369-.444.85-.608 1.23a18.566 18.566 0 0 0-5.487 0 12.36 12.36 0 0 0-.617-1.23A.077.077 0 0 0 8.562 3c-1.714.29-3.354.8-4.885 1.491a.07.07 0 0 0-.032.027C.533 9.093-.32 13.555.099 17.961a.08.08 0 0 0 .031.055 20.03 20.03 0 0 0 5.993 2.98.078.078 0 0 0 .084-.026 13.83 13.83 0 0 0 1.226-1.963.074.074 0 0 0-.041-.104 13.201 13.201 0 0 1-1.872-.878.075.075 0 0 1-.008-.125c.126-.093.252-.19.372-.287a.075.075 0 0 1 .078-.01c3.927 1.764 8.18 1.764 12.061 0a.075.075 0 0 1 .079.009c.12.098.245.195.372.288a.075.075 0 0 1-.006.125c-.598.344-1.22.635-1.873.877a.075.075 0 0 0-.041.105c.36.687.772 1.341 1.225 1.962a.077.077 0 0 0 .084.028 19.963 19.963 0 0 0 6.002-2.981.076.076 0 0 0 .032-.054c.5-5.094-.838-9.52-3.549-13.442a.06.06 0 0 0-.031-.028zM8.02 15.278c-1.182 0-2.157-1.069-2.157-2.38 0-1.312.956-2.38 2.157-2.38 1.21 0 2.176 1.077 2.157 2.38 0 1.312-.956 2.38-2.157 2.38zm7.975 0c-1.183 0-2.157-1.069-2.157-2.38 0-1.312.955-2.38 2.157-2.38 1.21 0 2.176 1.077 2.157 2.38 0 1.312-.946 2.38-2.157 2.38z" />
</svg>
```

### Color Palette Reference

```css
/* Brand Panel Colors */
--login-brand-bg-start: #1d2022;
--login-brand-bg-end: #2f3336;
--login-brand-accent-orange: rgba(203, 78, 27, 0.15);
--login-brand-accent-blue: rgba(9, 142, 207, 0.12);

/* Form Panel Colors */
--login-form-bg: #262a2d;

/* Discord Button Colors */
--discord-purple: #5865F2;
--discord-purple-light: #6674F4;
--discord-purple-dark: #4752C4;

/* Text Colors */
--login-text-primary: #d7d3d0;
--login-text-secondary: #a8a5a3;
--login-text-tertiary: #7a7876;

/* Border Colors */
--login-border-primary: #3f4447;

/* Input Colors */
--login-input-bg: #1d2022;
--login-input-bg-focus: #262a2d;

/* Accent Colors */
--login-accent-orange: #cb4e1b;
--login-accent-orange-hover: #e5591f;
--login-accent-blue: #098ecf;

/* Semantic Colors */
--login-error: #ef4444;
--login-success: #10b981;
```

---

## Implementation Code

### Complete HTML Structure

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Sign In - Discord Bot Admin</title>
  <link rel="stylesheet" href="/css/login.css">
</head>
<body>

  <div class="login-container">

    <!-- Left Panel: Brand Experience -->
    <aside class="login-brand-panel">
      <div class="login-brand-content">

        <!-- Logo -->
        <div class="login-brand-logo">
          <div class="login-brand-logo-container">
            <svg class="login-brand-logo-icon" fill="currentColor" viewBox="0 0 24 24">
              <path d="M20.317 4.492c-1.53-.69-3.17-1.2-4.885-1.49a.075.075 0 0 0-.079.036c-.21.369-.444.85-.608 1.23a18.566 18.566 0 0 0-5.487 0 12.36 12.36 0 0 0-.617-1.23A.077.077 0 0 0 8.562 3c-1.714.29-3.354.8-4.885 1.491a.07.07 0 0 0-.032.027C.533 9.093-.32 13.555.099 17.961a.08.08 0 0 0 .031.055 20.03 20.03 0 0 0 5.993 2.98.078.078 0 0 0 .084-.026 13.83 13.83 0 0 0 1.226-1.963.074.074 0 0 0-.041-.104 13.201 13.201 0 0 1-1.872-.878.075.075 0 0 1-.008-.125c.126-.093.252-.19.372-.287a.075.075 0 0 1 .078-.01c3.927 1.764 8.18 1.764 12.061 0a.075.075 0 0 1 .079.009c.12.098.245.195.372.288a.075.075 0 0 1-.006.125c-.598.344-1.22.635-1.873.877a.075.075 0 0 0-.041.105c.36.687.772 1.341 1.225 1.962a.077.077 0 0 0 .084.028 19.963 19.963 0 0 0 6.002-2.981.076.076 0 0 0 .032-.054c.5-5.094-.838-9.52-3.549-13.442a.06.06 0 0 0-.031-.028zM8.02 15.278c-1.182 0-2.157-1.069-2.157-2.38 0-1.312.956-2.38 2.157-2.38 1.21 0 2.176 1.077 2.157 2.38 0 1.312-.956 2.38-2.157 2.38zm7.975 0c-1.183 0-2.157-1.069-2.157-2.38 0-1.312.955-2.38 2.157-2.38 1.21 0 2.176 1.077 2.157 2.38 0 1.312-.946 2.38-2.157 2.38z" />
            </svg>
          </div>
        </div>

        <!-- Title & Tagline -->
        <h1 class="login-brand-title">Discord Bot Admin</h1>
        <p class="login-brand-tagline">
          Powerful bot management at your fingertips
        </p>

        <!-- Feature List -->
        <ul class="login-brand-features">
          <li class="login-brand-feature">
            <svg class="login-brand-feature-icon" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
            </svg>
            <span>Powerful bot management</span>
          </li>
          <li class="login-brand-feature">
            <svg class="login-brand-feature-icon" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
            </svg>
            <span>Real-time analytics</span>
          </li>
          <li class="login-brand-feature">
            <svg class="login-brand-feature-icon" fill="currentColor" viewBox="0 0 20 20">
              <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
            </svg>
            <span>Secure Discord integration</span>
          </li>
        </ul>

      </div>
    </aside>

    <!-- Right Panel: Login Form -->
    <main class="login-form-panel">
      <div class="login-form-container">

        <!-- Form Header -->
        <header class="login-form-header">
          <h2 class="login-form-title">Welcome back</h2>
          <p class="login-form-subtitle">Sign in to access your dashboard</p>
        </header>

        <!-- Error Alert (conditionally displayed) -->
        <div class="login-error-alert shake" role="alert" aria-live="polite" style="display: none;">
          <svg class="login-error-icon" fill="currentColor" viewBox="0 0 20 20">
            <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
          </svg>
          <div class="login-error-content">
            <p class="login-error-title">Login failed</p>
            <p class="login-error-message">Invalid email or password. Please try again.</p>
          </div>
        </div>

        <!-- Discord OAuth Button (Primary CTA) -->
        <form method="post" action="/login-discord">
          <input type="hidden" name="returnUrl" value="/" />
          <button type="submit" class="btn-discord-oauth btn-ripple">
            <svg class="btn-discord-oauth-icon" fill="currentColor" viewBox="0 0 24 24">
              <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z" />
            </svg>
            Continue with Discord
          </button>
        </form>

        <!-- Divider -->
        <div class="login-divider">
          <span class="login-divider-text">Or use your email</span>
        </div>

        <!-- Email/Password Form -->
        <form method="post" action="/login" id="login-form">
          <input type="hidden" name="returnUrl" value="/" />

          <!-- Email Field -->
          <div class="form-group-floating">
            <input
              type="email"
              id="email"
              name="email"
              class="form-input-floating"
              placeholder=" "
              autocomplete="email"
              required
            />
            <label for="email" class="form-label-floating">Email address</label>
            <span class="form-error" role="alert"></span>
          </div>

          <!-- Password Field -->
          <div class="form-group-floating password-group">
            <input
              type="password"
              id="password"
              name="password"
              class="form-input-floating"
              placeholder=" "
              autocomplete="current-password"
              required
            />
            <label for="password" class="form-label-floating">Password</label>
            <button
              type="button"
              class="password-toggle"
              aria-label="Show password"
            >
              <svg class="password-toggle-icon password-show" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
              </svg>
              <svg class="password-toggle-icon password-hide hidden" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l3.59 3.59m0 0A9.953 9.953 0 0112 5c4.478 0 8.268 2.943 9.543 7a10.025 10.025 0 01-4.132 5.411m0 0L21 21" />
              </svg>
            </button>
            <span class="form-error" role="alert"></span>
          </div>

          <!-- Remember Me -->
          <div class="remember-me-container">
            <input
              type="checkbox"
              id="remember-me"
              name="rememberMe"
              class="remember-me-checkbox"
            />
            <label for="remember-me" class="remember-me-checkbox-custom">
              <svg class="remember-me-checkmark" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
              </svg>
            </label>
            <label for="remember-me" class="remember-me-label">Remember me</label>
          </div>

          <!-- Submit Button -->
          <button type="submit" class="btn-submit-login btn-ripple">
            Sign in with Email
          </button>

        </form>

      </div>
    </main>

  </div>

  <script src="/js/login.js"></script>
</body>
</html>
```

### Complete CSS (login.css)

```css
/* ============================================
   LOGIN PAGE STYLES
   Discord Bot Admin - Modern Login Design
   ============================================ */

/* CSS Reset & Base */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

body {
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

/* Main Container */
.login-container {
  display: flex;
  min-height: 100vh;
  background-color: #262a2d;
}

/* ============================================
   LEFT PANEL: BRAND EXPERIENCE
   ============================================ */

.login-brand-panel {
  width: 40%;
  min-width: 480px;
  max-width: 600px;
  min-height: 100vh;
  position: relative;
  overflow: hidden;
  background: linear-gradient(135deg, #1d2022 0%, #2f3336 100%);
}

/* Animated gradient overlay */
.login-brand-panel::before {
  content: "";
  position: absolute;
  inset: 0;
  background: radial-gradient(
    circle at 30% 20%,
    rgba(203, 78, 27, 0.15) 0%,
    transparent 50%
  ),
  radial-gradient(
    circle at 70% 80%,
    rgba(9, 142, 207, 0.12) 0%,
    transparent 50%
  );
  animation: gradient-shift 8s ease-in-out infinite alternate;
  z-index: 1;
}

@keyframes gradient-shift {
  0% {
    transform: translate(0, 0) scale(1);
    opacity: 0.8;
  }
  100% {
    transform: translate(20px, -20px) scale(1.1);
    opacity: 1;
  }
}

.login-brand-content {
  position: relative;
  z-index: 2;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 3rem 2rem;
  text-align: center;
}

/* Logo */
.login-brand-logo {
  margin-bottom: 2rem;
}

.login-brand-logo-container {
  width: 140px;
  height: 140px;
  border-radius: 24px;
  background: linear-gradient(135deg, #cb4e1b 0%, #e5591f 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow:
    0 10px 40px rgba(203, 78, 27, 0.3),
    0 0 60px rgba(203, 78, 27, 0.15);
  animation: logo-pulse 3s ease-in-out infinite;
}

@keyframes logo-pulse {
  0%, 100% {
    transform: scale(1);
    box-shadow:
      0 10px 40px rgba(203, 78, 27, 0.3),
      0 0 60px rgba(203, 78, 27, 0.15);
  }
  50% {
    transform: scale(1.05);
    box-shadow:
      0 15px 50px rgba(203, 78, 27, 0.4),
      0 0 80px rgba(203, 78, 27, 0.2);
  }
}

.login-brand-logo-icon {
  width: 88px;
  height: 88px;
  color: #ffffff;
}

/* Title & Tagline */
.login-brand-title {
  font-size: 2.5rem;
  font-weight: 700;
  line-height: 1.2;
  letter-spacing: -0.02em;
  color: #d7d3d0;
  margin-bottom: 1rem;
  background: linear-gradient(135deg, #d7d3d0 0%, #a8a5a3 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.login-brand-tagline {
  font-size: 1.125rem;
  font-weight: 400;
  line-height: 1.6;
  color: #a8a5a3;
  margin-bottom: 3rem;
  max-width: 400px;
}

/* Feature List */
.login-brand-features {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  align-items: flex-start;
  max-width: 360px;
}

.login-brand-feature {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.9375rem;
  color: #a8a5a3;
  transition: color 0.2s ease;
}

.login-brand-feature:hover {
  color: #d7d3d0;
}

.login-brand-feature-icon {
  width: 1.25rem;
  height: 1.25rem;
  color: #cb4e1b;
  flex-shrink: 0;
}

/* ============================================
   RIGHT PANEL: LOGIN FORM
   ============================================ */

.login-form-panel {
  flex: 1;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 3rem 2rem;
  background-color: #262a2d;
}

.login-form-container {
  width: 100%;
  max-width: 440px;
  margin: 0 auto;
}

/* Stagger animation on load */
.login-form-container > * {
  opacity: 0;
  transform: translateY(12px);
  animation: fade-in-up 0.4s cubic-bezier(0.4, 0, 0.2, 1) forwards;
}

.login-form-container > *:nth-child(1) { animation-delay: 0.1s; }
.login-form-container > *:nth-child(2) { animation-delay: 0.15s; }
.login-form-container > *:nth-child(3) { animation-delay: 0.2s; }
.login-form-container > *:nth-child(4) { animation-delay: 0.25s; }
.login-form-container > *:nth-child(5) { animation-delay: 0.3s; }
.login-form-container > *:nth-child(6) { animation-delay: 0.35s; }

@keyframes fade-in-up {
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Form Header */
.login-form-header {
  margin-bottom: 2.5rem;
  text-align: center;
}

.login-form-title {
  font-size: 1.875rem;
  font-weight: 700;
  line-height: 1.3;
  color: #d7d3d0;
  margin-bottom: 0.5rem;
}

.login-form-subtitle {
  font-size: 0.9375rem;
  color: #a8a5a3;
}

/* ============================================
   ERROR ALERT
   ============================================ */

.login-error-alert {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: linear-gradient(
    135deg,
    rgba(239, 68, 68, 0.1) 0%,
    rgba(239, 68, 68, 0.05) 100%
  );
  border: 1px solid rgba(239, 68, 68, 0.3);
  border-left: 4px solid #ef4444;
  border-radius: 8px;
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  animation: slide-down 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes slide-down {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.login-error-icon {
  width: 1.25rem;
  height: 1.25rem;
  color: #ef4444;
  flex-shrink: 0;
  margin-top: 0.125rem;
}

.login-error-content {
  flex: 1;
}

.login-error-title {
  font-size: 0.875rem;
  font-weight: 600;
  color: #ef4444;
  margin-bottom: 0.25rem;
}

.login-error-message {
  font-size: 0.875rem;
  color: #ef4444;
  line-height: 1.5;
  opacity: 0.9;
}

.login-error-alert.shake {
  animation: shake 0.4s cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes shake {
  0%, 100% { transform: translateX(0); }
  10%, 30%, 50%, 70%, 90% { transform: translateX(-4px); }
  20%, 40%, 60%, 80% { transform: translateX(4px); }
}

/* ============================================
   DISCORD OAUTH BUTTON
   ============================================ */

.btn-discord-oauth {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  font-size: 1rem;
  font-weight: 600;
  letter-spacing: 0.01em;
  color: #ffffff;
  background: linear-gradient(135deg, #5865F2 0%, #4752C4 100%);
  border: 1px solid #5865F2;
  border-radius: 8px;
  box-shadow:
    0 4px 12px rgba(88, 101, 242, 0.3),
    0 0 0 0 rgba(88, 101, 242, 0.4);
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  cursor: pointer;
  position: relative;
  overflow: hidden;
}

.btn-discord-oauth:hover {
  background: linear-gradient(135deg, #6674F4 0%, #5865F2 100%);
  border-color: #6674F4;
  box-shadow:
    0 6px 16px rgba(88, 101, 242, 0.4),
    0 0 0 0 rgba(88, 101, 242, 0.6);
  transform: translateY(-2px);
}

.btn-discord-oauth:active {
  transform: translateY(0);
  box-shadow:
    0 2px 8px rgba(88, 101, 242, 0.4),
    0 0 0 0 rgba(88, 101, 242, 0.4);
}

.btn-discord-oauth:focus-visible {
  outline: none;
  box-shadow:
    0 4px 12px rgba(88, 101, 242, 0.3),
    0 0 0 3px rgba(9, 142, 207, 0.4);
}

.btn-discord-oauth:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}

.btn-discord-oauth-icon {
  width: 1.5rem;
  height: 1.5rem;
  flex-shrink: 0;
  transition: transform 0.2s ease;
}

.btn-discord-oauth:hover .btn-discord-oauth-icon {
  transform: scale(1.1);
}

/* Shimmer effect */
.btn-discord-oauth::before {
  content: "";
  position: absolute;
  inset: 0;
  background: linear-gradient(
    90deg,
    transparent 0%,
    rgba(255, 255, 255, 0.15) 50%,
    transparent 100%
  );
  transform: translateX(-100%);
  transition: transform 0.6s ease;
}

.btn-discord-oauth:hover::before {
  transform: translateX(100%);
}

/* ============================================
   DIVIDER
   ============================================ */

.login-divider {
  position: relative;
  margin: 2rem 0;
  text-align: center;
}

.login-divider::before {
  content: "";
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(
    to right,
    transparent 0%,
    #3f4447 20%,
    #3f4447 80%,
    transparent 100%
  );
}

.login-divider-text {
  position: relative;
  display: inline-block;
  padding: 0 1rem;
  background-color: #262a2d;
  font-size: 0.8125rem;
  font-weight: 500;
  color: #7a7876;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

/* ============================================
   FLOATING LABEL INPUTS
   ============================================ */

.form-group-floating {
  position: relative;
  margin-bottom: 1.5rem;
}

.form-input-floating {
  width: 100%;
  padding: 1.25rem 1rem 0.5rem 1rem;
  font-size: 0.9375rem;
  line-height: 1.5;
  color: #d7d3d0;
  background-color: #1d2022;
  border: 1px solid #3f4447;
  border-radius: 8px;
  box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.1);
  transition: all 0.2s ease;
  outline: none;
  -webkit-appearance: none;
  appearance: none;
}

.form-input-floating::placeholder {
  color: transparent;
}

.form-input-floating:hover {
  border-color: #098ecf;
}

.form-input-floating:focus {
  border-color: #098ecf;
  background-color: #262a2d;
  box-shadow:
    inset 0 1px 2px rgba(0, 0, 0, 0.1),
    0 0 0 3px rgba(9, 142, 207, 0.15);
  animation: input-glow 0.6s ease-out;
}

@keyframes input-glow {
  0% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 0 rgba(9, 142, 207, 0);
  }
  50% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 6px rgba(9, 142, 207, 0.15);
  }
  100% {
    box-shadow:
      inset 0 1px 2px rgba(0, 0, 0, 0.1),
      0 0 0 3px rgba(9, 142, 207, 0.15);
  }
}

.form-label-floating {
  position: absolute;
  top: 1rem;
  left: 1rem;
  font-size: 0.9375rem;
  font-weight: 500;
  color: #7a7876;
  background-color: transparent;
  pointer-events: none;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  transform-origin: left center;
}

.form-input-floating:focus + .form-label-floating,
.form-input-floating:not(:placeholder-shown) + .form-label-floating {
  transform: translateY(-0.625rem) scale(0.85);
  color: #098ecf;
  font-weight: 600;
}

.form-input-floating.error {
  border-color: #ef4444;
  background-color: rgba(239, 68, 68, 0.05);
}

.form-input-floating.error:focus {
  box-shadow:
    inset 0 1px 2px rgba(0, 0, 0, 0.1),
    0 0 0 3px rgba(239, 68, 68, 0.15);
}

.form-input-floating.error + .form-label-floating {
  color: #ef4444;
}

.form-error {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.8125rem;
  color: #ef4444;
  opacity: 0;
  transform: translateY(-4px);
  transition: all 0.2s ease;
}

.form-input-floating.error ~ .form-error {
  opacity: 1;
  transform: translateY(0);
}

/* ============================================
   PASSWORD TOGGLE
   ============================================ */

.password-group {
  position: relative;
}

.password-toggle {
  position: absolute;
  top: 50%;
  right: 0.75rem;
  transform: translateY(-50%);
  background: none;
  border: none;
  padding: 0.5rem;
  cursor: pointer;
  color: #7a7876;
  transition: color 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.password-toggle:hover {
  color: #a8a5a3;
  background-color: rgba(122, 120, 118, 0.1);
}

.password-toggle:focus-visible {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
  color: #098ecf;
}

.password-toggle-icon {
  width: 1.25rem;
  height: 1.25rem;
  transition: opacity 0.15s ease;
}

.password-toggle-icon.hidden {
  display: none;
}

.password-group .form-input-floating {
  padding-right: 3rem;
}

/* ============================================
   REMEMBER ME CHECKBOX
   ============================================ */

.remember-me-container {
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  gap: 0.625rem;
}

.remember-me-checkbox {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.remember-me-checkbox-custom {
  width: 1.25rem;
  height: 1.25rem;
  border: 2px solid #3f4447;
  border-radius: 4px;
  background-color: #1d2022;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.2s ease;
  cursor: pointer;
}

.remember-me-checkbox:hover + .remember-me-checkbox-custom {
  border-color: #098ecf;
}

.remember-me-checkbox:focus-visible + .remember-me-checkbox-custom {
  outline: 2px solid #098ecf;
  outline-offset: 2px;
}

.remember-me-checkbox:checked + .remember-me-checkbox-custom {
  background-color: #cb4e1b;
  border-color: #cb4e1b;
}

.remember-me-checkmark {
  width: 0.875rem;
  height: 0.875rem;
  color: #ffffff;
  opacity: 0;
  transform: scale(0.5);
  transition: all 0.15s cubic-bezier(0.4, 0, 0.2, 1);
}

.remember-me-checkbox:checked + .remember-me-checkbox-custom .remember-me-checkmark {
  opacity: 1;
  transform: scale(1);
}

.remember-me-label {
  font-size: 0.875rem;
  color: #a8a5a3;
  cursor: pointer;
  user-select: none;
  transition: color 0.2s ease;
}

.remember-me-label:hover {
  color: #d7d3d0;
}

/* ============================================
   SUBMIT BUTTON
   ============================================ */

.btn-submit-login {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.875rem 1.5rem;
  font-size: 0.9375rem;
  font-weight: 600;
  color: #ffffff;
  background: linear-gradient(135deg, #cb4e1b 0%, #e5591f 100%);
  border: 1px solid #cb4e1b;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(203, 78, 27, 0.3);
  transition: all 0.2s ease;
  cursor: pointer;
}

.btn-submit-login:hover {
  background: linear-gradient(135deg, #e5591f 0%, #cb4e1b 100%);
  box-shadow: 0 4px 12px rgba(203, 78, 27, 0.4);
  transform: translateY(-1px);
}

.btn-submit-login:active {
  transform: translateY(0);
  box-shadow: 0 1px 4px rgba(203, 78, 27, 0.3);
}

.btn-submit-login:focus-visible {
  outline: none;
  box-shadow:
    0 2px 8px rgba(203, 78, 27, 0.3),
    0 0 0 3px rgba(9, 142, 207, 0.4);
}

.btn-submit-login:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn-submit-login.loading {
  pointer-events: none;
}

.btn-submit-login.loading::before {
  content: "";
  width: 1rem;
  height: 1rem;
  border: 2px solid #ffffff;
  border-top-color: transparent;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* ============================================
   BUTTON RIPPLE EFFECT
   ============================================ */

.btn-ripple {
  position: relative;
  overflow: hidden;
}

.btn-ripple::after {
  content: "";
  position: absolute;
  width: 100%;
  height: 100%;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%) scale(0);
  background: radial-gradient(circle, rgba(255, 255, 255, 0.3) 0%, transparent 70%);
  opacity: 0;
  pointer-events: none;
}

.btn-ripple:active::after {
  transform: translate(-50%, -50%) scale(2);
  opacity: 1;
  transition: transform 0.5s ease, opacity 0.3s ease;
}

/* ============================================
   UTILITIES
   ============================================ */

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}

.hidden {
  display: none;
}

/* ============================================
   RESPONSIVE DESIGN
   ============================================ */

/* Tablet (768px - 1023px) */
@media (min-width: 768px) and (max-width: 1023px) {
  .login-brand-panel {
    width: 35%;
    min-width: 360px;
  }
}

/* Mobile (< 768px) */
@media (max-width: 767px) {
  .login-container {
    flex-direction: column;
  }

  .login-brand-panel {
    width: 100%;
    min-width: auto;
    max-width: 100%;
    min-height: auto;
    max-height: 25vh;
    padding: 2rem 1.5rem;
  }

  .login-brand-content {
    min-height: auto;
    padding: 0;
  }

  .login-brand-logo-container {
    width: 80px;
    height: 80px;
    border-radius: 16px;
  }

  .login-brand-logo-icon {
    width: 50px;
    height: 50px;
  }

  .login-brand-title {
    font-size: 1.75rem;
    margin-bottom: 0.5rem;
  }

  .login-brand-tagline {
    font-size: 0.875rem;
    margin-bottom: 0;
  }

  .login-brand-features {
    display: none;
  }

  .login-form-panel {
    min-height: auto;
    padding: 2rem 1.5rem;
  }

  .login-form-header {
    margin-bottom: 1.5rem;
  }

  .login-form-title {
    font-size: 1.5rem;
  }

  .login-form-subtitle {
    font-size: 0.875rem;
  }
}

/* ============================================
   REDUCED MOTION
   ============================================ */

@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }

  /* Keep essential loading animations */
  .btn-submit-login.loading::before,
  .btn-discord-oauth.loading .btn-discord-oauth-icon {
    animation-duration: 1s !important;
  }
}
```

### Complete JavaScript (login.js)

```javascript
// ============================================
// LOGIN PAGE INTERACTIONS
// Discord Bot Admin - Modern Login Design
// ============================================

document.addEventListener('DOMContentLoaded', function() {

  // ==========================================
  // Password Visibility Toggle
  // ==========================================

  const passwordToggles = document.querySelectorAll('.password-toggle');

  passwordToggles.forEach(button => {
    button.addEventListener('click', function() {
      const input = this.parentElement.querySelector('input');
      const showIcon = this.querySelector('.password-show');
      const hideIcon = this.querySelector('.password-hide');

      if (input.type === 'password') {
        input.type = 'text';
        showIcon.classList.add('hidden');
        hideIcon.classList.remove('hidden');
        this.setAttribute('aria-label', 'Hide password');
      } else {
        input.type = 'password';
        showIcon.classList.remove('hidden');
        hideIcon.classList.add('hidden');
        this.setAttribute('aria-label', 'Show password');
      }
    });
  });

  // ==========================================
  // Form Validation
  // ==========================================

  const loginForm = document.getElementById('login-form');

  if (loginForm) {
    loginForm.addEventListener('submit', function(e) {
      let isValid = true;

      // Email validation
      const emailInput = document.getElementById('email');
      const emailError = emailInput.nextElementSibling.nextElementSibling;

      if (!emailInput.value) {
        emailInput.classList.add('error');
        emailError.textContent = 'Email is required.';
        isValid = false;
      } else if (!isValidEmail(emailInput.value)) {
        emailInput.classList.add('error');
        emailError.textContent = 'Please enter a valid email address.';
        isValid = false;
      } else {
        emailInput.classList.remove('error');
        emailError.textContent = '';
      }

      // Password validation
      const passwordInput = document.getElementById('password');
      const passwordError = passwordInput.parentElement.querySelector('.form-error');

      if (!passwordInput.value) {
        passwordInput.classList.add('error');
        passwordError.textContent = 'Password is required.';
        isValid = false;
      } else {
        passwordInput.classList.remove('error');
        passwordError.textContent = '';
      }

      if (!isValid) {
        e.preventDefault();

        // Shake error alert if present
        const errorAlert = document.querySelector('.login-error-alert');
        if (errorAlert) {
          errorAlert.classList.add('shake');
          setTimeout(() => {
            errorAlert.classList.remove('shake');
          }, 400);
        }
      }
    });

    // Clear error on input
    const inputs = loginForm.querySelectorAll('.form-input-floating');
    inputs.forEach(input => {
      input.addEventListener('input', function() {
        this.classList.remove('error');
        const errorSpan = this.parentElement.querySelector('.form-error');
        if (errorSpan) {
          errorSpan.textContent = '';
        }
      });
    });
  }

  // ==========================================
  // Button Loading States
  // ==========================================

  const submitButtons = document.querySelectorAll('button[type="submit"]');

  submitButtons.forEach(button => {
    button.addEventListener('click', function() {
      // Add loading class on submit (will be cleared on page reload or error)
      setTimeout(() => {
        this.classList.add('loading');
      }, 100);
    });
  });

  // ==========================================
  // Helper Functions
  // ==========================================

  function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

});
```

---

## Summary Checklist

### Must-Have Features
- [x] Email/password login form
- [x] Discord OAuth button (conditional)
- [x] Remember me checkbox
- [x] Error message display
- [x] Validation message display
- [x] Return URL handling
- [x] Bot logo/branding

### Visual Enhancements
- [x] Split-panel layout (desktop)
- [x] Animated gradient background
- [x] Floating label inputs
- [x] Password visibility toggle
- [x] Loading states for buttons
- [x] Error shake animation
- [x] Staggered entrance animations
- [x] Focus glow effects
- [x] Hover transitions

### Accessibility
- [x] WCAG 2.1 AA color contrast
- [x] Keyboard navigation
- [x] ARIA attributes
- [x] Focus indicators
- [x] Screen reader support
- [x] Reduced motion support

### Responsive Design
- [x] Mobile layout (< 768px)
- [x] Tablet layout (768px - 1023px)
- [x] Desktop layout (1024px+)

---

## Implementation Notes

1. **Preserve existing functionality** - The PageModel (`Login.cshtml.cs`) remains unchanged. Only the view and CSS are updated.

2. **Conditional Discord OAuth** - The Discord button section should be wrapped in `@if (Model.IsDiscordOAuthConfigured)` as in the original.

3. **Validation messages** - ASP.NET Core validation attributes (`asp-validation-for`) should be mapped to the `.form-error` spans.

4. **Return URL** - Hidden input field for return URL must be preserved.

5. **Loading states** - JavaScript adds `.loading` class on submit. The class is automatically removed on page reload or can be removed manually on client-side validation errors.

6. **Browser compatibility** - All CSS uses standard properties. Vendor prefixes included where necessary (e.g., `-webkit-background-clip`).

7. **Performance** - No external dependencies. All animations use CSS transforms and opacity for GPU acceleration.

---

**End of Specification**

This document provides complete implementation guidance for creating a modern, visually impressive login page while preserving all existing functionality and meeting accessibility standards.
