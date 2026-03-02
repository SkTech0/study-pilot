/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      borderRadius: {
        'sp': 'var(--sp-radius)',
        'sp-lg': 'var(--sp-radius-lg)',
      },
      boxShadow: {
        'sp': 'var(--sp-shadow)',
        'sp-md': 'var(--sp-shadow-md)',
      },
    },
  },
  plugins: [],
}

