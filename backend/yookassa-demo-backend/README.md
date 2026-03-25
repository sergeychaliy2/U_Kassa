# YooKassa Demo Backend (Node.js)

Этот backend нужен для безопасной интеграции с YooKassa:
Unity-клиент **не хранит** `secretKey`, он только вызывает ваш сервер.

## Быстрый старт (локально)

1. Перейдите в папку:
   `backend/yookassa-demo-backend`
2. Установите зависимости:
   `npm i`
3. Создайте `.env` из `.env.example` и заполните:
   - `YOOKASSA_SHOP_ID`
   - `YOOKASSA_SECRET_KEY`
   (лучше взять **тестовые** креды из sandbox YooKassa)
4. Запустите:
   `npm start`

Сервер поднимается на `http://localhost:3000`.

## Endpoints

- `POST /api/payments/create`
  - принимает: `orderId`, `returnUrl`, `items[]` (Unity-формат)
  - возвращает: `paymentId`, `confirmationUrl`, `status`
- `GET /api/payments/status/:paymentId`
  - возвращает `paymentId` и `status`

## Безопасность (что уже сделано)

- `totalRub` и состав корзины пересчитываются на сервере (не доверяем Unity).
- `orderId` валидируется и используется как idempotence key (защита от двойного клика).
- Опциональная защита `BACKEND_CLIENT_KEY` + проверка заголовка `X-Client-Key`.
