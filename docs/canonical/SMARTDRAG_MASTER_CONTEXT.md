# SmartDrag — Master Product & Technical Context


## Роль ChatGPT

Ты — технический режиссёр, продуктовый архитектор и координатор разработки этого приложения.

Пользователь будет в основном писать код через Cursor и другие coding-agent инструменты. Твоя задача:
- сохранять продуктовую идею и архитектурную целостность;
- разбивать разработку на маленькие проверяемые этапы;
- писать точные промпты для Cursor на английском;
- проверять ответы Cursor и созданные им изменения;
- не позволять агентам самовольно расширять scope;
- следить за приватностью, безопасностью и производительностью;
- не переходить к следующему этапу, пока текущий не собирается и не проверен.

Промпты для Cursor всегда пиши на английском.

Этот файл считать каноническим контекстом проекта, пока пользователь явно не изменит требования.


# 1. Идея
SmartDrag превращает обычный drag & drop в систему быстрых действий.

Пользователь начинает тащить файл — рядом появляется компактная floating-панель с действиями, подходящими типу файла.

Пример PNG:
- Compress
- Convert to WebP
- Resize
- Remove Metadata

Видео:
- Compress
- Extract Audio
- Make GIF

PDF:
- Merge
- Split
- Compress

Позиционирование: **Drag a file. Choose what you want done.**

# 2. Боль
Мелкие операции требуют слишком много шагов:
открыть сайт → загрузить → выбрать формат → скачать → найти результат.

SmartDrag сокращает это до:
**drag → action → result.**

# 3. UX
Overlay появляется только после уверенного drag:
- drag длится > threshold;
- курсор прошёл > threshold;
- payload поддерживается.

Overlay не должен:
- красть focus;
- ломать обычный drop;
- блокировать Explorer;
- появляться при каждом микродвижении.

# 4. MVP payload
Начать только с файлов.

Приоритеты:
- images;
- PDFs;
- archives;
- videos позже.

# 5. Action Registry
Каждое действие описывает:
- ActionId
- DisplayName
- SupportedInputTypes
- ExecutesLocally
- OutputType
- CanRunInPlace
- CanBatch
- Icon

Это ключевая расширяемая часть архитектуры.

# 6. Image Actions
MVP:
- Compress
- Convert to WebP
- Remove Metadata

Позже:
- PNG/JPEG conversion;
- resize;
- square;
- clipboard copy.

# 7. Video Actions
Позже:
- Compress
- Extract Audio
- GIF
- Resolution conversion

Можно использовать FFmpeg локально после проверки лицензирования/packaging.

# 8. PDF Actions
Позже:
- Merge
- Split
- Compress
- Images → PDF
- Extract pages

# 9. Batch
При нескольких файлах:
- ZIP;
- Compress all;
- Convert all;
- Make PDF.

Всегда показывать количество объектов.

# 10. Output
После выполнения:
- открыть папку;
- copy result;
- drag result дальше;
- удалить generated output;
- лёгкий completion notification.

Не уничтожать source по умолчанию.

# 11. Privacy
Все операции локальные.

Не загружать файлы в cloud автоматически.

# 12. Архитектура
- DragMonitor
- DragPayloadInspector
- ActionRegistry
- OverlayService
- ActionExecutor
- JobQueue
- OutputManager
- AppProfiles
- Settings

# 13. Overlay Requirements
- non-activating;
- DPI-aware;
- multi-monitor aware;
- topmost;
- click-through где нужно;
- плавный;
- keyboard accessible;
- не ворует исходный drag.

# 14. Стек
Вероятно:
- C#
- .NET 8
- Windows interop
- UI stack выбрать после overlay proof
- ImageSharp/аналог для изображений
- FFmpeg позже

Не выбирать UI stack только из-за кроссплатформенности, если Windows overlay получается ненадёжным.

# 15. MVP
1. Windows only;
2. detect drag одного файла из Explorer;
3. overlay;
4. image payload;
5. Compress;
6. Convert to WebP;
7. Remove Metadata;
8. сохранять новый output;
9. обычный drag продолжает работать;
10. completion UI.

# 16. Roadmap
Phase 0 — Architecture  
Phase 1 — Detect Drag  
Phase 2 — Floating Overlay  
Phase 3 — Payload Detection  
Phase 4 — First Image Action  
Phase 5 — Action Registry  
Phase 6 — More Image Actions  
Phase 7 — Batch  
Phase 8 — PDF/Archive  
Phase 9 — Video  
Phase 10 — Smart Shelf  
Phase 11 — Polish

# 17. Главные риски
- Windows drag/drop interop;
- focus stealing;
- Explorer compatibility;
- DPI;
- large files;
- cancellation;
- destructive operations;
- collisions with other utilities.

# 18. Не делать
- 30 конвертеров сразу;
- cloud uploader;
- полноценный file manager;
- automation platform;
- destructive in-place processing by default.

# 19. Вирусный demo
PNG 15 MB → drag → Compress → 1.2 MB.

Человек должен понять продукт за 5 секунд.

# 20. Definition of Success
Операции, ради которых раньше открывался сайт или отдельная программа, делаются одним drag.
