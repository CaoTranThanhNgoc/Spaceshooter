// Local test harness for Assets/CloudCode/AccountRecovery.js - no Unity, no network.
// Run:  node Tools/CloudCodeTests/run.js
//
// The script is loaded with fakes for the two Unity modules it requires ("axios-1.6" and the Cloud Save SDK):
//   - a Cloud Save fake that keeps private custom items in memory and refuses what the real one would refuse
//   - an axios fake that records every outgoing HTTP call (the Gmail mail relay / Unity admin APIs) and can be told to fail
// This proves the logic of the flow (codes, expiry, attempts, uniqueness, admin calls) - NOT that the
// live Unity services accept the requests; that needs a real deployment.

const fs = require("fs");
const path = require("path");
const vm = require("vm");
const assert = require("assert");

const scriptPath = path.join(__dirname, "..", "..", "Assets", "CloudCode", "AccountRecovery.js");
const source = fs.readFileSync(scriptPath, "utf8");

function loadScript(env) {
  const module = { exports: {} };
  const fakeRequire = (name) => {
    if (name === "axios-1.6") return env.axios;
    if (name === "@unity-services/cloud-save-1.4") return { DataApi: env.DataApi };
    throw new Error("Unexpected require: " + name);
  };
  const wrapped = source + "\n;module.exports.__internals = { sha256, classifyContact, isStrongPassword, cleanDisplayName, nameKey };";
  vm.runInNewContext(wrapped, { module, exports: module.exports, require: fakeRequire, console, Date: env.Date }, { filename: scriptPath });
  return module.exports;
}

const env_relayAnswer = { value: { ok: true } };
const env_adminProbe = { status: 200 };   // what Unity answers when selfCheck tries the admin credentials

function makeEnv() {
  const store = new Map();            // customId -> Map(key -> value)
  const playerItems = new Set();      // players whose default items were deleted
  const calls = [];                   // recorded HTTP calls
  const failures = new Set();         // url fragments that must fail
  const clock = { now: 1700000000000 };

  class DataApi {
    constructor(context) { this.context = context; }
    async getPrivateCustomItems(projectId, customId, keys) {
      const items = store.get(customId);
      const results = [];
      if (items) for (const k of keys || []) if (items.has(k)) results.push({ key: k, value: items.get(k) });
      return { data: { results } };
    }
    async setPrivateCustomItem(projectId, customId, body) {
      if (!store.has(customId)) store.set(customId, new Map());
      store.get(customId).set(body.key, body.value);
      return { data: {} };
    }
    async deletePrivateCustomItems(projectId, customId) {
      if (!store.has(customId)) { const e = new Error("404"); e.response = { status: 404 }; throw e; }
      store.delete(customId);
    }
    async deleteItems(projectId, playerId) { playerItems.add(playerId); }
  }

  const axios = {
    async post(url, body, config) {
      calls.push({ method: "POST", url, body: typeof body === "string" ? body : JSON.parse(JSON.stringify(body)), config });
      for (const f of failures) if (url.includes(f)) { const e = new Error("boom"); e.response = { status: 500 }; throw e; }
      if (url.includes("script.google.com")) return { status: 200, data: env_relayAnswer.value };
      return { status: 200, data: {} };
    },
    async get(url, config) {
      calls.push({ method: "GET", url, config });
      if (env_adminProbe.status >= 400) { const e = new Error("rejected"); e.response = { status: env_adminProbe.status, data: {} }; throw e; }
      return { status: env_adminProbe.status, data: {} };
    },
    async delete(url, config) {
      calls.push({ method: "DELETE", url, config });
      for (const f of failures) if (url.includes(f)) { const e = new Error("boom"); e.response = { status: 500 }; throw e; }
      return { status: 204 };
    },
  };

  const RealDate = Date;
  const FakeDate = class extends RealDate {
    static now() { return clock.now; }
  };

  return { store, playerItems, calls, failures, clock, DataApi, axios, Date: FakeDate };
}

const SECRETS = {
  UGS_ADMIN_AUTH: "QUJDOkRFRg==",
  RECOVERY_PEPPER: "pepper-for-tests",
  MAIL_RELAY_URL: "https://script.google.com/macros/s/AKfycbTEST/exec",
  MAIL_RELAY_KEY: "relay-key",
};

function makeRunner(env, secrets) {
  const script = loadScript(env);
  const logs = [];
  const logger = { info: (m) => logs.push(["info", m]), error: (m) => logs.push(["error", m]) };
  const secretManager = {
    async getSecret(name) {
      if (!(name in secrets)) throw new Error("no secret " + name);
      return { value: secrets[name] };
    },
  };
  return {
    script, logs,
    // JSON round-trip: the script runs in its own realm, so its objects have another Object.prototype
    call: async (playerId, params) => JSON.parse(JSON.stringify(await script({ params, logger, secretManager, context: { projectId: "proj", environmentId: "env", playerId } }))),
  };
}

let passed = 0;
async function test(name, fn) {
  try { await fn(); passed++; console.log("  ok   " + name); }
  catch (e) { console.log("  FAIL " + name + "\n       " + (e.stack || e)); process.exitCode = 1; }
}

(async () => {
  // ------------------------------------------------------------------ helpers
  await test("sha256 matches the known test vectors", () => {
    const { sha256 } = loadScript(makeEnv()).__internals;
    assert.strictEqual(sha256(""), "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    assert.strictEqual(sha256("abc"), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    assert.strictEqual(sha256("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"), "248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1");
    assert.strictEqual(sha256("a".repeat(1000)), "41edece42d63e8d9bf515a9ba6932e1c20cbc9f5a5d134645adb5db1b9737ea3");
    assert.strictEqual(sha256("tiếng Việt"), require("crypto").createHash("sha256").update("tiếng Việt", "utf8").digest("hex"));
  });

  await test("contact classification and password strength follow the rules", () => {
    const { classifyContact, isStrongPassword } = loadScript(makeEnv()).__internals;
    const plain = (x) => JSON.parse(JSON.stringify(x));
    assert.deepStrictEqual(plain(classifyContact(" Pilot@Example.COM ")), { kind: "email", value: "pilot@example.com" });
    assert.strictEqual(classifyContact("+84912345678"), null, "phone numbers are not supported");
    assert.strictEqual(classifyContact("0912345678"), null);
    assert.strictEqual(classifyContact("not a contact"), null);
    assert.strictEqual(isStrongPassword("Abcdef1!"), true);
    assert.strictEqual(isStrongPassword("Abcdefg1"), false, "needs a symbol");
    assert.strictEqual(isStrongPassword("abcdef1!"), false);
    assert.strictEqual(isStrongPassword("Ab1!"), false);
  });

  // ------------------------------------------------------------------ contact verification
  const lastCode = (env) => {
    const msg = env.calls.filter((c) => c.url.includes("script.google.com")).pop();
    if (!msg) return null;
    return /code is (\d{6})/.exec(msg.body.body)[1];
  };
  const wrongCode = (code) => (code === "000000" ? "111111" : "000000");

  // Gets a contact attached to a player the way the game does: send a code, verify it, set it.
  async function attach(env, r, playerId, contact, username) {
    assert.deepStrictEqual(await r.call(playerId, { action: "sendVerification", contact }), { ok: true });
    assert.deepStrictEqual(await r.call(playerId, { action: "verifyContact", contact, code: lastCode(env) }), { ok: true });
    return r.call(playerId, { action: "setContact", contact, username });
  }
  async function registered(env, r, contact = "pilot@x.io") {
    const res = await attach(env, r, "P1", contact, "Pilot_One");
    assert.strictEqual(res.ok, true);
    env.calls.length = 0;                       // the tests below look at what happens AFTER registration
    env.clock.now += 61000;                     // ...and are not held back by the send cooldown
  }

  await test("a contact is verified by a code that was sent to it, then attached", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.deepStrictEqual(await r.call("P1", { action: "sendVerification", contact: "A@B.co" }), { ok: true });
    const mail = env.calls.find((c) => c.url.includes("script.google.com"));
    assert.strictEqual(mail.body.to, "a@b.co");
    assert.strictEqual(mail.body.key, "relay-key");
    assert.match(mail.body.subject, /verification/);
    assert.deepStrictEqual(await r.call("P1", { action: "verifyContact", contact: "a@b.co", code: lastCode(env) }), { ok: true });
    assert.deepStrictEqual(await r.call("P1", { action: "setContact", contact: "a@b.co", username: "Pilot_One" }), { ok: true, transferred: false });
  });

  await test("setContact without a verified code is refused", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await r.call("P1", { action: "setContact", contact: "a@b.co", username: "x" })).error, "not_verified");
    await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    assert.strictEqual((await r.call("P1", { action: "setContact", contact: "a@b.co", username: "x" })).error, "not_verified", "a code that was only sent is not enough");
  });

  await test("a verification belongs to the player who asked for it", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    const code = lastCode(env);
    assert.strictEqual((await r.call("P2", { action: "verifyContact", contact: "a@b.co", code })).error, "invalid_code");
    assert.strictEqual((await r.call("P2", { action: "setContact", contact: "a@b.co", username: "x" })).error, "not_verified");
  });

  await test("wrong verification codes are counted and five lock it", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    const code = lastCode(env);
    for (let i = 0; i < 4; i++) {
      assert.strictEqual((await r.call("P1", { action: "verifyContact", contact: "a@b.co", code: wrongCode(code) })).error, "invalid_code");
    }
    assert.strictEqual((await r.call("P1", { action: "verifyContact", contact: "a@b.co", code: wrongCode(code) })).error, "too_many_attempts");
    assert.strictEqual((await r.call("P1", { action: "verifyContact", contact: "a@b.co", code })).error, "too_many_attempts", "even the right code is refused once locked");
  });

  await test("a verification code expires, and so does a verified-but-unused contact", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    const code = lastCode(env);
    env.clock.now += 10 * 60 * 1000 + 1;
    assert.strictEqual((await r.call("P1", { action: "verifyContact", contact: "a@b.co", code })).error, "code_expired");

    env.clock.now += 61000;
    await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    await r.call("P1", { action: "verifyContact", contact: "a@b.co", code: lastCode(env) });
    env.clock.now += 15 * 60 * 1000 + 1;
    assert.strictEqual((await r.call("P1", { action: "setContact", contact: "a@b.co", username: "x" })).error, "not_verified");
  });

  await test("a verification works once", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await attach(env, r, "P1", "a@b.co", "pilot");
    assert.strictEqual((await r.call("P1", { action: "setContact", contact: "a@b.co", username: "pilot" })).error, "not_verified");
  });

  await test("asking again too soon is refused, and an inbox cannot be flooded", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.deepStrictEqual(await r.call("P1", { action: "sendVerification", contact: "a@b.co" }), { ok: true });
    const soon = await r.call("P1", { action: "sendVerification", contact: "a@b.co" });
    assert.strictEqual(soon.error, "too_soon");
    assert.ok(soon.retryAfter > 0 && soon.retryAfter <= 60);

    // Different players, each respecting its own cooldown: the contact still only gets 6 messages an hour.
    for (let i = 2; i <= 6; i++) assert.deepStrictEqual(await r.call("Q" + i, { action: "sendVerification", contact: "a@b.co" }), { ok: true });
    assert.strictEqual((await r.call("Q7", { action: "sendVerification", contact: "a@b.co" })).error, "rate_limited");
    assert.strictEqual(env.calls.filter((c) => c.url.includes("script.google.com")).length, 6);

    env.clock.now += 60 * 60 * 1000 + 1;
    assert.deepStrictEqual(await r.call("Q7", { action: "sendVerification", contact: "a@b.co" }), { ok: true }, "a new hour, a new allowance");
  });

  await test("a failing mail provider reports send_failed and leaves nothing pending", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    env.failures.add("script.google.com");
    assert.strictEqual((await r.call("P1", { action: "sendVerification", contact: "a@b.co" })).error, "send_failed");
    env.failures.clear();
    assert.deepStrictEqual(await r.call("P1", { action: "sendVerification", contact: "a@b.co" }), { ok: true }, "no cooldown after a failed send");
  });

  await test("codes are mailed through the Gmail relay (no domain needed)", async () => {
    env_relayAnswer.value = { ok: true };
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.deepStrictEqual(await r.call("P1", { action: "sendVerification", contact: "Me@Gmail.com" }), { ok: true });
    const call = env.calls.find((c) => c.url.includes("script.google.com"));
    assert.ok(call, "the relay was called");
    assert.strictEqual(call.body.key, "relay-key");
    assert.strictEqual(call.body.to, "me@gmail.com");
    assert.match(call.body.subject, /verification/);
    assert.match(call.body.body, /code is \d{6}/);
    assert.deepStrictEqual(await r.call("P1", { action: "verifyContact", contact: "me@gmail.com", code: lastCode(env) }), { ok: true });
  });

  await test("a refusing relay is reported as send_failed", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.deepStrictEqual(await r.call("P1", { action: "sendVerification", contact: "a@b.co" }), { ok: true });
    env_relayAnswer.value = { ok: false, error: "forbidden" };
    env.clock.now += 61000;
    assert.strictEqual((await r.call("P2", { action: "sendVerification", contact: "c@d.co" })).error, "send_failed");
    env_relayAnswer.value = "<html>sign in</html>";           // e.g. the web app is not public
    assert.strictEqual((await r.call("P3", { action: "sendVerification", contact: "e@f.co" })).error, "send_failed");
    env_relayAnswer.value = { ok: true };
  });

  await test("phone numbers are refused: only e-mail addresses are supported", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    for (const action of ["sendVerification", "verifyContact", "setContact"]) {
      assert.strictEqual((await r.call("P1", { action, contact: "+84912345678", code: "123456" })).error, "invalid_contact");
    }
    assert.strictEqual(env.calls.length, 0, "nothing was sent");
  });

  await test("invalid contacts are refused everywhere", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    for (const action of ["sendVerification", "verifyContact", "setContact"]) {
      assert.strictEqual((await r.call("P1", { action, contact: "garbage", code: "123456" })).error, "invalid_contact");
    }
    assert.strictEqual(env.calls.length, 0);
  });

  await test("the real owner can take a contact over from an older account", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await attach(env, r, "OLD", "me@x.io", "old_name")).transferred, false);
    env.clock.now += 61000;
    const moved = await attach(env, r, "NEW", "me@x.io", "new_name");
    assert.deepStrictEqual(moved, { ok: true, transferred: true });

    // the reset now belongs to the new account only
    env.clock.now += 61000; env.calls.length = 0;
    assert.deepStrictEqual(await r.call("G", { action: "requestReset", username: "old_name", contact: "me@x.io" }), { ok: true });
    assert.strictEqual(env.calls.length, 0, "the old username no longer matches");
    await r.call("G", { action: "requestReset", username: "new_name", contact: "me@x.io" });
    const code = lastCode(env);
    await r.call("G", { action: "confirmReset", username: "new_name", contact: "me@x.io", code, newPassword: "Brand-new1!" });
    assert.ok(env.calls.find((c) => c.url.endsWith("/users/NEW/change-password")), "the password of the NEW account was reset");
    assert.ok(!env.calls.find((c) => c.url.endsWith("/users/OLD/change-password")));
  });

  await test("a stranger cannot take a contact over without receiving its code", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await attach(env, r, "OWNER", "me@x.io", "owner");
    env.clock.now += 61000;
    await r.call("EVIL", { action: "sendVerification", contact: "me@x.io" });      // the code goes to the owner's inbox
    assert.strictEqual((await r.call("EVIL", { action: "verifyContact", contact: "me@x.io", code: "123456" })).error, "invalid_code");
    assert.strictEqual((await r.call("EVIL", { action: "setContact", contact: "me@x.io", username: "evil" })).error, "not_verified");
    // the owner still has it
    env.calls.length = 0; env.clock.now += 61000;
    await r.call("G", { action: "requestReset", username: "owner", contact: "me@x.io" });
    assert.ok(lastCode(env), "the reset still reaches the real owner");
  });

  await test("changing the contact drops the old one", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    await attach(env, r, "P1", "old@x.io", "pilot");
    env.clock.now += 61000;
    await attach(env, r, "P1", "new@x.io", "pilot");
    env.clock.now += 61000; env.calls.length = 0;
    await r.call("G", { action: "requestReset", username: "pilot", contact: "old@x.io" });
    assert.strictEqual(env.calls.length, 0, "the old contact no longer resets anything");
    await r.call("G", { action: "requestReset", username: "pilot", contact: "new@x.io" });
    assert.ok(lastCode(env));
  });

  // ------------------------------------------------------------------ reset
  const sentCode = lastCode;

  await test("requestReset e-mails a 6 digit code through the relay", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    assert.deepStrictEqual(await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "PILOT@x.io" }), { ok: true });
    const mail = env.calls.find((c) => c.url.includes("script.google.com"));
    assert.ok(mail, "a mail request was made");
    assert.strictEqual(mail.body.key, "relay-key");
    assert.strictEqual(mail.body.to, "pilot@x.io");
    assert.match(mail.body.subject, /reset/);
    assert.match(mail.body.body, /code is \d{6}/);
  });

  await test("requestReset with a phone number is refused", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    assert.strictEqual((await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "+84912345678" })).error, "invalid_contact");
    assert.strictEqual(env.calls.length, 0, "nothing was sent");
  });

  await test("an unknown username/contact pair looks like success but sends nothing", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    assert.deepStrictEqual(await r.call("GUEST", { action: "requestReset", username: "somebody_else", contact: "pilot@x.io" }), { ok: true });
    assert.deepStrictEqual(await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "nobody@x.io" }), { ok: true });
    assert.strictEqual(env.calls.length, 0);
  });

  await test("a second request within a minute is refused, then allowed", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const again = await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    assert.strictEqual(again.error, "too_soon");
    assert.ok(again.retryAfter > 0 && again.retryAfter <= 60);
    env.clock.now += 61000;
    assert.deepStrictEqual(await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" }), { ok: true });
  });

  await test("the right code changes the password through the admin API, once", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const code = sentCode(env);
    const res = await r.call("GUEST", { action: "confirmReset", username: "Pilot_One", contact: "pilot@x.io", code, newPassword: "Brand-new1" });
    assert.deepStrictEqual(res, { ok: true });
    const change = env.calls.find((c) => c.url.endsWith("/change-password"));
    assert.strictEqual(change.url, "https://services.api.unity.com/player-identity/v1/projects/proj/users/P1/change-password");
    assert.deepStrictEqual(change.body, { newPassword: "Brand-new1" });
    assert.strictEqual(change.config.headers.Authorization, "Basic QUJDOkRFRg==");

    const again = await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Another-one2" });
    assert.strictEqual(again.error, "invalid_code", "a code works once");
  });

  await test("a wrong code is counted, and five wrong ones lock the code", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const code = sentCode(env);
    const wrong = code === "000000" ? "111111" : "000000";
    for (let i = 0; i < 4; i++) {
      assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code: wrong, newPassword: "Brand-new1" })).error, "invalid_code");
    }
    assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code: wrong, newPassword: "Brand-new1" })).error, "too_many_attempts");
    // even the right code no longer works - a new one has to be requested
    assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" })).error, "too_many_attempts");
    assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" })).error, "invalid_code", "the lock clears the code");
    assert.ok(!env.calls.some((c) => c.url.endsWith("/change-password")), "the password was never changed");
  });

  await test("a code expires after ten minutes", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const code = sentCode(env);
    env.clock.now += 10 * 60 * 1000 + 1;
    assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" })).error, "code_expired");
  });

  await test("a weak new password is refused before anything else", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const res = await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code: sentCode(env), newPassword: "weakpass" });
    assert.strictEqual(res.error, "weak_password");
  });

  await test("a failing mail provider reports send_failed and leaves no live code", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    env.failures.add("script.google.com");
    assert.strictEqual((await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" })).error, "send_failed");
    env.failures.clear();
    assert.deepStrictEqual(await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" }), { ok: true }, "no cooldown after a failed send");
  });

  await test("a missing mail channel or admin credential is reported, not hidden", async () => {
    const env = makeEnv();
    const noMail = Object.assign({}, SECRETS);
    const r = makeRunner(env, noMail); await registered(env, r);
    delete noMail.MAIL_RELAY_URL;                     // the secret goes missing after the account exists
    assert.strictEqual((await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" })).error, "server_not_configured");
    assert.strictEqual((await r.call("P5", { action: "sendVerification", contact: "other@x.io" })).error, "server_not_configured");

    const env2 = makeEnv();
    const noAdmin = Object.assign({}, SECRETS);
    const r2 = makeRunner(env2, noAdmin); await registered(env2, r2);
    await r2.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const code = sentCode(env2);
    delete noAdmin.UGS_ADMIN_AUTH;                    // ...and so does the admin credential
    assert.strictEqual((await r2.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" })).error, "server_not_configured");
  });

  await test("a failing admin call reports reset_failed and keeps the code usable", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    await r.call("GUEST", { action: "requestReset", username: "pilot_one", contact: "pilot@x.io" });
    const code = sentCode(env);
    env.failures.add("change-password");
    assert.strictEqual((await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" })).error, "reset_failed");
    env.failures.clear();
    assert.deepStrictEqual(await r.call("GUEST", { action: "confirmReset", username: "pilot_one", contact: "pilot@x.io", code, newPassword: "Brand-new1" }), { ok: true });
  });

  // ------------------------------------------------------------------ delete
  await test("deleteData purges the leaderboards, the recovery records and the Cloud Save items", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS); await registered(env, r);
    const res = await r.call("P1", { action: "deleteData" });
    assert.deepStrictEqual(res, { ok: true });
    const purge = env.calls.find((c) => c.method === "DELETE");
    assert.strictEqual(purge.url, "https://services.api.unity.com/leaderboards/v1/projects/proj/environments/env/leaderboards/scores/players/P1/purge");
    assert.strictEqual(purge.config.headers.Authorization, "Basic QUJDOkRFRg==");
    assert.ok(env.playerItems.has("P1"));
    assert.deepStrictEqual([...env.store.keys()].filter((k) => !k.startsWith("rl-")), [], "no recovery record is left behind (only the anonymous send counter stays)");
    env.clock.now += 61000;
    assert.strictEqual((await attach(env, r, "P2", "pilot@x.io", "someone_else")).transferred, false, "the contact is free again - nothing to take over");
  });

  await test("deleteData for an account without a contact still works, and a failed purge aborts", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.deepStrictEqual(await r.call("P9", { action: "deleteData" }), { ok: true });

    const env2 = makeEnv(); const r2 = makeRunner(env2, SECRETS); await registered(env2, r2);
    env2.failures.add("/purge");
    assert.strictEqual((await r2.call("P1", { action: "deleteData" })).error, "purge_failed");
    assert.ok(!env2.playerItems.has("P1"), "nothing else was deleted when the purge failed");
  });

  await test("selfCheck reports what is configured, never the values", async () => {
    const full = makeRunner(makeEnv(), SECRETS);
    assert.deepStrictEqual(await full.call("P1", { action: "selfCheck" }), { ok: true, names: true, adminAuth: true, adminAuthWorks: true, adminAuthStatus: 200, pepper: true, email: true });

    const noKey = Object.assign({}, SECRETS); delete noKey.MAIL_RELAY_KEY;
    assert.strictEqual((await makeRunner(makeEnv(), noKey).call("P1", { action: "selfCheck" })).email, false, "the relay needs both its URL and its key");

    const partial = Object.assign({}, SECRETS); delete partial.UGS_ADMIN_AUTH;
    const r = makeRunner(makeEnv(), partial);
    const res = await r.call("P1", { action: "selfCheck" });
    assert.deepStrictEqual(res, { ok: true, names: true, adminAuth: false, adminAuthWorks: false, adminAuthStatus: 0, pepper: true, email: true });
    assert.ok(!JSON.stringify(res).includes("pepper-for-tests"), "no secret value leaks");
    assert.deepStrictEqual(await makeRunner(makeEnv(), {}).call("P1", { action: "selfCheck" }), { ok: true, names: true, adminAuth: false, adminAuthWorks: false, adminAuthStatus: 0, pepper: false, email: false });
  });

  await test("selfCheck tells a stored-but-rejected admin credential apart from a working one", async () => {
    env_adminProbe.status = 401;
    try {
      const env = makeEnv(); const r = makeRunner(env, SECRETS);
      const res = await r.call("P1", { action: "selfCheck" });
      assert.strictEqual(res.adminAuth, true);
      assert.strictEqual(res.adminAuthWorks, false);
      assert.strictEqual(res.adminAuthStatus, 401);
      assert.ok(!JSON.stringify(res).includes(SECRETS.UGS_ADMIN_AUTH), "the credential never comes back");
      const probe = env.calls.find((c) => c.method === "GET");
      assert.ok(probe && probe.url.includes("/leaderboards"), "a harmless leaderboards read was used as the probe");
    } finally { env_adminProbe.status = 200; }
  });

  // ------------------------------------------------------------------ display names
  const WEEK_MS = 7 * 24 * 60 * 60 * 1000;
  const DAY_MS = 24 * 60 * 60 * 1000;
  const setName = (r, player, name, auto) => r.call(player, { action: "setName", displayName: name, auto: auto === true ? "true" : "false" });

  await test("display names: what is valid, and which names count as the same", () => {
    const { cleanDisplayName, nameKey } = loadScript(makeEnv()).__internals;
    assert.strictEqual(cleanDisplayName("  Pilot_One "), "Pilot_One", "the ends are trimmed");
    assert.strictEqual(cleanDisplayName("Ng\u1ecdc"), "Ng\u1ecdc", "accents are fine");
    for (const bad of ["", "ab", "a".repeat(17), "no@symbols", "two words", "emoji\u{1F600}x", "<script>"]) assert.strictEqual(cleanDisplayName(bad), null, JSON.stringify(bad));
    assert.strictEqual(nameKey("Ng\u1ecdc"), "ngoc");
    assert.strictEqual(nameKey("N.g-o_c"), "ngoc");
    assert.strictEqual(nameKey("\u0110\u1ea1t Pro"), "datpro", "d with a stroke is a d");
    assert.strictEqual(nameKey("NGOC"), nameKey("ngoc"));
    assert.notStrictEqual(nameKey("ngoc1"), nameKey("ngoc"));
  });

  await test("setName: a free name is claimed, bad names are refused", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    const next = Math.floor((env.clock.now + WEEK_MS) / 1000);
    assert.deepStrictEqual(await setName(r, "P1", "Pilot_Hawk"), { ok: true, name: "Pilot_Hawk", nextChangeAt: next });
    for (const bad of ["ab", "x".repeat(17), "bad@name", "  ", "two words"]) assert.strictEqual((await setName(r, "P2", bad)).error, "invalid_name", bad);
    assert.strictEqual((await setName(r, "P2", "...")).error, "invalid_name", "a name needs letters or digits");
    assert.deepStrictEqual(await r.call("P1", { action: "getName" }), { ok: true, name: "Pilot_Hawk", nextChangeAt: next });
    assert.deepStrictEqual(await r.call("NOBODY", { action: "getName" }), { ok: true, name: "", nextChangeAt: 0 });
  });

  await test("setName: nobody else can take a name that differs only in case, accents or punctuation", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await setName(r, "P1", "Ng\u1ecdc")).ok, true);
    for (const same of ["ngoc", "NGOC", "N.g-o.c", "Ng\u1ecdc "]) assert.strictEqual((await setName(r, "P2", same)).error, "name_taken", same);
    assert.strictEqual((await setName(r, "P2", "Ngoc_2")).ok, true, "a different name is fine");
    assert.strictEqual((await setName(r, "P1", "Ng\u1ecdc")).ok, true, "the owner keeps it");
  });

  await test("setName: one change per week, the first manual change is free", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    const auto = await setName(r, "P1", "Pilot1a2b", true);
    assert.deepStrictEqual(auto, { ok: true, name: "Pilot1a2b", nextChangeAt: 0 }, "an automatic name starts no clock");

    const first = await setName(r, "P1", "Hawk_One");
    assert.strictEqual(first.ok, true, "the first real choice is free");
    assert.strictEqual(first.nextChangeAt, Math.floor((env.clock.now + WEEK_MS) / 1000));

    env.clock.now += 2 * DAY_MS;
    const early = await setName(r, "P1", "Hawk_Two");
    assert.strictEqual(early.error, "name_cooldown");
    assert.strictEqual(early.retryAfter, 5 * 24 * 60 * 60, "five days to go");
    assert.deepStrictEqual(await setName(r, "P1", "Hawk_One"), { ok: true, name: "Hawk_One", nextChangeAt: first.nextChangeAt }, "asking for the name already held is no change");

    env.clock.now += 5 * DAY_MS;
    assert.strictEqual((await setName(r, "P1", "Hawk_Two")).ok, true, "a week later it works again");
    assert.strictEqual((await setName(r, "P1", "Hawk_Three")).error, "name_cooldown", "and the clock starts over");
  });

  await test("setName: a name that is given up becomes free; an automatic rename keeps the clock", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await setName(r, "P1", "Old_Name")).ok, true);
    assert.strictEqual((await setName(r, "P2", "old_name")).error, "name_taken");
    env.clock.now += WEEK_MS + 1;
    assert.strictEqual((await setName(r, "P1", "New_Name")).ok, true);
    assert.strictEqual((await setName(r, "P2", "old_name")).ok, true, "P1 gave it up");
    assert.strictEqual((await setName(r, "P1", "Third_Name", true)).ok, true, "an automatic change is not held back");
    assert.strictEqual((await setName(r, "P1", "Fourth_Name")).error, "name_cooldown", "...but it did not reset the weekly clock either");
  });

  await test("setName: a stale holder never blocks a name", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await setName(r, "P1", "Ghost_Pilot")).ok, true);
    env.store.delete("np-P1");                              // P1's own record is gone (cut-short cleanup)
    assert.strictEqual((await setName(r, "P2", "ghost_pilot")).ok, true);
  });

  await test("deleteData frees the display name for other players", async () => {
    const env = makeEnv(); const r = makeRunner(env, SECRETS);
    assert.strictEqual((await setName(r, "P1", "Short_Lived")).ok, true);
    assert.strictEqual((await setName(r, "P2", "short_lived")).error, "name_taken");
    assert.strictEqual((await r.call("P1", { action: "deleteData" })).ok, true);
    assert.deepStrictEqual(await r.call("P1", { action: "getName" }), { ok: true, name: "", nextChangeAt: 0 });
    assert.strictEqual((await setName(r, "P2", "short_lived")).ok, true);
  });

  await test("unknown actions are rejected", async () => {
    const r = makeRunner(makeEnv(), SECRETS);
    assert.deepStrictEqual(await r.call("P1", { action: "nope" }), { ok: false, error: "unknown_action" });
  });

  console.log("\n" + passed + " checks passed" + (process.exitCode ? " - SOME FAILED" : ""));
})();
