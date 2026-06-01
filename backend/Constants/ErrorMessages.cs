namespace MechanicApp.Server.Constants
{
    /// <summary>
    /// Centralized error and response messages used across controllers.
    /// </summary>
    public static class ErrorMessages
    {
        public const string RequiredFieldsMissing = "Some required fields are not filled. Please check them.";
        public const string InvalidCredentials = "Invalid username or password.";
        public const string AccountDisabled = "Account is disabled. Contact your administrator.";
        public const string UsernamePasswordRequired = "Username and password are required.";
    }
}
