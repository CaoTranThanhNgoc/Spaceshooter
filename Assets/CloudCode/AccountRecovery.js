// Space Hawk - account recovery and account-data cleanup (Unity Cloud Code, JavaScript script).
//
// Why this runs on the server: Unity Authentication has no password recovery of its own. Unity's
// documentation says recovery "must be implemented as a custom server-authoritative flow" using the
// Player Authentication Admin API (change-password), with a service account, never from the game
// client. Removing a player's scores from the Leaderboards likewise needs the Leaderboards Admin API.
//
// The game client calls this one script with an `action`:
//   sendVerification - send a 6-digit code to an e-mail address the player wants to attach     (signed-in player, e.g. a guest registering)
//   verifyContact    - check that code: the player has now proven the contact is theirs        (same player)
//   setContact       - attach the VERIFIED contact to the calling player's account. If another
//                      account held it, it moves here (only its real owner could verify it)     (same player)
//   requestReset     - send a 6-digit code to the contact registered for `username`            (any signed-in player, e.g. a guest)
//   confirmReset     - check the code and set a new password via the Admin API                 (any signed-in player)
//   deleteData       - purge the calling player's leaderboard scores, recovery data
//                      and Cloud Save items; the client then deletes the account itself        (signed-in player)
//   selfCheck        - which secrets / channels are configured (booleans only, never values)    (signed-in player)
// It always answers { ok: true } or { ok: false, error: "<code>" }; the client maps codes to texts.
//
// ---- Setup (once) -----------------------------------------------------------------------------
// The one-command helper is Tools/CloudCodeSetup/Setup-CloudCode.ps1 (it deploys this file and prepares the
// secret values); the steps it cannot do for you need your own Unity and Google logins:
// 1. Unity Dashboard (cloud.unity.com) > Service accounts: create a service account for this project and give it
//    the project roles  Cloud Code Script Editor, Cloud Code Script Publisher, Cloud Code Script Viewer,
//    Unity Environments Admin  (to deploy)  and  Authentication Admin, Leaderboards Admin  (what this script
//    calls at runtime: player_auth.password.update and live_ops.leaderboards.scores.delete). Create a key and
//    keep its Key ID + Secret. One account can do both jobs; a second, narrower one (only the last two roles)
//    for the runtime secret is tidier.
// 2. Unity Dashboard > your project > Secrets > Add secret - service access "Cloud Code" for each:
//      UGS_ADMIN_AUTH      base64("KEY_ID:SECRET_KEY") of the runtime service account            (required)
//      RECOVERY_PEPPER     any long random string - mixed into the stored code hashes             (required)
//      MAIL_RELAY_URL      web app URL of Tools/MailRelay/Code.gs - a Google Apps Script that sends the codes   (required)
//      MAIL_RELAY_KEY      from YOUR Gmail (no domain needed) - and the shared key in its RELAY_KEY property
//    (Secret Manager has no CLI: secrets are added in the Dashboard.)
// 3. Deploy this file as the Cloud Code script named "AccountRecovery" (the helper does it with `ugs deploy`, or use
//    Unity's Deployment window).
// 4. Check it: Play Mode > Tools > Space Hawk > Check Account Server (calls the "selfCheck" action below).
//
// Storage: Cloud Save PRIVATE custom data (readable/writable by this server code only):
//   rc-<hash of contact>          { playerId, username, kind, verified, reset: { codeHash, expiresAt, sentAt, attempts } | null }
//   rp-<playerId>                 the id of that player's contact record (so deleting the account finds it)
//   rv-<hash of contact+player>   a pending verification: { codeHash, expiresAt, sentAt, attempts, verifiedAt }
//   rl-<hash of contact>          send limiter: { windowStart, count }  (at most 6 messages per contact per hour)

const axios = require("axios-1.6");
const { DataApi } = require("@unity-services/cloud-save-1.4");

const CODE_TTL_MS = 10 * 60 * 1000;
const RESEND_COOLDOWN_MS = 60 * 1000;
const MAX_ATTEMPTS = 5;
const VERIFIED_TTL_MS = 15 * 60 * 1000;     // how long a verified contact may be attached to an account
const SEND_WINDOW_MS = 60 * 60 * 1000;
const MAX_SENDS_PER_WINDOW = 6;
const ADMIN_BASE = "https://services.api.unity.com";

// ------------------------------------------------------------------------------------ helpers

// SHA-256 (the Cloud Code runtime has no crypto module). Returns lower-case hex.
function sha256(message) {
  const K = [
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
    0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
    0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
    0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
    0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
    0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
  ];
  const rotr = (x, n) => (x >>> n) | (x << (32 - n));

  // UTF-8 bytes
  const bytes = [];
  const text = unescape(encodeURIComponent(String(message)));
  for (let i = 0; i < text.length; i++) bytes.push(text.charCodeAt(i));
  const bitLength = bytes.length * 8;
  bytes.push(0x80);
  while (bytes.length % 64 !== 56) bytes.push(0);
  const high = Math.floor(bitLength / 0x100000000);
  const low = bitLength >>> 0;
  for (let i = 3; i >= 0; i--) bytes.push((high >>> (i * 8)) & 0xff);
  for (let i = 3; i >= 0; i--) bytes.push((low >>> (i * 8)) & 0xff);

  const h = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
  const w = new Array(64);
  for (let offset = 0; offset < bytes.length; offset += 64) {
    for (let i = 0; i < 16; i++) {
      const j = offset + i * 4;
      w[i] = (bytes[j] << 24) | (bytes[j + 1] << 16) | (bytes[j + 2] << 8) | bytes[j + 3];
    }
    for (let i = 16; i < 64; i++) {
      const s0 = rotr(w[i - 15], 7) ^ rotr(w[i - 15], 18) ^ (w[i - 15] >>> 3);
      const s1 = rotr(w[i - 2], 17) ^ rotr(w[i - 2], 19) ^ (w[i - 2] >>> 10);
      w[i] = (w[i - 16] + s0 + w[i - 7] + s1) | 0;
    }
    let [a, b, c, d, e, f, g, hh] = h;
    for (let i = 0; i < 64; i++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (hh + S1 + ch + K[i] + w[i]) | 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) | 0;
      hh = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
    }
    h[0] = (h[0] + a) | 0; h[1] = (h[1] + b) | 0; h[2] = (h[2] + c) | 0; h[3] = (h[3] + d) | 0;
    h[4] = (h[4] + e) | 0; h[5] = (h[5] + f) | 0; h[6] = (h[6] + g) | 0; h[7] = (h[7] + hh) | 0;
  }
  return h.map((x) => ("00000000" + (x >>> 0).toString(16)).slice(-8)).join("");
}

// The client normalizes before sending; the server re-checks so it never trusts the client's word.
// Only e-mail addresses are accepted (phone numbers are not supported).
function classifyContact(contact) {
  const value = String(contact || "").trim();
  if (/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(value) && value.length <= 120) return { kind: "email", value: value.toLowerCase() };
  return null;
}

function isStrongPassword(password) {
  const p = String(password || "");
  return p.length >= 8 && p.length <= 30 && /[A-Z]/.test(p) && /[a-z]/.test(p) && /[0-9]/.test(p) && /[^A-Za-z0-9]/.test(p);
}

const contactRecordId = (contact) => "rc-" + sha256("contact:" + contact).slice(0, 32);
const playerPointerId = (playerId) => "rp-" + playerId;
const verificationId = (contactId, playerId) => "rv-" + sha256("verify:" + contactId + ":" + playerId).slice(0, 32);
const limiterId = (contactId) => "rl-" + contactId.slice(3);

async function optionalSecret(secretManager, name) {
  try {
    const secret = await secretManager.getSecret(name);
    return secret && secret.value ? secret.value : null;
  } catch (err) {
    return null;
  }
}

// ----------------------------------------------------------------------------- private storage

function storage(context) {
  const api = new DataApi(context);

  async function read(customId, key) {
    const res = await api.getPrivateCustomItems(context.projectId, customId, [key]);
    const items = (res.data && res.data.results) || [];
    return items.length > 0 ? items[0].value : null;
  }
  async function write(customId, key, value) {
    await api.setPrivateCustomItem(context.projectId, customId, { key, value });
  }
  async function remove(customId) {
    try {
      await api.deletePrivateCustomItems(context.projectId, customId);
    } catch (err) {
      if (!(err.response && err.response.status === 404)) throw err;
    }
  }

  return {
    async getRecord(contactId) {
      const raw = await read(contactId, "record");
      return raw ? (typeof raw === "string" ? JSON.parse(raw) : raw) : null;
    },
    saveRecord: (contactId, record) => write(contactId, "record", JSON.stringify(record)),
    async getVerification(contactId, playerId) {
      const raw = await read(verificationId(contactId, playerId), "state");
      return raw ? (typeof raw === "string" ? JSON.parse(raw) : raw) : null;
    },
    saveVerification: (contactId, playerId, state) => write(verificationId(contactId, playerId), "state", JSON.stringify(state)),
    removeVerification: (contactId, playerId) => remove(verificationId(contactId, playerId)),
    async getLimiter(contactId) {
      const raw = await read(limiterId(contactId), "window");
      return raw ? (typeof raw === "string" ? JSON.parse(raw) : raw) : null;
    },
    saveLimiter: (contactId, window) => write(limiterId(contactId), "window", JSON.stringify(window)),
    getPointer: (playerId) => read(playerPointerId(playerId), "contactId"),
    savePointer: (playerId, contactId) => write(playerPointerId(playerId), "contactId", contactId),
    removeContact: remove,
    removePointer: (playerId) => remove(playerPointerId(playerId)),
    removePlayerItems: async () => {
      try {
        await api.deleteItems(context.projectId, context.playerId);
      } catch (err) {
        if (!(err.response && err.response.status === 404)) throw err;
      }
    },
  };
}

// ----------------------------------------------------------------------------------- messages

// purpose: "reset" (a forgotten password) or "verify" (proving a contact is yours)
async function sendCode(secretManager, logger, contact, code, purpose) {
  const verify = purpose === "verify";
  const subject = verify ? "Space Hawk - your verification code" : "Space Hawk - your password reset code";
  const text = verify
    ? "Your Space Hawk verification code is " + code + ". It expires in 10 minutes.\n" +
      "Mã xác minh Space Hawk của bạn là " + code + ". Mã hết hạn sau 10 phút.\n" +
      "If you did not ask for this, ignore this message. / Nếu bạn không yêu cầu, hãy bỏ qua tin nhắn này."
    : "Your Space Hawk password reset code is " + code + ". It expires in 10 minutes.\n" +
      "Mã đặt lại mật khẩu Space Hawk của bạn là " + code + ". Mã hết hạn sau 10 phút.\n" +
      "If you did not ask for this, ignore this message. / Nếu bạn không yêu cầu, hãy bỏ qua tin nhắn này.";

  // The Gmail relay: a small Google Apps Script web app that mails from the owner's own account.
  const relayUrl = await optionalSecret(secretManager, "MAIL_RELAY_URL");
  const relayKey = await optionalSecret(secretManager, "MAIL_RELAY_KEY");
  if (!relayUrl || !relayKey) return "server_not_configured";
  const res = await axios.post(relayUrl, { key: relayKey, to: contact.value, subject, body: text }, {
    headers: { "content-type": "application/json" },
    maxRedirects: 5,
  });
  if (!res.data || res.data.ok !== true) throw new Error("the mail relay refused the message");
  return null;
}

function adminHeaders(auth) {
  return { headers: { Authorization: "Basic " + auth, "Content-Type": "application/json" } };
}

// The Cloud Code log keeps only the message text (not the attributes), so the HTTP status and the service's
// answer go into the text itself. Never contains secrets: Unity's error bodies only describe the problem.
function why(err) {
  const status = err && err.response ? err.response.status : "no response";
  let detail = err && err.message ? err.message : "";
  try {
    if (err && err.response && err.response.data !== undefined) detail = JSON.stringify(err.response.data);
  } catch (e) { /* keep the message */ }
  return status + " " + String(detail).slice(0, 300);
}

// ------------------------------------------------------------------------------------ actions

// At most MAX_SENDS_PER_WINDOW messages per contact per hour, whoever asks: nobody can use this to flood an inbox.
async function allowSend(store, contactId, now) {
  let window = await store.getLimiter(contactId);
  if (!window || now - window.windowStart >= SEND_WINDOW_MS) window = { windowStart: now, count: 0 };
  if (window.count >= MAX_SENDS_PER_WINDOW) return false;
  window.count += 1;
  await store.saveLimiter(contactId, window);
  return true;
}

function newCode() {
  return String(Math.floor(Math.random() * 1000000)).padStart(6, "0");
}

async function sendVerification(store, context, params, secretManager, logger) {
  const contact = classifyContact(params.contact);
  if (!contact) return { ok: false, error: "invalid_contact" };

  const contactId = contactRecordId(contact.value);
  const now = Date.now();
  const previous = await store.getVerification(contactId, context.playerId);
  if (previous && now - previous.sentAt < RESEND_COOLDOWN_MS) {
    return { ok: false, error: "too_soon", retryAfter: Math.ceil((RESEND_COOLDOWN_MS - (now - previous.sentAt)) / 1000) };
  }

  const pepper = await optionalSecret(secretManager, "RECOVERY_PEPPER");
  if (!pepper) return { ok: false, error: "server_not_configured" };
  if (!(await allowSend(store, contactId, now))) return { ok: false, error: "rate_limited" };

  const code = newCode();
  await store.saveVerification(contactId, context.playerId, {
    codeHash: sha256(code + ":" + pepper + ":" + context.playerId + ":" + contactId),
    expiresAt: now + CODE_TTL_MS,
    sentAt: now,
    attempts: 0,
    verifiedAt: 0,
  });

  try {
    const problem = await sendCode(secretManager, logger, contact, code, "verify");
    if (problem) {
      await store.removeVerification(contactId, context.playerId);
      return { ok: false, error: problem };
    }
  } catch (err) {
    logger.error("Could not send the verification code: " + why(err), { "error.message": err.message });
    await store.removeVerification(contactId, context.playerId);
    return { ok: false, error: "send_failed" };
  }
  return { ok: true };
}

async function verifyContact(store, context, params, secretManager) {
  const contact = classifyContact(params.contact);
  if (!contact) return { ok: false, error: "invalid_contact" };

  const contactId = contactRecordId(contact.value);
  const state = await store.getVerification(contactId, context.playerId);
  if (!state || !state.codeHash) return { ok: false, error: "invalid_code" };

  const now = Date.now();
  if (now > state.expiresAt) {
    await store.removeVerification(contactId, context.playerId);
    return { ok: false, error: "code_expired" };
  }
  if (state.attempts >= MAX_ATTEMPTS) {
    await store.removeVerification(contactId, context.playerId);
    return { ok: false, error: "too_many_attempts" };
  }

  const pepper = await optionalSecret(secretManager, "RECOVERY_PEPPER");
  if (!pepper) return { ok: false, error: "server_not_configured" };

  const given = sha256(String(params.code || "").trim() + ":" + pepper + ":" + context.playerId + ":" + contactId);
  if (given !== state.codeHash) {
    state.attempts += 1;
    await store.saveVerification(contactId, context.playerId, state);
    return { ok: false, error: state.attempts >= MAX_ATTEMPTS ? "too_many_attempts" : "invalid_code" };
  }

  state.codeHash = null;       // a code works once
  state.verifiedAt = now;
  await store.saveVerification(contactId, context.playerId, state);
  return { ok: true };
}

async function setContact(store, context, params) {
  const contact = classifyContact(params.contact);
  if (!contact) return { ok: false, error: "invalid_contact" };

  const contactId = contactRecordId(contact.value);
  const now = Date.now();
  const state = await store.getVerification(contactId, context.playerId);
  if (!state || !state.verifiedAt || now - state.verifiedAt > VERIFIED_TTL_MS) return { ok: false, error: "not_verified" };

  // The contact is proven to be this player's. If another account held it, it moves here: only
  // the person who can read this inbox could have got this far.
  let transferred = false;
  const existing = await store.getRecord(contactId);
  if (existing && existing.playerId !== context.playerId) {
    const otherPointer = await store.getPointer(existing.playerId);
    if (otherPointer === contactId) await store.removePointer(existing.playerId);
    transferred = true;
  }

  // A player has one contact: drop the previous record when it changes.
  const previousId = await store.getPointer(context.playerId);
  if (previousId && previousId !== contactId) await store.removeContact(previousId);

  await store.saveRecord(contactId, {
    playerId: context.playerId,
    username: String(params.username || "").trim().toLowerCase(),
    kind: contact.kind,
    verified: true,
    reset: null,
  });
  await store.savePointer(context.playerId, contactId);
  await store.removeVerification(contactId, context.playerId);
  return { ok: true, transferred };
}

async function requestReset(store, context, params, secretManager, logger) {
  const contact = classifyContact(params.contact);
  if (!contact) return { ok: false, error: "invalid_contact" };

  const contactId = contactRecordId(contact.value);
  const record = await store.getRecord(contactId);
  const username = String(params.username || "").trim().toLowerCase();

  // Never reveal whether an account exists: a wrong username/contact pair looks like success.
  if (!record || !record.verified || !username || record.username !== username) {
    logger.info("Password reset requested for an unknown username/contact pair.");
    return { ok: true };
  }

  const now = Date.now();
  if (record.reset && now - record.reset.sentAt < RESEND_COOLDOWN_MS) {
    return { ok: false, error: "too_soon", retryAfter: Math.ceil((RESEND_COOLDOWN_MS - (now - record.reset.sentAt)) / 1000) };
  }

  const pepper = await optionalSecret(secretManager, "RECOVERY_PEPPER");
  if (!pepper) return { ok: false, error: "server_not_configured" };
  if (!(await allowSend(store, contactId, now))) return { ok: false, error: "rate_limited" };

  const code = newCode();
  record.reset = { codeHash: sha256(code + ":" + pepper + ":" + record.playerId), expiresAt: now + CODE_TTL_MS, sentAt: now, attempts: 0 };
  await store.saveRecord(contactId, record);

  try {
    const problem = await sendCode(secretManager, logger, contact, code, "reset");
    if (problem) {
      record.reset = null;
      await store.saveRecord(contactId, record);
      return { ok: false, error: problem };
    }
  } catch (err) {
    logger.error("Could not send the reset code: " + why(err), { "error.message": err.message });
    record.reset = null;
    await store.saveRecord(contactId, record);
    return { ok: false, error: "send_failed" };
  }
  return { ok: true };
}

async function confirmReset(store, context, params, secretManager, logger) {
  const contact = classifyContact(params.contact);
  if (!contact) return { ok: false, error: "invalid_contact" };
  if (!isStrongPassword(params.newPassword)) return { ok: false, error: "weak_password" };

  const contactId = contactRecordId(contact.value);
  const record = await store.getRecord(contactId);
  const username = String(params.username || "").trim().toLowerCase();
  if (!record || !username || record.username !== username || !record.reset) return { ok: false, error: "invalid_code" };

  const now = Date.now();
  if (now > record.reset.expiresAt) {
    record.reset = null;
    await store.saveRecord(contactId, record);
    return { ok: false, error: "code_expired" };
  }
  if (record.reset.attempts >= MAX_ATTEMPTS) {
    record.reset = null;
    await store.saveRecord(contactId, record);
    return { ok: false, error: "too_many_attempts" };
  }

  const pepper = await optionalSecret(secretManager, "RECOVERY_PEPPER");
  const adminAuth = await optionalSecret(secretManager, "UGS_ADMIN_AUTH");
  if (!pepper || !adminAuth) return { ok: false, error: "server_not_configured" };

  const given = sha256(String(params.code || "").trim() + ":" + pepper + ":" + record.playerId);
  if (given !== record.reset.codeHash) {
    record.reset.attempts += 1;
    await store.saveRecord(contactId, record);
    return { ok: false, error: record.reset.attempts >= MAX_ATTEMPTS ? "too_many_attempts" : "invalid_code" };
  }

  try {
    await axios.post(
      ADMIN_BASE + "/player-identity/v1/projects/" + context.projectId + "/users/" + record.playerId + "/change-password",
      { newPassword: params.newPassword },
      adminHeaders(adminAuth)
    );
  } catch (err) {
    logger.error("Admin change-password failed: " + why(err), { "error.message": err.message });
    return { ok: false, error: "reset_failed" };
  }

  record.reset = null; // a code works once
  await store.saveRecord(contactId, record);
  return { ok: true };
}

// Does Unity accept the stored admin credentials? One harmless read (list the leaderboards); only the HTTP
// status comes back - never the credential. 0 = not tried / no answer.
async function probeAdminAuth(context, secretManager) {
  const adminAuth = await optionalSecret(secretManager, "UGS_ADMIN_AUTH");
  if (!adminAuth) return 0;
  try {
    const res = await axios.get(
      ADMIN_BASE + "/leaderboards/v1/projects/" + context.projectId + "/environments/" + context.environmentId + "/leaderboards",
      adminHeaders(adminAuth)
    );
    return res.status;
  } catch (err) {
    return err && err.response ? err.response.status : 0;
  }
}

async function selfCheck(context, secretManager) {
  const has = async (name) => (await optionalSecret(secretManager, name)) !== null;
  const adminAuthStatus = await probeAdminAuth(context, secretManager);
  return {
    ok: true,
    adminAuth: await has("UGS_ADMIN_AUTH"),
    adminAuthWorks: adminAuthStatus >= 200 && adminAuthStatus < 300,
    adminAuthStatus,
    pepper: await has("RECOVERY_PEPPER"),
    email: (await has("MAIL_RELAY_URL")) && (await has("MAIL_RELAY_KEY")),
  };
}

async function deleteData(store, context, secretManager, logger) {
  const adminAuth = await optionalSecret(secretManager, "UGS_ADMIN_AUTH");
  if (!adminAuth) return { ok: false, error: "server_not_configured" };

  // 1. Every live leaderboard: the player disappears from the ranking.
  try {
    await axios.delete(
      ADMIN_BASE + "/leaderboards/v1/projects/" + context.projectId + "/environments/" + context.environmentId +
        "/leaderboards/scores/players/" + context.playerId + "/purge",
      adminHeaders(adminAuth)
    );
  } catch (err) {
    if (!(err.response && err.response.status === 404)) {
      logger.error("Leaderboard purge failed: " + why(err), { "error.message": err.message });
      return { ok: false, error: "purge_failed" };
    }
  }

  // 2. Recovery contact + pointer, 3. the account's Cloud Save progress.
  try {
    const contactId = await store.getPointer(context.playerId);
    if (contactId) {
      await store.removeContact(contactId);
      await store.removeVerification(contactId, context.playerId);
    }
    await store.removePointer(context.playerId);
    await store.removePlayerItems();
  } catch (err) {
    logger.error("Could not remove the stored account data: " + why(err), { "error.message": err.message });
    return { ok: false, error: "cleanup_failed" };
  }
  return { ok: true };
}

// ------------------------------------------------------------------------------------- entry

module.exports = async ({ params, context, logger, secretManager }) => {
  const store = storage(context);
  switch (params.action) {
    case "sendVerification": return sendVerification(store, context, params, secretManager, logger);
    case "verifyContact": return verifyContact(store, context, params, secretManager);
    case "setContact": return setContact(store, context, params);
    case "requestReset": return requestReset(store, context, params, secretManager, logger);
    case "confirmReset": return confirmReset(store, context, params, secretManager, logger);
    case "deleteData": return deleteData(store, context, secretManager, logger);
    case "selfCheck": return selfCheck(context, secretManager);
    default: return { ok: false, error: "unknown_action" };
  }
};

module.exports.params = {
  action: { type: "String", required: true },
  username: "String",
  contact: "String",
  code: "String",
  newPassword: "String",
};
