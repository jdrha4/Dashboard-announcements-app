document.addEventListener("DOMContentLoaded", function () {
  // Funkcia na prepínanie viditeľnosti hesla
  function setupPasswordToggle() {
    document.querySelectorAll(".password-wrapper").forEach((wrapper) => {
      const passwordField = wrapper.querySelector("input");
      const togglePassword = wrapper.querySelector(".toggle-password");

      if (!togglePassword || !passwordField) return;

      togglePassword.addEventListener("click", function () {
        const isPassword = passwordField.type === "password";
        passwordField.type = isPassword ? "text" : "password";

        // Prepnutie Bootstrap ikon
        this.classList.toggle("bi-eye", !isPassword);
        this.classList.toggle("bi-eye-slash", isPassword);
      });
    });
  }

  // Spustenie funkcií pri načítaní stránky
  setupPasswordToggle();
});
