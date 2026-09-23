using System.Collections.Concurrent;
using NPTELManagement.Core.Interfaces;

namespace NPTELManagement.Infrastructure.Authentication;

public class LoginRateLimiter : ILoginRateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _attempts = new();
    private readonly int _maxAttempts;
    private readonly TimeSpan _lockoutDuration;

    public LoginRateLimiter(int maxAttempts = 5, int lockoutMinutes = 5)
    {
        _maxAttempts = maxAttempts;
        _lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
    }

    private class RateLimitEntry
    {
        public int FailedCount { get; set; }
        public DateTime? LockoutUntil { get; set; }
        public DateTime LastAttempt { get; set; } = DateTime.UtcNow;
    }

    public bool IsAllowed(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        if (_attempts.TryGetValue(key, out var entry))
        {
            if (entry.LockoutUntil.HasValue)
            {
                if (DateTime.UtcNow < entry.LockoutUntil.Value)
                    return false;

                // Lockout expired, reset
                _attempts.TryRemove(key, out _);
                return true;
            }
        }

        return true;
    }

    public void RecordFailedAttempt(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        _attempts.AddOrUpdate(key,
            _ => new RateLimitEntry { FailedCount = 1, LastAttempt = DateTime.UtcNow },
            (_, entry) =>
            {
                // If attempt was long ago (more than lockout duration), reset count
                if (DateTime.UtcNow - entry.LastAttempt > _lockoutDuration)
                {
                    entry.FailedCount = 1;
                    entry.LockoutUntil = null;
                }
                else
                {
                    entry.FailedCount++;
                    if (entry.FailedCount >= _maxAttempts)
                    {
                        entry.LockoutUntil = DateTime.UtcNow.Add(_lockoutDuration);
                    }
                }
                entry.LastAttempt = DateTime.UtcNow;
                return entry;
            });
    }

    public void ResetAttempts(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _attempts.TryRemove(key, out _);
        }
    }

    public TimeSpan? GetRemainingLockoutTime(string key)
    {
        if (_attempts.TryGetValue(key, out var entry) && entry.LockoutUntil.HasValue)
        {
            var remaining = entry.LockoutUntil.Value - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
                return remaining;
        }

        return null;
    }
}
