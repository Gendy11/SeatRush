// Windows: Vitest breaks ("failed to find the runner") when the working directory's drive letter is
// lowercase (VS Code terminals often start in "c:\..."), because it then loads itself twice (c:/ and C:/).
// Re-launch `ng test` from the same folder with an uppercase drive letter.
import { spawnSync } from 'node:child_process';

const cwd = process.cwd().replace(/^[a-z]:/, (d) => d.toUpperCase());
const result = spawnSync('npx', ['ng', 'test', ...process.argv.slice(2)], { cwd, stdio: 'inherit', shell: true });
process.exit(result.status ?? 1);
