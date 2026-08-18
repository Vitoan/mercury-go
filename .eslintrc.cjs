module.exports = {
  root: true,
  env: {
    node: true,
    browser: true,
    es2021: true
  },
  'extends': [
    'plugin:vue/vue3-essential',
    'eslint:recommended'
  ],
  parserOptions: {
    ecmaVersion: 2021
  },
  rules: {
    'no-console': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
    'no-debugger': process.env.NODE_ENV === 'production' ? 'warn' : 'off',
    'vue/no-deprecated-slot-attribute': 'off'
  },
  overrides: [
    {
      files: ['tests/e2e/**/*.{js,jsx}'],
      globals: {
        describe: 'readonly',
        it: 'readonly',
        cy: 'readonly',
        before: 'readonly',
        beforeEach: 'readonly',
        after: 'readonly',
        afterEach: 'readonly',
        expect: 'readonly'
      }
    }
  ]
}
