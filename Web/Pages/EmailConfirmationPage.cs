namespace Web.Pages;

public static class EmailConfirmationPage
{
    public static string Render(bool succeeded)
    {
        var title = succeeded ? "Email verified successfully" : "Не удалось подтвердить email";
        var message = succeeded
            ? "Your email is confirmed. Return to the app and sign in to your account."
            : "Ссылка недействительна или срок её действия истёк. Вернитесь в приложение и запросите новое письмо подтверждения.";
        var englishTitle = succeeded ? "Email verified successfully" : "Email verification failed";
        var englishMessage = succeeded
            ? "Your email is confirmed. Return to the app and sign in to your account."
            : "This link is invalid or has expired. Return to the app and request a new confirmation email.";
        return $$"""
            <!doctype html>
            <html lang="{{(succeeded ? "en" : "ru")}}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="referrer" content="no-referrer"><title>{{title}}</title>
            <style>
            :root { color-scheme: light dark; font-family: system-ui, sans-serif; background: #111; color: #eee; }
            * { box-sizing: border-box; } body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px; }
            main { width: 100%; max-width: 520px; padding: 40px; border: 1px solid #333; border-radius: 10px; background: #151515; }
            h1 { font-size: 28px; line-height: 1.25; margin: 0 0 20px; } p { color: #aaa; line-height: 1.6; margin: 0; }
            @media(prefers-color-scheme: light) { :root { background: #fff; color: #171717; } main { background: #fafafa; border-color: #ddd; } p { color: #555; } }
            @media(max-width: 480px) { main { padding: 26px; } h1 { font-size: 24px; } }
            </style></head><body><main><h1 id="title">{{title}}</h1><p id="message">{{message}}</p></main>
            <script>
            if (!navigator.language.toLowerCase().startsWith('ru')) {
              document.documentElement.lang = 'en'; document.title = '{{englishTitle}}';
              document.getElementById('title').textContent = '{{englishTitle}}';
              document.getElementById('message').textContent = '{{englishMessage}}';
            }
            history.replaceState(null, '', location.pathname);
            </script></body></html>
            """;
    }
}
