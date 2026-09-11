# SmartDrag — аудит передачи в Codex

Дата аудита: **2026-09-03**
Исходная точка: **foundation v0.9 → preview vertical slice**

## Короткий вывод

SmartDrag сейчас — ещё не production-приложение, но уже имеет рабочий визуально-функциональный preview-срез: WPF overlay, WPF preview host и Windows WIC JPEG/PNG adapter. Production путь ещё не замкнут: глобальная активация намеренно выключена, WebP encoder/G3 limits не приняты, а Windows interoperability не подтверждена на живом Explorer.

Preview используется для итерации UI и локального output pipeline. Следующие обязательные gates — G1/G2 (Windows drag/drop proof), затем G3 (выбор production codec/limits); native production activation остаётся выключенной до evidence.

## Что было проверено при передаче

- Прочитаны канонический контекст, handoff-комплект, текущее состояние, ADR-001..041, открытые вопросы, специализированные спецификации и test matrices.
- Проинвентаризированы исходники, тесты, probe, скрипты, схемы и image fixtures.
- SHA-256 handoff-пакета проверены по `SHA256SUMS.txt`: расхождений **0**.
- `scripts/validate_repo.py`: **PASS**.
- `scripts/validate_image_corpus.py`: **PASS**, 9 fixtures.
- Обнаружено 19 .NET-проектов; после реализации preview автоматический suite содержит 127 тестов.
- G0 выполнен на Windows 10.0.19045 с .NET SDK 8.0.424: 127 passed, 0 failed, 0 skipped; единственное предупреждение — недоступный внешний NuGet advisory feed `NU1900`.
- Git baseline создан внутри `CURRENT_REPO/SmartDrag`; дальнейшие изменения отделены коммитами.

## Продуктовый контракт MVP

Один файл JPEG/PNG из Explorer перетаскивается к компактному неактивирующему overlay. Пользователь выбирает ровно одно из трёх локальных действий:

- Compress;
- Convert to WebP;
- Remove Metadata.

Результат всегда создаётся новым файлом; source сохраняется. Обычный Explorer drag/drop важнее SmartDrag и не должен ломаться, если SmartDrag не уверен, проигнорирован или аварийно завершился.

PDF, archive, video, FFmpeg, batch, cloud и in-place processing не входят в первый MVP.

## Карта компонентов

| Компонент | Роль | Фактическая зрелость |
|---|---|---|
| `SmartDrag.Core` | Чистые модели, state machines, payload/output/settings/contracts | Реализован, покрыт unit-тестами, build не подтверждён |
| `SmartDrag.Actions` | Registry трёх MVP actions и exception-safe executor | Реализован, build не подтверждён |
| `SmartDrag.Orchestration` | Preflight authorization, Drop revalidation, commit gate, idempotent dispatch | Реализован как ключевая safety-граница |
| `SmartDrag.Runtime` | Последовательная очередь, cancel, terminal snapshots, completion commands | Реализован, build/runtime не подтверждены |
| `SmartDrag.Infrastructure` | Payload snapshot, safe output reservation/commit, durable recovery journal | Реализован; destructive/recovery сценарии требуют реального исполнения |
| `SmartDrag.Imaging` | Codec-neutral semantics, guard и handlers | Каркас готов; WIC adapter подключается только в preview |
| `SmartDrag.Windows` | WinEvent/OLE/Shell/clipboard/mutex/file identity interop | Код есть; главные G1/G2/P4 утверждения эмпирически не доказаны |
| `SmartDrag.Presentation` | Toolkit-neutral snapshots и user intents | Реализован; production status/completion host ещё не подключён |
| `SmartDrag.Overlay` | WPF visual boundary | Реализован как non-activating overlay adapter |
| `SmartDrag.App` | Safety bootstrap и authoritative composition root | Runtime graph есть; `--preview` запускает безопасный preview, production path inert |
| `SmartDrag.Windows.Probe` | Диагностический P0/G2 raw HWND/OLE probe | Реализован и собран; ручной evidence ещё не принят |

## Уже защищённые архитектурные цепочки

### Drag authorization

```text
пассивный drag signal
  -> консервативный preflight одного поддерживаемого файла
  -> immutable PreparedOverlaySession
  -> реальный OLE CF_HDROP на Drop
  -> совпадение preflight и Drop identity
  -> выбранный ActionId был предложен и всё ещё доступен
  -> внутренний source-preserving OutputPolicy
  -> один idempotent ActionRequest
  -> очередь
```

### Output safety

```text
durable journal record
  -> SmartDrag-owned partial
  -> encode/write
  -> collision recheck
  -> no-overwrite move
  -> strong identity capture
  -> journal cleanup
```

### Startup safety

```text
single-instance mutex
  -> open recovery journal
  -> exact journal-driven cleanup
  -> runtime graph
  -> только затем native hooks/UI
```

## Что реально отсутствует

1. Production application lifetime и глобальная native activation после gate evidence.
2. Доказательство, что WinEvent signal и компактный OLE target сосуществуют с Explorer без focus/drop regressions.
3. Надёжная pre-overlay qualification для Explorer windows/tabs; Desktop остаётся отдельным вопросом.
4. Production renderer/status/completion wiring поверх preview adapter.
5. Production-approved codec: WIC preview покрывает JPEG/PNG actions, но WebP и числовые limits требуют G3.
6. Реальный status/completion renderer.
7. Запускающий `Program.Main`, регистрация proven hooks и полноценный application lifetime.
8. End-to-end путь Explorer -> action -> output -> completion.
9. Release packaging, signing, autostart/update/support policy.

## Оставшийся путь — строгий порядок

### 1. Нормализовать рабочую базу

- Поместить `CURRENT_REPO/SmartDrag` в Git и создать неизменённый baseline commit v0.9.
- Исторические ZIP/handoff-файлы держать вне active source tree или только как read-only archaeology.
- Не смешивать организационные изменения с G0 compile fixes.

### 2. G0 — build/test

- Установить .NET 8 SDK.
- Запустить `scripts/build.ps1` с разрешённой PowerShell execution policy и доступным Python.
- Исправлять только реальные compile/test defects, особенно COM marshalling, LibraryImport, mutex lifetime, recovery serialization, event/locking/disposal.
- Зафиксировать OS, SDK, команды, test count и fixes в `PROJECT_STATE.md`.

### 3. G1 — P0 Explorer coexistence

- Собрать и запустить probe `--mode=p0` на интерактивной Windows 11.
- Пройти P0 matrix на Explorer, repeated drags, mixed DPI, negative coordinates и shutdown.
- Сохранить JSONL и анализ, а не только визуальное впечатление.
- Если WinEvent strategy не работает, оформить evidence и новый ADR до изменения подхода.

### 4. G2 — strict qualification

- Запустить probe `--mode=g2`.
- Доказать ровно один JPEG/PNG, правильное окно/вкладку, отказ для multi/folder/virtual/unsupported и mismatch A/B.
- Отдельно решить Desktop: безопасная qualification либо явное исключение из MVP.

### 5. Production renderer

- После G1/G2 выбрать технологию, сохраняющую proven raw HWND/OLE behavior.
- Реализовать production overlay и status/completion windows без focus theft.
- ADR должен отдельно решить keyboard accessibility, transparent hit-test region и разделение OLE overlay/status HWND.

### 6. G3 — codec evidence

- Сделать изолированные adapters для серьёзных кандидатов, не подключая их напрямую к handlers.
- Прогнать corpus и реальные camera/large images, измерить correctness, orientation, ICC, alpha, malformed data, cancellation, peak memory, speed, size, cold start, publish footprint и licensing.
- На данных выбрать codec, JPEG/WebP/PNG semantics и `ImageSafetyLimits`; оформить ADR.
- Подключить только через `GuardedImageProcessor`.

### 7. P4/P5

- P4: replacement-at-same-path, missing file, reparse/symlink, access/share failure и identity-unavailable; path fallback запрещён.
- P5: truthful queue/status/cancel/FIFO/privacy/capabilities на реальном renderer; никакого fake progress.

### 8. Production activation и E2E

- Реализовать `Program.Main` по safety startup order.
- Включить native activation только после записанного gate evidence и ADR.
- Для каждого действия доказать новый output, неизменённый hash source, collisions, cancel/failure/crash recovery и completion commands.

### 9. Release hardening

- Отдельно решить installer/portable, signing/SmartScreen, autostart, logging retention, supported Windows/filesystems, update mechanism и third-party notices.

## Главные неопределённости и риски

- Самый большой риск — не codec, а Explorer/OLE coexistence и раннее определение payload.
- `ShellFolderView.SelectedItems` может оказаться ненадёжным для Windows 11 tabs/Desktop.
- Raw interop код ещё ни разу не прошёл compiler/runtime proof.
- Тестов много, но без SDK их наличие не является evidence.
- Image corpus хорош как старт, но слишком мал для окончательных performance/resource limits.
- Release scope пока не специфицирован и не должен незаметно смешиваться с MVP implementation.

## Ближайший практический шаг

Установить .NET 8 SDK и завершить G0. До этого не имеет смысла выбирать UI framework, codec или включать `NativeActivationEnabled`: любой следующий слой будет строиться на неподтверждённой компиляции foundation.

## Навигация по документации

- Текущая истина: `PROJECT_STATE.md`.
- Продуктовый источник: `docs/canonical/SMARTDRAG_MASTER_CONTEXT.md`.
- Решения: `docs/decisions/ADR-000-index.md` и ADR-001..041.
- Что ещё не решено: `docs/OPEN_QUESTIONS.md`.
- Safety/архитектура: `docs/architecture`, `orchestration`, `runtime`, `recovery`, `artifacts`, `composition`.
- Эмпирические контракты: `docs/testing`.
- История: `docs/handoff/FOUNDATION_CHANGELOG.md` и внешний `HISTORY`; она не должна заменять current repo.

## Обновление после начала реализации — 2026-09-03

G0 теперь закрыт фактическим запуском: Windows 10.0.19045, .NET SDK 8.0.424, restore/build всех 19 проектов, 127 passed / 0 failed / 0 skipped. Единственное предупреждение — недоступный внешний NuGet advisory feed `NU1900`; compiler/analyzer warnings отсутствуют. В процессе исправлены LibraryImport/unsafe interop, stale probe ActionId references, recovery API overload, ошибки build pipeline, доверие к display extension в G2, WPF overlay и WIC preview pipeline.

G1 пока не закрыт. P0-probe был запущен и корректно остановлен, но в сессии не было пользовательских drag-событий (`probe.started` — единственная запись). Поэтому Explorer coexistence, focus, COPY/NONE, DPI и native drop по-прежнему требуют ручного evidence.
