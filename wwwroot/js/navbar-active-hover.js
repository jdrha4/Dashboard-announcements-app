document.addEventListener("DOMContentLoaded", function() {
  // Select all navbar links
  const navLinks = document.querySelectorAll('.navbar-nav .nav-link');
  // Get the current page path
  const currentPath = window.location.pathname.toLowerCase();

  // Special activation rules, when the link's href to current URL match alone isn't sufficient
  // This maps navbar links (based on their href paths) to activation rules based on the current URL.
  const specialCases = [
    {
      activeWhenPathStartsWith: "/auth/forgot-password",
      linkShouldContain: "/auth/login"
    }
    // Additional cases can be added here
  ];

  navLinks.forEach(link => {
    // Get the link path
    const linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase();
    let isActive = false;

    // Check for direct path match
    if (currentPath.startsWith(linkPath)) {
      isActive = true;
    }

    // Check for special activation rules
    if (!isActive) {
      for (const special of specialCases) {
        if (currentPath.startsWith(special.activeWhenPathStartsWith) &&
          linkPath.includes(special.linkShouldContain)) {
          isActive = true;
          break;
        }
      }
    }

    // Toggle the 'active' class
    if (isActive) {
      link.classList.add('active');
    } else {
      link.classList.remove('active');
    }
  });
});
