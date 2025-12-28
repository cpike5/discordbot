// ============================================
// LOGIN PAGE JAVASCRIPT
// ============================================

document.addEventListener('DOMContentLoaded', function() {

  // ============================================
  // PASSWORD VISIBILITY TOGGLE
  // ============================================

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
        this.setAttribute('aria-pressed', 'true');
      } else {
        input.type = 'password';
        showIcon.classList.remove('hidden');
        hideIcon.classList.add('hidden');
        this.setAttribute('aria-label', 'Show password');
        this.setAttribute('aria-pressed', 'false');
      }
    });
  });

  // ============================================
  // FORM VALIDATION
  // ============================================

  const loginForm = document.getElementById('login-form');
  if (!loginForm) return;

  const emailInput = document.getElementById('email');
  const passwordInput = document.getElementById('password');
  const emailError = document.getElementById('email-error');
  const passwordError = document.getElementById('password-error');

  // Validate email on blur
  if (emailInput) {
    emailInput.addEventListener('blur', function() {
      validateEmail();
    });

    // Clear error on focus
    emailInput.addEventListener('focus', function() {
      clearError(emailInput, emailError);
    });
  }

  // Validate password on blur
  if (passwordInput) {
    passwordInput.addEventListener('blur', function() {
      validatePassword();
    });

    // Clear error on focus
    passwordInput.addEventListener('focus', function() {
      clearError(passwordInput, passwordError);
    });
  }

  function validateEmail() {
    const email = emailInput.value.trim();
    if (!email) {
      setError(emailInput, emailError, 'Email address is required');
      return false;
    }
    if (!isValidEmail(email)) {
      setError(emailInput, emailError, 'Please enter a valid email address');
      return false;
    }
    clearError(emailInput, emailError);
    return true;
  }

  function validatePassword() {
    const password = passwordInput.value;
    if (!password) {
      setError(passwordInput, passwordError, 'Password is required');
      return false;
    }
    clearError(passwordInput, passwordError);
    return true;
  }

  function isValidEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  function setError(input, errorElement, message) {
    input.classList.add('error');
    input.setAttribute('aria-invalid', 'true');
    if (errorElement && !errorElement.textContent) {
      errorElement.textContent = message;
    }
  }

  function clearError(input, errorElement) {
    input.classList.remove('error');
    input.setAttribute('aria-invalid', 'false');
    // Don't clear server-side validation messages
    if (errorElement && errorElement.classList.contains('field-validation-error')) {
      return;
    }
    if (errorElement) {
      errorElement.textContent = '';
    }
  }

});
