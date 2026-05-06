/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        severity: {
          advisory: '#3b82f6',
          caution:  '#f59e0b',
          warning:  '#f97316',
          critical: '#ef4444',
        }
      }
    },
  },
  plugins: [],
}
