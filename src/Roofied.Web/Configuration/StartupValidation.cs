namespace Roofied.Web.Configuration;

/// <summary>
/// Validates that required configuration is present before the app starts. Reports problems
/// clearly without leaking secret values. In production, missing critical config aborts startup.
/// </summary>
public static class StartupValidation
{
    public static void Validate(IConfiguration config, IHostEnvironment env)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(config.GetConnectionString("DefaultConnection")))
            problems.Add("ConnectionStrings:DefaultConnection is not configured.");

        var ipSalt = config["Security:IpHashSalt"];
        if (string.IsNullOrWhiteSpace(ipSalt))
        {
            if (env.IsProduction())
                problems.Add("Security:IpHashSalt is required in production (used to hash client IPs).");
        }
        else if (env.IsProduction() && ipSalt.Length < 16)
        {
            problems.Add("Security:IpHashSalt should be a strong secret of at least 16 characters in production.");
        }

        var captchaEnabled = config.GetValue<bool>("Captcha:Enabled");
        if (captchaEnabled && string.IsNullOrWhiteSpace(config["Captcha:SecretKey"]))
            problems.Add("Captcha:Enabled is true but Captcha:SecretKey is not configured.");

        if (problems.Count == 0)
            return;

        var message = "Configuration validation failed:" + Environment.NewLine +
                      string.Join(Environment.NewLine, problems.Select(p => " - " + p));

        if (env.IsProduction())
            throw new InvalidOperationException(message);

        // In development, warn loudly but allow the app to run with safe fallbacks.
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[startup warning] " + message);
        Console.ResetColor();
    }
}
