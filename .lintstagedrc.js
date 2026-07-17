const path = require('node:path')

function toPosixRelative(base, files) {
  return files.map((file) => path.relative(base, file).split(path.sep).join('/'))
}

module.exports = {
  'backend/**/*.cs': (files) => {
    const relativePaths = toPosixRelative(process.cwd(), files)
    return `dotnet format backend/ExpenseTracker.sln --include ${relativePaths.join(' ')}`
  },
  'frontend/**/*.{ts,tsx}': (files) => {
    const relativePaths = toPosixRelative(path.join(process.cwd(), 'frontend'), files)
    return `pnpm --filter frontend exec oxlint --max-warnings=0 ${relativePaths.join(' ')}`
  },
}
