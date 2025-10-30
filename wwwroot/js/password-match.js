function updatePasswordMatchRefs() {
  const passwordInput = document.querySelector("#Password");
  const repeatInput = document.querySelector("#PasswordRepeat");
  const matchIcon = document.querySelector("#passwordMatchIcon");
  const mismatchIcon = document.querySelector("#passwordMismatchIcon");

  if (!passwordInput || !repeatInput || !matchIcon || !mismatchIcon) return;

  function checkPasswordsMatch() {
    const password = passwordInput.value;
    const repeat = repeatInput.value;

    if (!password || !repeat) {
      matchIcon.style.display = "none";
      mismatchIcon.style.display = "none";
    } else if (password === repeat) {
      matchIcon.style.display = "block";
      mismatchIcon.style.display = "none";
    } else {
      matchIcon.style.display = "none";
      mismatchIcon.style.display = "block";
    }
  }

  passwordInput.addEventListener("input", checkPasswordsMatch);
  repeatInput.addEventListener("input", checkPasswordsMatch);

  checkPasswordsMatch();
}

updatePasswordMatchRefs();
