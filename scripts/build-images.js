#!/usr/bin/env node
'use strict';

/**
 * Локальна збірка: Linux Docker-образ + Windows-бінарник (Native AOT) без Docker.
 *
 * Що робить:
 *   1. Перевіряє і за потреби запускає Docker Desktop (рушій має бути
 *      в режимі Linux-контейнерів — це режим за замовчуванням, перемикати не потрібно).
 *   2. Збирає Linux-образ з Dockerfile.linux та витягує готовий бінарник
 *      з образу в linux-out (щоб мати артефакт на диску, а не тільки в образі).
 *   3. Локально, через `dotnet publish -r win-x64 -p:PublishAot=true`, збирає
 *      Windows-бінарник — без будь-яких контейнерів, напряму на хості.
 *
 * За замовчуванням корінь проєкту обчислюється відносно розташування самого
 * скрипта (на рівень вище), тому запускати можна з будь-якого місця:
 *   node .\scripts\build-images.js
 *   node build-images.js               (якщо ви вже в папці scripts)
 *
 * Опції:
 *   --image-name <name>   Ім'я Linux Docker-образу (за замовч. notifyservice)
 *   --tag <tag>            Тег образу (за замовч. local)
 *   --project-dir <path>   Корінь проєкту (за замовч. батьківська папка від скрипта)
 *   --csproj <path>        Шлях до .csproj відносно project-dir (за замовч. ./Notify.csproj)
 *   --win-out <path>       Куди класти windows-бінарник (за замовч. ./publish/windows)
 *   --linux-out <path>     Куди класти linux-бінарник (за замовч. ./publish/linux)
 *   --skip-linux           Пропустити збірку Linux-образу
 *   --skip-windows         Пропустити локальну збірку Windows-бінарника
 *   -h, --help             Ця довідка
 */

const { spawnSync, spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

// ---------- аргументи командного рядка ----------

function parseArgs(argv) {
  const args = {
    imageName: 'notify-service',
    tag: 'dev',
    projectDir: path.resolve(__dirname, '..'),
    csprojPath: './Notify.csproj',
    winOutputDir: './publish/windows',
    linuxOutputDir: './publish/linux',
    skipLinux: false,
    skipWindows: false,
  };

  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    switch (a) {
      case '--image-name': args.imageName = argv[++i]; break;
      case '--tag': args.tag = argv[++i]; break;
      case '--project-dir': args.projectDir = path.resolve(argv[++i]); break;
      case '--csproj': args.csprojPath = argv[++i]; break;
      case '--win-out': args.winOutputDir = argv[++i]; break;
      case '--linux-out': args.linuxOutputDir = argv[++i]; break;
      case '--skip-linux': args.skipLinux = true; break;
      case '--skip-windows': args.skipWindows = true; break;
      case '-h':
      case '--help':
        printHelp();
        process.exit(0);
        break;
      default:
        console.warn(`Невідомий аргумент: ${a} (див. --help)`);
    }
  }
  return args;
}

function printHelp() {
  console.log(`
Використання: node build-images.js [опції]

  --image-name <name>   Ім'я Linux Docker-образу (за замовч. notifyservice)
  --tag <tag>            Тег образу (за замовч. local)
  --project-dir <path>   Корінь проєкту (за замовч. батьківська папка від скрипта)
  --csproj <path>        Шлях до .csproj відносно project-dir (за замовч. ./Notify.csproj)
  --win-out <path>       Куди класти windows-бінарник (за замовч. ./publish/windows)
  --linux-out <path>     Куди класти linux-бінарник (за замовч. ./publish/linux)
  --skip-linux           Пропустити збірку Linux-образу
  --skip-windows         Пропустити локальну збірку Windows-бінарника
  -h, --help             Ця довідка
`);
}

// ---------- допоміжні функції ----------

function step(message) {
  console.log(`\n==== ${message} ====`);
}

/** Запуск команди зі стрімінгом виводу в консоль; кидає помилку при ненульовому коді виходу. */
function run(cmd, cmdArgs, opts = {}) {
  const result = spawnSync(cmd, cmdArgs, { stdio: 'inherit', ...opts });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    throw new Error(`Команда "${cmd} ${cmdArgs.join(' ')}" завершилась з кодом ${result.status}`);
  }
  return result;
}

/** Запуск команди з перехопленням виводу (без стріму в консоль). Не кидає помилку сама. */
function runCapture(cmd, cmdArgs, opts = {}) {
  return spawnSync(cmd, cmdArgs, { encoding: 'utf8', ...opts });
}

function sleepSync(ms) {
  const shared = new Int32Array(new SharedArrayBuffer(4));
  Atomics.wait(shared, 0, 0, ms);
}

function dockerOsType() {
  const res = runCapture('docker', ['info', '--format', '{{.OSType}}']);
  if (res.status !== 0) return null;
  return (res.stdout || '').trim();
}

function waitDockerReady(timeoutSec = 120) {
  const deadline = Date.now() + timeoutSec * 1000;
  while (Date.now() < deadline) {
    const res = runCapture('docker', ['info']);
    if (res.status === 0) return true;
    sleepSync(3000);
  }
  return false;
}

function ensureDockerRunning() {
  step('Перевірка Docker Desktop');

  let res = runCapture('docker', ['info']);
  if (res.status !== 0) {
    console.log('Docker не запущено, намагаюсь запустити Docker Desktop...');
    const dockerDesktopExe = path.join(
      process.env.ProgramFiles || 'C:\\Program Files',
      'Docker', 'Docker', 'Docker Desktop.exe'
    );
    if (!fs.existsSync(dockerDesktopExe)) {
      throw new Error(
        `Docker Desktop не знайдено за шляхом '${dockerDesktopExe}'. Встановіть Docker Desktop або запустіть його вручну.`
      );
    }
    spawn(dockerDesktopExe, [], { detached: true, stdio: 'ignore' }).unref();
    console.log('Очікую запуску рушія...');
    if (!waitDockerReady(180)) {
      throw new Error("Docker Desktop не вийшов на зв'язок за відведений час (180с).");
    }
  }

  const osType = dockerOsType();
  if (osType !== 'linux') {
    throw new Error(
      `Docker Desktop зараз у режимі '${osType}', а потрібен 'linux'. ` +
      `Перемкніть через трей-іконку Docker Desktop (Switch to Linux containers) і перезапустіть скрипт.`
    );
  }
  console.log('Docker запущено, рушій у режимі linux.');
}

function exportLinuxArtifact(imageTag, outputDir) {
  step('Витягуємо бінарник з Linux-образу');

  if (fs.existsSync(outputDir)) {
    fs.rmSync(outputDir, { recursive: true, force: true });
  }
  fs.mkdirSync(outputDir, { recursive: true });

  const createRes = runCapture('docker', ['create', imageTag]);
  if (createRes.status !== 0 || !createRes.stdout) {
    throw new Error(`Не вдалося створити тимчасовий контейнер з образу '${imageTag}'.`);
  }
  const containerId = createRes.stdout.trim();

  try {
    // WORKDIR фінального стейджа у Dockerfile.linux — /app
    run('docker', ['cp', `${containerId}:/app/.`, outputDir]);
  } finally {
    runCapture('docker', ['rm', containerId]);
  }

  console.log(`Готово: файли з образу скопійовано в ${outputDir}`);
}

function listDir(dir) {
  if (!fs.existsSync(dir)) {
    console.log('  (порожньо)');
    return;
  }
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const stat = fs.statSync(full);
    if (stat.isFile()) {
      console.log(`  ${name}\t${stat.size} байт`);
    }
  }
}

// ---------- main ----------

function main() {
  const args = parseArgs(process.argv.slice(2));

  console.log(`Корінь проєкту: ${args.projectDir}`);

  if (!fs.existsSync(args.projectDir)) {
    throw new Error(`Папка проєкту не знайдена: ${args.projectDir}`);
  }
  process.chdir(args.projectDir);

  if (!args.skipLinux) {
    ensureDockerRunning();

    if (!fs.existsSync('Dockerfile.linux')) {
      throw new Error(`Не знайдено Dockerfile.linux у '${process.cwd()}'. Перевірте --project-dir.`);
    }

    step('Збірка Linux-образу');
    const linuxTag = `${args.imageName}:${args.tag}-linux`;
    run('docker', ['build', '-f', 'Dockerfile.linux', '-t', linuxTag, '.']);
    console.log(`Готово: ${linuxTag}`);

    exportLinuxArtifact(linuxTag, args.linuxOutputDir);
  }

  if (!args.skipWindows) {
    step('Локальна збірка Windows-бінарника (Native AOT, без Docker)');

    if (!fs.existsSync(args.csprojPath)) {
      throw new Error(
        `Не знайдено проєкт '${args.csprojPath}' (відносно '${process.cwd()}'). Вкажіть правильний шлях через --csproj.`
      );
    }

    run('dotnet', [
      'publish', args.csprojPath,
      '-c', 'Release',
      '-r', 'win-x64',
      '--self-contained', 'true',
      '-p:PublishAot=true',
      '-o', args.winOutputDir,
    ]);

    console.log(`Готово: результат у ${args.winOutputDir}`);
  }

  step('Підсумок');

  if (!args.skipLinux) {
    const imagesRes = runCapture('docker', ['images']);
    const lines = (imagesRes.stdout || '')
      .split('\n')
      .filter((l) => l.includes(args.imageName));
    console.log(lines.join('\n'));
    console.log('\nLinux-артефакт:');
    listDir(args.linuxOutputDir);
  }

  if (!args.skipWindows) {
    console.log('\nWindows-артефакт:');
    listDir(args.winOutputDir);
  }
}

try {
  main();
} catch (err) {
  console.error(`\nПОМИЛКА: ${err.message}`);
  process.exit(1);
}