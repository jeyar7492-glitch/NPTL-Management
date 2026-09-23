namespace NPTELManagement.Core.Interfaces;

public interface ILoginRateLimiter
{
    bool IsAllowed(string key);
    void RecordFailedAttempt(string key);
    void ResetAttempts(string key);
    TimeSpan? GetRemainingLockoutTime(string key);
}
