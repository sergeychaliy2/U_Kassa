/* eslint-disable no-console */
const express = require("express");
const cors = require("cors");
const dotenv = require("dotenv");
const YooKassa = require("yookassa");

dotenv.config();

const app = express();
app.use(cors());
app.use(express.json({ limit: "256kb" }));

const PORT = Number(process.env.PORT || 3000);
const SHOP_ID = process.env.YOOKASSA_SHOP_ID;
const SECRET_KEY = process.env.YOOKASSA_SECRET_KEY;
const EXPECTED_CLIENT_KEY = process.env.BACKEND_CLIENT_KEY || "";

const yooKassa = new YooKassa({
  shopId: SHOP_ID,
  secretKey: SECRET_KEY,
});

function jsonError(res, status, message) {
  res.status(status).json({ error: message });
}

function assertClientKey(req, res) {
  if (!EXPECTED_CLIENT_KEY) return true; // client key not configured

  const provided = req.header("X-Client-Key") || "";
  if (!provided || provided !== EXPECTED_CLIENT_KEY) {
    jsonError(res, 403, "Forbidden: invalid X-Client-Key");
    return false;
  }
  return true;
}

function sanitizeOrderId(orderId) {
  // Keep it strict: orderId used as idempotenceKey in YooKassa SDK.
  if (typeof orderId !== "string") return null;
  const trimmed = orderId.trim();
  if (trimmed.length < 8 || trimmed.length > 128) return null;
  if (!/^[a-zA-Z0-9_\-]+$/.test(trimmed)) return null;
  return trimmed;
}

function parsePositiveInt(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return null;
  if (!Number.isInteger(n)) return null;
  if (n <= 0) return null;
  return n;
}

function parseNonNegativeInt(value) {
  const n = Number(value);
  if (!Number.isFinite(n)) return null;
  if (!Number.isInteger(n)) return null;
  if (n < 0) return null;
  return n;
}

function parsePositiveRubInt(value) {
  // Unity sends unitPriceRub as integer rubles (no kopecks).
  return parsePositiveInt(value);
}

function toMoneyValueRubTotal(totalRub) {
  // YooKassa expects amount.value as a decimal string with 2 digits.
  // Here we treat totalRub as integer rubles.
  return Number(totalRub).toFixed(2);
}

app.post("/api/payments/create", async (req, res) => {
  try {
    if (!assertClientKey(req, res)) return;

    const body = req.body || {};
    const orderId = sanitizeOrderId(body.orderId);
    const returnUrl = typeof body.returnUrl === "string" ? body.returnUrl.trim() : "";
    const items = Array.isArray(body.items) ? body.items : [];

    if (!orderId) return jsonError(res, 400, "Invalid orderId");
    if (!returnUrl) return jsonError(res, 400, "Invalid returnUrl");
    if (items.length === 0) return jsonError(res, 400, "Cart is empty");

    // Validate and compute server-side totals (do NOT trust totalRub from client).
    let totalRub = 0;
    const validatedItems = [];

    for (const it of items) {
      const sku = typeof it.sku === "string" ? it.sku.trim() : "";
      const title = typeof it.title === "string" ? it.title.trim() : "";
      const quantity = parseNonNegativeInt(it.quantity);
      const unitPriceRub = parsePositiveRubInt(it.unitPriceRub);

      if (!sku || !title) return jsonError(res, 400, "Invalid item sku/title");
      if (quantity === null || quantity === 0) continue; // ignore zero items
      if (unitPriceRub === null) return jsonError(res, 400, "Invalid unitPriceRub");

      validatedItems.push({ sku, title, quantity, unitPriceRub });
      totalRub += quantity * unitPriceRub;
    }

    if (validatedItems.length === 0 || totalRub <= 0) {
      return jsonError(res, 400, "Cart total is invalid");
    }

    const payload = {
      amount: {
        value: toMoneyValueRubTotal(totalRub),
        currency: "RUB",
      },
      // One-stage payment: YooKassa captures funds automatically.
      capture: true,
      description: `Заказ: ${orderId}`,
      payment_method_data: {
        type: "bank_card",
      },
      confirmation: {
        type: "redirect",
        return_url: returnUrl,
      },
      metadata: {
        order_id: orderId,
      },
    };

    // Use orderId as idempotence key: protects against double-click / retries.
    const payment = await yooKassa.createPayment(payload, orderId);

    res.json({
      paymentId: payment.id,
      confirmationUrl: payment.confirmationUrl,
      status: payment.status,
    });
  } catch (e) {
    console.error(e);
    return jsonError(res, 500, "Backend error creating payment");
  }
});

app.get("/api/payments/status/:paymentId", async (req, res) => {
  try {
    if (!assertClientKey(req, res)) return;

    const paymentId = req.params.paymentId;
    if (!paymentId || typeof paymentId !== "string") {
      return jsonError(res, 400, "Invalid paymentId");
    }

    const payment = await yooKassa.getPayment(paymentId);
    res.json({
      paymentId: payment.id,
      status: payment.status,
    });
  } catch (e) {
    console.error(e);
    return jsonError(res, 500, "Backend error getting payment status");
  }
});

// Simple return page for redirect confirmation.
app.get("/return", (req, res) => {
  const orderId = req.query && req.query.orderId ? req.query.orderId : "";
  res.setHeader("Content-Type", "text/html; charset=utf-8");
  res.send(`<!doctype html><html><body><h3>Оплата завершена (демо)</h3><div>orderId=${encodeURIComponent(orderId)}</div><p>Если вы хотите узнать статус из Unity — нажмите кнопку "Проверить статус последнего платежа".</p></body></html>`);
});

app.listen(PORT, () => {
  console.log(`yookassa-demo-backend listening on http://localhost:${PORT}`);
  if (!SHOP_ID || !SECRET_KEY) {
    console.warn("WARNING: YOOKASSA_SHOP_ID / YOOKASSA_SECRET_KEY are not set.");
  }
});

