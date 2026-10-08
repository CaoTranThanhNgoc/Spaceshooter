// Space Hawk mail relay - a tiny Google Apps Script web app.
//
// It lets the account server (Unity Cloud Code, Assets/CloudCode/AccountRecovery.js) send the 6-digit
// verification / password-reset codes from YOUR OWN Gmail account - no domain, no paid mail service.
// Google sends the mail itself, so it reaches the inbox (not spam). Limit: about 100 mails a day on a
// normal Gmail account, plenty for an indie game; move to a real mail service later if it grows.
//
// Set-up (once):
//  1. script.google.com > New project > paste this whole file > save.
//  2. Project Settings (gear) > Script properties > Add script property:
//       RELAY_KEY = any long random string (the same value goes into the Unity secret MAIL_RELAY_KEY).
//  3. Deploy > New deployment > type "Web app" > Execute as: Me > Who has access: Anyone > Deploy.
//     Google asks once to authorize "send email on your behalf" - allow it.
//  4. Copy the "Web app URL" (ends in /exec): it goes into the Unity secret MAIL_RELAY_URL.
// Anyone who has the URL AND the key could send mail as you, so keep both private (they live only in
// Unity's Secret Manager and in this script's properties).

function doPost(e) {
  try {
    var data = JSON.parse(e.postData.contents);
    var key = PropertiesService.getScriptProperties().getProperty("RELAY_KEY");
    if (!key || data.key !== key) return reply({ ok: false, error: "forbidden" });

    var to = String(data.to || "");
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(to) || to.length > 120) return reply({ ok: false, error: "bad_recipient" });

    MailApp.sendEmail({
      to: to,
      subject: String(data.subject || "Space Hawk").slice(0, 120),
      body: String(data.body || "").slice(0, 1000),
      name: "Space Hawk",
    });
    return reply({ ok: true });
  } catch (err) {
    return reply({ ok: false, error: "error" });
  }
}

function reply(result) {
  return ContentService.createTextOutput(JSON.stringify(result)).setMimeType(ContentService.MimeType.JSON);
}
