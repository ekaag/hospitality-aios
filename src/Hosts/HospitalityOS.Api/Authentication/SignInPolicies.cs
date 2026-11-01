using Microsoft.AspNetCore.Authorization;

namespace HospitalityOS.Api.Authentication;

/// <summary>
/// What a sign-in may call before it acts as anyone (K4b): <see cref="SignIn"/> admits anyone signed in through the
/// identity provider, linked or not (to see who they act as and redeem an invite); <see cref="Operator"/> admits the
/// platform's operators, who invite customers' first administrators. Neither admits stay tokens.
/// </summary>
internal static class SignInPolicies
{
    public const string SignIn = nameof(SignIn);
    public const string Operator = nameof(Operator);

    /// <summary>
    /// Anyone signed in or holding a staff token, who may ask who they act as.
    /// </summary>
    public const string Caller = nameof(Caller);

    public static void Add(AuthorizationOptions options)
    {
        options.AddPolicy(SignIn, policy => policy.RequireAssertion(context => SignInClaims.IsSignIn(context.User)));
        options.AddPolicy(Caller, policy => policy.RequireAssertion(context =>
            SignInClaims.IsSignIn(context.User) || Enum.GetNames<SharedKernel.Execution.StaffRole>().Any(context.User.IsInRole)));
        options.AddPolicy(Operator, policy => policy.RequireAssertion(context => SignInClaims.IsSignIn(context.User)).RequireRole(SignInClaims.OperatorRole));
    }
}
