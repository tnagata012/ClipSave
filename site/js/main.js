// ============================================================
// ClipSave Landing Page — JavaScript
// ============================================================

(function () {
  "use strict";

  // --- Scroll fade-in with IntersectionObserver ---
  const fadeEls = document.querySelectorAll(".fade-in");

  if ("IntersectionObserver" in window) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("visible");
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.15 }
    );

    fadeEls.forEach((el) => observer.observe(el));
  } else {
    // Fallback: show all immediately
    fadeEls.forEach((el) => el.classList.add("visible"));
  }

  // --- Mobile menu toggle ---
  const menuBtn = document.querySelector(".mobile-menu-btn");
  const nav = document.querySelector(".nav");

  function setMenuState(isOpen) {
    if (!menuBtn || !nav) {
      return;
    }

    menuBtn.classList.toggle("active", isOpen);
    nav.classList.toggle("open", isOpen);
    menuBtn.setAttribute("aria-expanded", String(isOpen));
    document.body.classList.toggle("menu-open", isOpen);
  }

  if (menuBtn && nav) {
    setMenuState(false);

    menuBtn.addEventListener("click", () => {
      setMenuState(!nav.classList.contains("open"));
    });

    // Close menu when a nav link is clicked
    nav.querySelectorAll("a").forEach((link) => {
      link.addEventListener("click", () => {
        setMenuState(false);
      });
    });

    // Close menu by keyboard
    window.addEventListener("keydown", (event) => {
      if (event.key === "Escape" && nav.classList.contains("open")) {
        setMenuState(false);
      }
    });

    // Close when user taps outside
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element) || !nav.classList.contains("open")) {
        return;
      }

      if (!nav.contains(target) && !menuBtn.contains(target)) {
        setMenuState(false);
      }
    });

    // Ensure desktop view is never stuck in mobile open state
    window.addEventListener("resize", () => {
      if (window.innerWidth > 640 && nav.classList.contains("open")) {
        setMenuState(false);
      }
    });
  }

  // --- Header background on scroll ---
  const header = document.querySelector(".header");

  // --- Back to top button ---
  const backToTop = document.querySelector(".back-to-top");

  // --- Active nav highlighting ---
  const navLinks = document.querySelectorAll(".nav a[href^='#']");
  const sections = [];

  navLinks.forEach((link) => {
    const href = link.getAttribute("href");
    if (href && href !== "#" && !link.classList.contains("btn")) {
      const section = document.querySelector(href);
      if (section) {
        sections.push({ el: section, link: link });
      }
    }
  });

  function updateActiveNav() {
    const scrollY = window.scrollY + 120;

    let currentSection = null;
    for (let i = sections.length - 1; i >= 0; i--) {
      if (sections[i].el.offsetTop <= scrollY) {
        currentSection = sections[i];
        break;
      }
    }

    navLinks.forEach((link) => link.classList.remove("active"));
    if (currentSection) {
      currentSection.link.classList.add("active");
    }
  }

  // --- Scroll event handler ---
  function handleScroll() {
    const scrollY = window.scrollY;

    // Header border
    if (header) {
      header.style.borderBottomColor =
        scrollY > 50 ? "rgba(30, 41, 59, 0.8)" : "";
    }

    // Back to top visibility
    if (backToTop) {
      if (scrollY > 400) {
        backToTop.classList.add("visible");
      } else {
        backToTop.classList.remove("visible");
      }
    }

    // Active nav
    if (sections.length > 0) {
      updateActiveNav();
    }
  }

  if (header || backToTop || sections.length > 0) {
    window.addEventListener("scroll", handleScroll, { passive: true });
    handleScroll();
  }

  // Back to top click
  if (backToTop) {
    backToTop.addEventListener("click", () => {
      window.scrollTo({ top: 0, behavior: "smooth" });
    });
  }
})();
