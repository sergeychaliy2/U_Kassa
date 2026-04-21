# U_Kassa — интеграция YooKassa с Unity

Демо-проект интеграции платёжной системы **ЮKassa** в Unity-клиент по правильной схеме: Unity общается **только** со своим backend, а backend уже ходит в ЮKassa (секреты `shopId` / `secretKey` никогда не покидают сервер). Это исходная предпосылка архитектуры — всё остальное выстроено вокруг неё.

https://github.com/user-attachments/assets/78e0c7a4-4e7b-4e0e-9ddd-f2e5470472b3

---

## Оглавление

1. [Стек и состав](#стек-и-состав)
2. [Сценарий работы пользователя](#сценарий-работы-пользователя)
3. [Архитектура](#архитектура)
4. [Разбор по слоям](#разбор-по-слоям)
5. [Применённые паттерны](#применённые-паттерны)
6. [Анализ SOLID / Clean Architecture](#анализ-solid--clean-architecture)
7. [Модель безопасности](#модель-безопасности)
8. [Как запустить](#как-запустить)
9. [Честная критика и TODO](#честная-критика-и-todo)

---

## Стек и состав

| Компонент | Технология |
|---|---|
| Клиент | Unity **6000.3.4f1**, C#, IMGUI (для демо-UI), `UnityWebRequest`, `JsonUtility` |
| Backend | Node.js, Express 4, `yookassa` SDK, dotenv, CORS |
| Контракты | DTO на Unity-стороне + идентичный JSON на сервере |
| Конфигурация | `ScriptableObject` (каталог + эндпоинты) |

Структура репозитория (границы слоёв прошиты **Assembly Definitions** — нарушение направления зависимостей ловится компилятором, а не ревью):

```
UKassa/
├── Assets/
│   ├── Scenes/UKassa.unity                    — целевая сцена
│   └── Scripts/UKassaDemo/
│       ├── UKassaDemo.Presentation.asmdef     — корневая сборка презентации
│       ├── Domain/                            — ядро, noEngineReferences: true
│       │   └── UKassaDemo.Domain.asmdef
│       ├── Application/                       — ViewModel, mapper, installer
│       │   └── UKassaDemo.Application.asmdef  (ref: Domain, Payments, Config)
│       ├── Payments/                          — IPaymentGateway + 2 адаптера + PaymentError
│       │   └── UKassaDemo.Payments.asmdef     (ref: Domain)
│       ├── Config/                            — ScriptableObject-конфиги
│       │   └── UKassaDemo.Config.asmdef       (ref: Domain)
│       ├── UKassaShopDemoController.cs        — тонкий MonoBehaviour-презентер (IMGUI)
│       └── UKassaDemoBootstrap.cs             — RuntimeInitializeOnLoad-хук
└── backend/yookassa-demo-backend/             — Node.js сервер c YooKassa SDK
```

> `UKassaDemo.Domain.asmdef` помечен `"noEngineReferences": true` — Domain физически не может использовать `UnityEngine`. Это превращает правило «ядро без Unity» из документации в инвариант компиляции.

---

## Сценарий работы пользователя

1. Сцена `UKassa` стартует → [UKassaDemoBootstrap.cs](Assets/Scripts/UKassaDemo/UKassaDemoBootstrap.cs) автоматически создаёт `UKassaShopDemoController`.
2. Пользователь двигает `+/-` у товаров → `ShopDemoViewModel.ChangeQuantity` → `Cart` пересчитывает итог.
3. Клик «Оплатить» → `ShopDemoViewModel.CreatePayment` → `PaymentRequestMapper` превращает `Cart` в DTO → `IPaymentGateway.CreatePayment`.
4. `BackendPaymentGateway` делает `POST /api/payments/create` → сервер вызывает ЮKassa SDK и возвращает `confirmationUrl`.
5. Контроллер открывает `confirmationUrl` через `Application.OpenURL`.
6. Клик «Проверить статус» → `GET /api/payments/status/:paymentId` → обновление `PaymentState`.

---

## Архитектура

Проект выстроен как **Layered + Ports & Adapters (Hexagonal)** с выраженным **Composition Root**:

```
┌────────────────────────────────────────────────────────────────┐
│                     Presentation (Unity/IMGUI)                 │
│           UKassaShopDemoController  ← MonoBehaviour            │
│                                │                               │
│                                ▼                               │
│                      Application (use-cases)                   │
│      ShopDemoViewModel  •  PaymentRequestMapper  •  Events     │
│                                │                               │
│                                ▼                               │
│                    Domain (чистое C#, без Unity)               │
│           Cart • CatalogProduct • OrderId • PaymentState       │
│                                ▲                               │
│                                │ использует                    │
│                                │                               │
│                    Ports  (интерфейсы)                         │
│                       IPaymentGateway                          │
│                   ▲                      ▲                     │
│                   │                      │                     │
│          ┌────────┴─────────┐   ┌───────┴────────────┐         │
│          │ MockPaymentGateway│  │ BackendPaymentGateway│        │
│          │   (in-memory)    │   │ (UnityWebRequest)  │         │
│          └──────────────────┘   └────────────────────┘         │
│                                                 │              │
└─────────────────────────────────────────────────┼──────────────┘
                                                  ▼
                           ┌─────────────────────────────────────┐
                           │   Backend (Node.js + YooKassa SDK)  │
                           │   /api/payments/create              │
                           │   /api/payments/status/:id          │
                           └─────────────────────────────────────┘
```

Направление зависимостей — строго внутрь: `Presentation → Application → Domain`. Адаптеры (`Backend/Mock PaymentGateway`) реализуют **порт** `IPaymentGateway`, определённый рядом со слоем Application; Domain не знает о существовании сети или Unity.

---

## Разбор по слоям

### Domain — [Assets/Scripts/UKassaDemo/Domain/](Assets/Scripts/UKassaDemo/Domain/)

Чистые C#-типы, нет `using UnityEngine`:

- **[CatalogProduct.cs](Assets/Scripts/UKassaDemo/Domain/CatalogProduct.cs)** — immutable-сущность товара с валидацией в конструкторе (sku/title/price).
- **[Cart.cs](Assets/Scripts/UKassaDemo/Domain/Cart.cs)** — агрегат корзины. Инкапсулирует количества, пересчёт итога, валидацию `ChangeQuantity`. Возвращает `IReadOnlyDictionary` и `IReadOnlyList<CartLine>` — внешний код не может мутировать состояние в обход API.
- **[OrderId.cs](Assets/Scripts/UKassaDemo/Domain/OrderId.cs)** — **Value Object** как `readonly struct`. Используется как idempotence key на сервере.
- **[PaymentState.cs](Assets/Scripts/UKassaDemo/Domain/PaymentState.cs)** — enum состояний платежа (`Idle → Creating → Pending → Succeeded | Error`).

### Application — [Assets/Scripts/UKassaDemo/Application/](Assets/Scripts/UKassaDemo/Application/)

- **[ShopDemoViewModel.cs](Assets/Scripts/UKassaDemo/Application/ShopDemoViewModel.cs)** — **MVVM ViewModel**, он же use-case handler: `ChangeQuantity`, `CreatePayment`, `CheckPaymentStatus`. Внутри — мини-стейт-машина (`SetState`/`SetError`) и события `OnPaymentStateChanged`, `OnPaymentCreated`. Все зависимости инжектятся через конструктор + null-guards. Реализует **`IDisposable`**: владеет `CancellationTokenSource`, который отменяет все in-flight запросы при уничтожении презентера.
- **[ShopDemoInstaller.cs](Assets/Scripts/UKassaDemo/Application/ShopDemoInstaller.cs)** — **composition root**: статическая фабрика, собирающая объектный граф (catalog → Cart → IPaymentGateway → Mapper → ViewModel) из конфига. Вынесена из презентера — контроллер больше не «знает» про конкретные реализации.
- **[PaymentRequestMapper.cs](Assets/Scripts/UKassaDemo/Application/PaymentRequestMapper.cs)** — mapper `Cart → PaymentCreateRequest`. Изолирует трансформацию, не даёт ViewModel «знать» про форму DTO.
- **[PaymentStateChanged.cs](Assets/Scripts/UKassaDemo/Application/PaymentStateChanged.cs)** — event payload (`readonly struct`, без аллокаций).

### Payments (порт + адаптеры) — [Assets/Scripts/UKassaDemo/Payments/](Assets/Scripts/UKassaDemo/Payments/)

- **[PaymentContracts.cs](Assets/Scripts/UKassaDemo/Payments/PaymentContracts.cs)** — DTO (`PaymentCreateRequest/Response`, `PaymentStatusResponse`, `PaymentItemRequest`) и сам порт **`IPaymentGateway`**. DTO — `[Serializable]` с private-полями + read-only геттерами (чтобы `JsonUtility` умел их сериализовать). Интерфейс принимает `CancellationToken` — отмена времени жизни презентера пробрасывается в сетевой слой.
- **[PaymentError.cs](Assets/Scripts/UKassaDemo/Payments/PaymentError.cs)** — **типизированный Result для ошибок** (`PaymentErrorKind` + `Message` + опциональный `RawPayload`). Заменяет лоссовый `Action<string> onError`: вызывающий код может отличить Network от Validation, Cancelled от NotFound без парсинга строк.
- **[BackendPaymentGateway.cs](Assets/Scripts/UKassaDemo/Payments/BackendPaymentGateway.cs)** — production-адаптер: `UnityWebRequest` + корутины, timeout (20с по умолчанию), cancellation через `req.Abort()`, поддержка `X-Client-Key`, унифицированная обработка транспорт-ошибок и битых JSON-ответов. `sealed`.
- **[MockPaymentGateway.cs](Assets/Scripts/UKassaDemo/Payments/MockPaymentGateway.cs)** — in-memory stub: хранит платежи в Dictionary, через 3 секунды статус становится `succeeded`. Позволяет полностью прогнать сценарий без backend’a. `sealed`, не тянет `UnityEngine`.

### Config — [Assets/Scripts/UKassaDemo/Config/](Assets/Scripts/UKassaDemo/Config/)

- **[ShopCatalogConfig.cs](Assets/Scripts/UKassaDemo/Config/ShopCatalogConfig.cs)** — `ScriptableObject` с каталогом, `CreateAssetMenu` для создания из Editor.
- **[UKassaDemoBackendConfig.cs](Assets/Scripts/UKassaDemo/Config/UKassaDemoBackendConfig.cs)** — `ScriptableObject` с эндпоинтами, ключом, return-URL. Позволяет не хардкодить и не коммитить окружение.
- **[CatalogProductData.cs](Assets/Scripts/UKassaDemo/Config/CatalogProductData.cs)** — serializable-DTO для Inspector + конвертер `ToDomainProduct()` (чтобы Domain оставался чистым).

### Presentation — [UKassaShopDemoController.cs](Assets/Scripts/UKassaDemo/UKassaShopDemoController.cs)

Тонкий `MonoBehaviour`-презентер. Рисует IMGUI, форвардит действия в `ShopDemoViewModel`, подписан на **оба** события ViewModel (`OnPaymentCreated` → открывает `confirmationUrl`, `OnPaymentStateChanged` → пишет в лог). `BuildApplication()` теперь делегирует сборку в `ShopDemoInstaller.Install(...)`, поэтому контроллер не знает про конкретные реализации `IPaymentGateway`. В `OnDestroy` — аккуратная уборка: отписка от событий + `_viewModel.Dispose()` (он отменит in-flight HTTP-запросы через `CancellationToken`).

### Bootstrap — [UKassaDemoBootstrap.cs](Assets/Scripts/UKassaDemo/UKassaDemoBootstrap.cs)

Статический `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` — гарантирует, что контроллер появится на нужной сцене (`UKassa`) без ручной расстановки в Editor.

### Backend — [backend/yookassa-demo-backend/server.js](backend/yookassa-demo-backend/server.js)

Express-приложение: два endpoint’а, sanitize входных данных (`sanitizeOrderId` с regex + длина, `parsePositiveInt`, пересчёт `totalRub` на сервере), `orderId` → idempotence key для YooKassa SDK, опциональный middleware-чек `X-Client-Key`.

---

## Применённые паттерны

| Паттерн | Где | Комментарий |
|---|---|---|
| **Ports & Adapters / Hexagonal** | `IPaymentGateway` + `Backend/Mock` адаптеры | Ядро не знает о транспорте |
| **Strategy** | 2 реализации `IPaymentGateway` выбираются флагом | Mock ↔ Backend без изменений ViewModel |
| **MVVM** | `ShopDemoViewModel` ↔ `UKassaShopDemoController` | ViewModel без ссылок на Unity |
| **Value Object** | `OrderId` (readonly struct) | Самовалидирующийся тип-обёртка |
| **State Machine** | `PaymentState` + `ShopDemoViewModel.SetState` | Явные переходы, один источник правды для UI |
| **DTO** | `PaymentCreateRequest/Response` и др. | Граница Unity ↔ сеть, совместимо с `JsonUtility` |
| **Mapper** | `PaymentRequestMapper` | Чёткая граница Domain → DTO |
| **Composition Root** | `UKassaShopDemoController.BuildApplication()` | Единая точка сборки графа зависимостей |
| **Observer / Pub-Sub** | `OnPaymentStateChanged`, `OnPaymentCreated` | Контроллер не опрашивает ViewModel, а получает события |
| **Null Object / Test Double** | `MockPaymentGateway` | Полная прогон сценария без внешних систем |
| **Configuration via Asset** | `ScriptableObject`-конфиги | Данные отделены от кода, правятся в Editor |
| **Idempotency Key** | `OrderId` → `createPayment(_, orderId)` на сервере | Защита от двойного клика / ретраев |
| **Result / Typed Error** | `PaymentError` + `PaymentErrorKind` | Вместо `Action<string>` — классификация ошибок enum-ом |
| **Cancellation Token Pattern** | `CancellationToken` через `IPaymentGateway` | Отмена in-flight запросов по `OnDestroy` |
| **Disposable Lifetime** | `ShopDemoViewModel : IDisposable` + `CancellationTokenSource` | Единая точка закрытия ресурсов |

---

## Анализ SOLID / Clean Architecture

### SRP — Single Responsibility
- `Cart` — только состояние и пересчёт итога.
- `PaymentRequestMapper` — только трансформация.
- `BackendPaymentGateway` — только HTTP-транспорт.
- `UKassaShopDemoController` — только IMGUI-рендер и проброс команд.
- `ShopDemoViewModel` — оркестрация use-case + state.

Разделение соблюдено. Единственное, что у `UKassaShopDemoController` совмещены две роли: *presenter* и *composition root* (метод `BuildApplication`). В продовом коде composition root вынесли бы в отдельный Installer.

### OCP — Open/Closed
Добавление нового способа оплаты (например, `StripePaymentGateway`) не требует правок `ShopDemoViewModel` — достаточно реализовать `IPaymentGateway`. ✅

### LSP — Liskov
`BackendPaymentGateway` и `MockPaymentGateway` честно соблюдают контракт `IPaymentGateway`: оба гарантируют вызов ровно одного из `onSuccess` / `onError`. ✅

### ISP — Interface Segregation
`IPaymentGateway` намеренно минимален — 2 метода. Ничего лишнего клиенту интерфейса не навязано. ✅

### DIP — Dependency Inversion
Высокоуровневый код (`ShopDemoViewModel`) зависит от абстракции (`IPaymentGateway`), а не от конкретных реализаций. DI идёт через конструктор, не через сервис-локатор. ✅

### Правила Clean Architecture
- Зависимости направлены **внутрь** (Presentation → Application → Domain). ✅
- Domain не импортирует `UnityEngine`. ✅ (проверено: ни один файл из `Domain/` не содержит `using UnityEngine`).
- Транспорт (`UnityWebRequest`) запрятан за интерфейс. ✅
- Конфигурация и данные отделены от бизнес-логики (ScriptableObject). ✅

---

## Модель безопасности

**Клиент ничего секретного не хранит.** Это не декоративный пункт, а следствие нескольких решений:

1. **Секреты YooKassa живут только в `.env` на сервере** (`YOOKASSA_SHOP_ID`, `YOOKASSA_SECRET_KEY`). В бинарник Unity они не попадают в принципе.
2. **Backend не доверяет клиенту сумму.** `totalRub` пересчитывается из `items[i].unitPriceRub * quantity` на сервере. Даже если игрок пропатчит билд и пришлёт `totalRub = 1`, сервер посчитает настоящий итог.
3. **`orderId` — idempotence key.** Double-click, авторетрай сети, случайный дубль запроса → YooKassa вернёт ту же самую платёжку, а не создаст новую.
4. **Валидация на входе**: `sanitizeOrderId` (regex `^[a-zA-Z0-9_\-]+$`, длина 8–128), `parsePositiveInt`, `express.json({ limit: "256kb" })` против бомб.
5. **`X-Client-Key`** — опциональный shared secret для отсечения «левых» клиентов (не замена аутентификации, но фильтрует случайные хиты по эндпоинту).

Что сознательно **не** решено в демо: аутентификация пользователя, webhook’и YooKassa, rate-limiting, TLS (подразумевается reverse-proxy), логи аудита. Всё это — отдельные задачи прод-интеграции, см. [TODO](#честная-критика-и-todo).

---

## Как запустить

### Mock-режим (без backend)
1. Открыть сцену `Assets/Scenes/UKassa.unity`.
2. В `UKassaDemoBackendConfig` снять флаг `Use Backend Gateway` (или не создавать конфиг — тогда используются значения по умолчанию).
3. Play. Оплата эмулируется локально, через 3 секунды статус становится `succeeded`.

### Backend-режим (реальная ЮKassa)
```bash
cd backend/yookassa-demo-backend
npm i
cp .env.example .env   # заполнить YOOKASSA_SHOP_ID и YOOKASSA_SECRET_KEY из sandbox
npm start              # http://localhost:3000
```
В Unity: включить `Use Backend Gateway`, проверить, что эндпоинты совпадают с сервером. Запустить сцену, нажать «Оплатить» — откроется страница ЮKassa.

---

## Что уже сделано и что ещё остаётся

### ✅ Исправлено в этом проекте

- **Assembly Definitions по слоям** — 5 `.asmdef` прошивают направление зависимостей. `UKassaDemo.Domain` имеет `"noEngineReferences": true` — `using UnityEngine` в Domain теперь ошибка компиляции.
- **Composition Root вынесен** в [ShopDemoInstaller](Assets/Scripts/UKassaDemo/Application/ShopDemoInstaller.cs). Презентер больше не знает про `MockPaymentGateway` / `BackendPaymentGateway`.
- **Типизированные ошибки** — `Action<string> onError` → `Action<PaymentError> onError` с enum `PaymentErrorKind` (Network / InvalidResponse / Validation / Cancelled / EmptyCart / NotFound / Timeout / Unknown).
- **`CancellationToken` в `IPaymentGateway`**, привязан к lifetime ViewModel через `IDisposable`. На `OnDestroy` вызывается `Dispose`, CTS отменяется, корутина внутри `BackendPaymentGateway` зовёт `UnityWebRequest.Abort()` — ни один callback не стреляет в «мёртвый» VM.
- **Timeout на `UnityWebRequest`** — 20 секунд по умолчанию, параметром в конструкторе.
- **Отписка от событий** в `OnDestroy` (`OnPaymentCreated`, `OnPaymentStateChanged`).
- **Презентер подписан на `OnPaymentStateChanged`** — событийная модель, а не только pull.
- **`#pragma region` → `#region`** во всех файлах. `#pragma region` — C/C++-директива, в C# компилятор её игнорирует (и в Rider/VS она не сворачивает блоки).
- **Адаптеры помечены `sealed`** (`MockPaymentGateway`, `BackendPaymentGateway`) — явная закрытость расширения через наследование, только через реализацию `IPaymentGateway`.
- **`MockPaymentGateway` больше не импортирует `UnityEngine`** — чистый C#, работает и в unit-тестах без Unity Play Mode.

### ⏳ Осталось для продакшена

- [ ] Перейти с callback-API на **async/UniTask** — короче код и структурированные исключения. Сейчас callback-API, но с типизированной ошибкой и `CancellationToken` — что закрывает 80% боли.
- [ ] Деньги как `int` рублей. В проде — `long` в копейках (или `decimal`), форматирование только на границе JSON.
- [ ] Автополлинг статуса платежа с backoff вместо ручного нажатия кнопки.
- [ ] `BackendClientKey` в клиентском билде — это **обфускация**, а не безопасность. В проде — короткоживущий токен после аутентификации пользователя.
- [ ] **Webhook** от ЮKassa на backend (`payment.succeeded`) + персистентность заказов (SQLite/Postgres). Polling недопустим как единственный источник правды.
- [ ] **Rate-limiting** и структурированные логи на backend.
- [ ] Переписать IMGUI на **UI Toolkit / uGUI** с reactive data-binding на `OnPaymentStateChanged`. Сейчас событие есть и презентер подписан (логирует), но рендер всё ещё pull в `OnGUI` — это естественно для immediate-mode IMGUI, но не для прод-UI.

---

## Лицензия / Атрибуция

Демо-код. YooKassa SDK — npm-пакет `yookassa`. Unity 6.
