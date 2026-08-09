# Castlevania2DPrototype

Прототип 2D-игры в духе Castlevania / metroidvania на Unity.

## Версия Unity

**Unity 6000.0.78f1** (Unity 6)

Все участники команды должны открывать проект **только этой версией**. Иначе возможны расхождения в `ProjectSettings` и YAML-сценах.

---

## Совместная разработка (Git + GitHub)

Проект настроен под Git:

- `.gitignore` — не коммитит `Library/`, `Temp/`, `Logs/`, `UserSettings/` и прочий локальный мусор
- `.gitattributes` — текстовые YAML-ассеты Unity + binary для картинок/аудио
- локальный merge-драйвер `unityyamlmerge` (Smart Merge) — если установлен Unity 6000.0.78f1

### 1. Что нужно установить каждому

1. **Git** — https://git-scm.com/download/win  
2. **Unity Hub** + редактор **6000.0.78f1**  
3. Аккаунт на **GitHub** (или GitLab)  
4. По желанию: [GitHub Desktop](https://desktop.github.com/) или работа из терминала / Cursor

### 2. Первый раз: владелец репозитория (ты)

В корне проекта (уже есть `git init` на ветке `main`):

```bash
git add .
git status
git commit -m "Initial commit: Castlevania 2D prototype"
```

Создай пустой репозиторий на GitHub (без README), затем:

```bash
git remote add origin https://github.com/<USER>/<REPO>.git
git push -u origin main
```

Пригласи напарника: GitHub → Settings → Collaborators.

### 3. Первый раз: второй разработчик

```bash
git clone https://github.com/<USER>/<REPO>.git
```

В Unity Hub: **Add** → укажи **корень** клонированной папки (где `Assets/`, `Packages/`, `ProjectSettings/`).  
Первый импорт создаст локальную `Library/` — её в git нет, это нормально.

### 4. Ежедневный цикл

```bash
# Перед работой
git checkout main
git pull

# Своя ветка под задачу
git checkout -b feature/my-task

# ... правки в Unity / Cursor ...

git add .
git status
git commit -m "Describe why the change exists"
git push -u origin feature/my-task
```

На GitHub: **Pull Request** → review → merge в `main`.  
После мержа у всех: `git checkout main && git pull`.

### 5. Правила команды (важно для Unity)

| Делать | Не делать |
|--------|-----------|
| Коммитить `Assets/**` вместе с `*.meta` | Коммитить `Library/`, `Temp/`, `Logs/` |
| Один человек правит одну сцену в один момент | Параллельно править `Prototype.unity` вдвоём |
| Выносить контент в префабы / отдельные сцены | Сидеть в `main` и пушить всё подряд |
| Одна Unity-версия у всей команды | Открывать проект другой версией редактора |
| Перед push — Play Mode smoke-test | Резолвить конфликт сцены «на глаз» без проверки в Editor |

**Сцены** (`*.unity`) и большие префабы плохо мержатся. Если оба правили одну сцену — договоритесь, чья версия база, второй переносит свои правки вручную.

### 6. Smart Merge (опционально)

Если конфликты YAML частые, проверь в `.git/config` секцию `merge.unityyamlmerge`  
(драйвер указывает на `UnityYAMLMerge.exe` из установки 6000.0.78f1).  
При конфликте Git вызовет Smart Merge; если не справится — откроется обычный merge tool.

### 7. Альтернатива: Unity Version Control (Plastic)

Если нужен встроенный VCS Unity (удобно для больших бинарных ассетов):  
Unity → **Window → Unity Version Control** / Unity Cloud.  
Для этой команды по умолчанию рекомендуется **Git + GitHub** (проще с Cursor и PR).

---

## Как открыть проект

1. Клонируйте репозиторий.
2. Установите Unity Hub и редактор **6000.0.78f1**.
3. Hub → **Open** / **Add** → корневая папка репозитория.
4. Дождитесь первого импорта.

Не открывайте вложенную папку `Assets` — только корень проекта.

## Что коммитить / что не коммитить

| Коммитить | Не коммитить |
|-----------|----------------|
| `Assets/` (+ все `*.meta`) | `Library/` |
| `Packages/` | `Temp/`, `Obj/` |
| `ProjectSettings/` | `Logs/`, `Build/`, `Builds/` |
| `.gitignore`, `.gitattributes`, `README.md` | `UserSettings/` |
| | `.vs/`, `.idea/`, `*.csproj`, `*.sln` |

Файлы `.meta` **обязательны** в git — без них ломаются ссылки на ассеты.

## Структура (кратко)

- `Assets/Scripts/` — gameplay-код (Player, Enemies, Combat, Movement, …)
- `Assets/BossSystems/` — системы босса Flower
- `Assets/Scenes/` — сцены (`Prototype.unity`)
- `Assets/Art/`, `Assets/Prefabs/`, `Assets/Animations/` — контент
- `Packages/`, `ProjectSettings/` — зависимости и настройки проекта
