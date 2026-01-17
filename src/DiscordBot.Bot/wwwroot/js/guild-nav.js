// Guild Navigation Dropdown Toggle
// Handles mobile dropdown for guild navigation tabs

document.addEventListener('DOMContentLoaded', function() {
    const dropdownToggle = document.getElementById('guildNavDropdownToggle');
    const dropdownMenu = document.getElementById('guildNavDropdownMenu');

    if (dropdownToggle && dropdownMenu) {
        // Toggle dropdown on button click
        dropdownToggle.addEventListener('click', function(e) {
            e.stopPropagation();
            const isHidden = dropdownMenu.classList.toggle('hidden');
            dropdownToggle.setAttribute('aria-expanded', !isHidden);
        });

        // Close dropdown on click outside
        document.addEventListener('click', function(e) {
            if (!e.target.closest('.guild-nav-dropdown')) {
                dropdownMenu.classList.add('hidden');
                dropdownToggle.setAttribute('aria-expanded', 'false');
            }
        });

        // Close dropdown on Escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && !dropdownMenu.classList.contains('hidden')) {
                dropdownMenu.classList.add('hidden');
                dropdownToggle.setAttribute('aria-expanded', 'false');
                dropdownToggle.focus();
            }
        });
    }
});
