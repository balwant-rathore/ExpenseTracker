/** @type {import('@commitlint/types').UserConfig} */
module.exports = {
  extends: ['@commitlint/config-conventional'],
  plugins: [
    {
      rules: {
        'scope-ticket-format': (parsed) => {
          const { scope } = parsed
          if (!scope) {
            return [true]
          }
          return [
            /^ET\d{3,4}$/.test(scope),
            'scope must be omitted or match a ticket ID like ET007 (see docs/TICKETS.md)',
          ]
        },
      },
    },
  ],
  rules: {
    'type-enum': [2, 'always', ['feat', 'fix', 'test', 'refactor', 'chore', 'docs']],
    'scope-ticket-format': [2, 'always'],
  },
}
