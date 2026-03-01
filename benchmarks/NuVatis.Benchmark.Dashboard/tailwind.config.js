/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        nuvatis: '#4F46E5',
        dapper: '#10B981',
        efcore: '#F59E0B'
      }
    }
  },
  plugins: []
};
