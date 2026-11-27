// The templates use the colors of the PrimeNG theme (bg-surface-50, text-primary-600...):
// each one reads the CSS variable the theme defines, so the two always agree.
const themeColor = (name) => `color-mix(in srgb, var(--p-${name}) calc(<alpha-value> * 100%), transparent)`;
const shades = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];
const palette = (name, steps) => Object.fromEntries(steps.map((step) => [step, themeColor(`${name}-${step}`)]));

/** @type {import('tailwindcss').Config} */
module.exports = {
    // Same selector as PrimeNG (app.config.ts): the dark: variants apply only when the theme is dark too
    darkMode: ['selector', '.my-app-dark'],
    content: [
        "./src/**/*.{html,ts}",
    ],
    theme: {
        extend: {
            colors: {
                surface: palette('surface', [0, ...shades]),
                primary: { DEFAULT: themeColor('primary-color'), ...palette('primary', shades) },
            },
        },
    },
    plugins: [],
}
