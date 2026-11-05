using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Signs in through the storefront's login form and waits until the profile is stored and the login route is left.</summary>
public sealed class SignInThroughTheLoginForm(string email, string password, bool openTheLoginPage = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = actor.LookingAtThePage();
        var loginPage = new LoginPage(page);

        if (openTheLoginPage)
            await loginPage.NavigateAsync();

        // AuthGuard flips off /login as soon as tokens land; the role-based redirect needs /me first.
        var signedIn = page.WaitForResponseAsync(r =>
            r.Url.Contains("/signin", StringComparison.OrdinalIgnoreCase) && r.Ok);
        var profileLoaded = page.WaitForResponseAsync(r =>
            r.Url.Contains("/me", StringComparison.OrdinalIgnoreCase) &&
            r.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            r.Ok);

        await loginPage.SignInAsync(email, password);
        await signedIn;
        await profileLoaded;

        await page.WaitForFunctionAsync(
            "() => { try { const u = JSON.parse(localStorage.getItem('user') || 'null'); return !!u?.role; } catch { return false; } }");

        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.Ordinal));
    }
}
