using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HeraAgent
{
    internal sealed class ApprovalBinding
    {
        internal string OperationId;
        internal string Tool;
        internal string Action;
        internal string ArgumentsHash;
        internal string RiskClass;
        internal string ProjectId;
    }

    internal sealed class ApprovalGrant
    {
        internal string Token;
        internal long ExpiresAtMs;
    }

    internal sealed class ApprovalAuthority
    {
        const int TokenVersion = 1;
        static readonly long TokenLifetimeMs = (long)TimeSpan.FromMinutes(5).TotalMilliseconds;

        readonly byte[] _secret;
        readonly Func<long> _now;
        readonly Dictionary<string, long> _used = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly object _gate = new object();

        internal static ApprovalAuthority CreateProcessLocal()
        {
            var secret = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(secret);
            return new ApprovalAuthority(secret, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        internal ApprovalAuthority(byte[] secret, Func<long> now)
        {
            if (secret == null || secret.Length < 32)
                throw new ArgumentException("Approval secret must contain at least 32 bytes.", nameof(secret));
            _secret = (byte[])secret.Clone();
            _now = now ?? throw new ArgumentNullException(nameof(now));
        }

        internal ApprovalGrant Issue(ApprovalBinding binding)
        {
            var expiresAtMs = _now() + TokenLifetimeMs;
            var payload = new JObject
            {
                ["version"] = TokenVersion,
                ["operation_id"] = binding.OperationId,
                ["tool"] = binding.Tool,
                ["action"] = binding.Action == null ? JValue.CreateNull() : new JValue(binding.Action),
                ["arguments_hash"] = binding.ArgumentsHash,
                ["risk_class"] = binding.RiskClass,
                ["project_id"] = binding.ProjectId,
                ["expires_at_ms"] = expiresAtMs,
                ["single_use"] = true,
            };
            var encodedPayload = Base64Url(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
            return new ApprovalGrant
            {
                Token = encodedPayload + "." + Base64Url(Sign(encodedPayload)),
                ExpiresAtMs = expiresAtMs,
            };
        }

        internal ErrorResponse VerifyAndConsume(string token, ApprovalBinding expected)
        {
            if (!TryDecode(token, out var payload, out var tokenHash, out var error))
                return error;
            if (payload.Value<int?>("version") != TokenVersion
                || payload.Value<bool?>("single_use") != true)
            {
                return new ErrorResponse("INVALID_APPROVAL_TOKEN", "Approval token has an unsupported contract.");
            }
            var expiresAt = payload.Value<long?>("expires_at_ms");
            var now = _now();
            if (!expiresAt.HasValue || expiresAt.Value <= now)
                return new ErrorResponse("APPROVAL_EXPIRED", "Approval token has expired.");
            if (!Matches(payload, expected))
                return new ErrorResponse("APPROVAL_MISMATCH", "Approval token does not match this operation.");
            lock (_gate)
            {
                RemoveExpired(now);
                if (_used.ContainsKey(tokenHash))
                    return new ErrorResponse("APPROVAL_ALREADY_USED", "Approval token has already been used.");
                _used[tokenHash] = expiresAt.Value;
            }
            return null;
        }

        void RemoveExpired(long now)
        {
            var expired = new List<string>();
            foreach (var entry in _used)
            {
                if (entry.Value <= now)
                    expired.Add(entry.Key);
            }
            foreach (var tokenHash in expired)
                _used.Remove(tokenHash);
        }

        bool TryDecode(string token, out JObject payload, out string tokenHash, out ErrorResponse error)
        {
            payload = null;
            tokenHash = null;
            error = null;
            var parts = token?.Split('.');
            if (parts == null || parts.Length != 2)
            {
                error = new ErrorResponse("INVALID_APPROVAL_TOKEN", "Approval token envelope is invalid.");
                return false;
            }
            try
            {
                var supplied = Base64UrlDecode(parts[1]);
                var expected = Sign(parts[0]);
                if (!ConstantTimeEquals(supplied, expected))
                {
                    error = new ErrorResponse("INVALID_APPROVAL_TOKEN", "Approval token signature is invalid.");
                    return false;
                }
                payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
                tokenHash = Hash(token);
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is JsonException)
            {
                error = new ErrorResponse("INVALID_APPROVAL_TOKEN", "Approval token payload is invalid.");
                return false;
            }
        }

        static bool Matches(JObject payload, ApprovalBinding expected) =>
            string.Equals(payload.Value<string>("operation_id"), expected.OperationId, StringComparison.Ordinal)
            && string.Equals(payload.Value<string>("tool"), expected.Tool, StringComparison.Ordinal)
            && string.Equals(payload.Value<string>("action"), expected.Action, StringComparison.Ordinal)
            && string.Equals(payload.Value<string>("arguments_hash"), expected.ArgumentsHash, StringComparison.Ordinal)
            && string.Equals(payload.Value<string>("risk_class"), expected.RiskClass, StringComparison.Ordinal)
            && string.Equals(payload.Value<string>("project_id"), expected.ProjectId, StringComparison.Ordinal);

        byte[] Sign(string value)
        {
            using var hmac = new HMACSHA256(_secret);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        }

        static string Hash(string value)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        static bool ConstantTimeEquals(byte[] left, byte[] right)
        {
            var difference = left.Length ^ right.Length;
            var length = Math.Min(left.Length, right.Length);
            for (var index = 0; index < length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        static string Base64Url(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] Base64UrlDecode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }
    }
}
