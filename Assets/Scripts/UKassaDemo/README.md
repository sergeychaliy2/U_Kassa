# UKassa Demo (Unity)

В сцене `UKassa` автоматически создается демо-магазин товаров с `+/-` количеством, итогом и кнопкой оплаты.

## Что уже реализовано

- Runtime UI на стандартных Unity UI (`Canvas`, `Text`, `Button`).
- Каталог товаров + корзина.
- Платежный слой через интерфейс `IPaymentGateway`.
- `MockPaymentGateway` для тестов без backend.
- `BackendPaymentGateway` для безопасной интеграции с ЮKassa через ваш сервер.
- Кнопка "Проверить статус последнего платежа" (через backend).

## Безопасность (важно)

- Секреты ЮKassa (`shopId`, `secretKey`) НЕ хранятся в клиенте Unity.
- Unity-клиент вызывает только ваш backend endpoint.
- Backend уже создает платеж в ЮKassa и возвращает клиенту `confirmationUrl`.

## Как подключить реальную оплату

1. Поднимите backend endpoint `POST /api/payments/create` и `GET /api/payments/status/:paymentId`.
2. На backend вызывайте SDK/REST YooKassa в режиме тестового магазина.
3. Верните JSON (ответ create):

```json
{
  "paymentId": "2a5b...",
  "confirmationUrl": "https://yoomoney.ru/checkout/payments/v2/contract?orderId=...",
  "status": "pending"
}
```

4. В инспекторе `UKassaShopDemoController`:
   - включите `Use Backend Gateway`
   - задайте `Backend Create Payment Endpoint` и `Backend Get Payment Status Endpoint Template`
   - при необходимости задайте `Backend Client Key`

## Локальный backend в репозитории

В проекте есть готовый пример backend:
`backend/yookassa-demo-backend`

Он использует npm-пакет `yookassa` и поднимается на `http://localhost:3000`.

Чтобы запустить:
1) перейдите в `backend/yookassa-demo-backend`
2) выполните `npm i`
3) создайте `.env` по примеру `.env.example` и заполните `YOOKASSA_SHOP_ID` и `YOOKASSA_SECRET_KEY`
4) запустите `npm start`

После этого Unity сможет создать платеж и по кнопке проверить его статус.

## Про SDK YooKassa для Unity

Если у вас отдельный Unity SDK-обертка, подключайте его на стороне backend или через отдельный server-side сервис. Прямой вызов ЮKassa из Unity клиента с секретом недопустим.
