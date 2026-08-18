/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/LeadRelay.Web/Views/**/*.cshtml"],
  theme: {
    extend: {
      fontFamily: {
        sans: ["Inter", "system-ui", "sans-serif"],
        display: ["Manrope", "Inter", "system-ui", "sans-serif"]
      },
      boxShadow: {
        panel: "0 1px 2px rgba(15, 23, 42, 0.06)"
      }
    }
  },
  plugins: []
};
