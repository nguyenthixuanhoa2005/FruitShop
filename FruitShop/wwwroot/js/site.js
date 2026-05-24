$(document).ready(function () {
    // Sidebar Toggle (Desktop)
    $('#toggleSidebar').click(function () {
        $('#sidebar').toggleClass('collapsed');
        $('body').toggleClass('collapsed');
        
        const isCollapsed = $('#sidebar').hasClass('collapsed');
        localStorage.setItem('sidebarCollapsed', isCollapsed);

        if (isCollapsed) {
            $('.menu-item.has-submenu').removeClass('open');
            $('.submenu').removeClass('open');
        }
        
        const icon = $(this).find('i');
        if (isCollapsed) {
            icon.removeClass('bi-chevron-left').addClass('bi-chevron-right');
        } else {
            icon.removeClass('bi-chevron-right').addClass('bi-chevron-left');
        }
    });

    // Mobile Toggle
    $('.mobile-nav-toggle, .sidebar-overlay').click(function () {
        $('#sidebar').toggleClass('mobile-open');
        $('body').toggleClass('mobile-open');
    });

    // Submenu Toggle
    $('.menu-item.has-submenu').click(function (e) {
        if ($('#sidebar').hasClass('collapsed') && window.innerWidth > 992) return;
        
        e.preventDefault();
        $(this).toggleClass('open');
        $(this).next('.submenu').toggleClass('open');
    });

    // Restore Sidebar state
    if (window.innerWidth > 992) {
        const savedState = localStorage.getItem('sidebarCollapsed');
        if (savedState === 'true') {
            $('#sidebar').addClass('collapsed');
            $('body').addClass('collapsed');
            $('#toggleSidebar').find('i').removeClass('bi-chevron-left').addClass('bi-chevron-right');
        }
    }

    // --- 3D DARK MODE LOGIC ---
    const themeToggle = document.getElementById('themeToggle');
    
    if (themeToggle) {
        themeToggle.addEventListener('click', () => {
            const isDark = document.documentElement.classList.toggle('dark-mode');
            localStorage.setItem('theme', isDark ? 'dark' : 'light');
            window.dispatchEvent(new Event('themeChanged'));
        });
    }

    // Shortcut Alt + T
    window.addEventListener('keydown', (e) => {
        if (e.altKey && e.key.toLowerCase() === 't') {
            themeToggle?.click();
        }
    });

    // Auto-close sidebar on mobile
    $('.sidebar-menu .menu-item:not(.has-submenu)').click(function() {
        if (window.innerWidth <= 992) {
            $('#sidebar').removeClass('mobile-open');
            $('body').removeClass('mobile-open');
        }
    });
});