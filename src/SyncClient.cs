// SyncClient.cs - raw REST client for Supabase (Auth + PostgREST).
// Deliberately no Supabase SDK: HttpClient + System.Text.Json only.
//
// Coupling: SupabaseUrl/ApiKey belong to the "monitor-switch" Supabase
// project (ref cvnpmmmkzphhgmimfrpi). The table schema lives in that
// project's migrations: public.profiles
//   (user_id, machine_name, slot 'A'|'B', name, input_values int[], updated_at)
// with RLS restricting every operation to user_id = auth.uid().
// The publishable API key is safe to embed; RLS is the security boundary.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MonitorSwitch
{
    // Thrown with a message that is safe/useful to show the user.
    class SyncException : Exception
    {
        public SyncException(string message) : base(message) { }
    }

    class ProfileRow
    {
        public string Slot;              // "A" or "B"
        public string Name;
        public List<uint> Values;
        public DateTime UpdatedAtUtc;
    }

    static class SyncClient
    {
        const string SupabaseUrl = "https://cvnpmmmkzphhgmimfrpi.supabase.co";
        const string ApiKey = "sb_publishable_ELKtUk5tUn7Rk2txYAYaNQ_hf6MDGBM";

        static readonly HttpClient http = CreateClient();

        public static string Email { get; private set; }
        static string accessToken;
        static string refreshToken;
        static DateTime accessExpiresUtc = DateTime.MinValue;

        public static bool IsSignedIn { get { return refreshToken != null; } }

        static HttpClient CreateClient()
        {
            var c = new HttpClient();
            c.Timeout = TimeSpan.FromSeconds(15);
            c.DefaultRequestHeaders.Add("apikey", ApiKey);
            return c;
        }

        // ----- wire DTOs ------------------------------------------------------

        class SessionDto
        {
            [JsonPropertyName("access_token")] public string AccessToken { get; set; }
            [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
            [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; }
            [JsonPropertyName("user")] public UserDto User { get; set; }
        }

        class UserDto
        {
            [JsonPropertyName("email")] public string Email { get; set; }
        }

        class ErrorDto
        {
            [JsonPropertyName("msg")] public string Msg { get; set; }
            [JsonPropertyName("message")] public string Message { get; set; }
            [JsonPropertyName("error_description")] public string ErrorDescription { get; set; }
            [JsonPropertyName("error_code")] public string ErrorCode { get; set; }
        }

        class RowDto
        {
            [JsonPropertyName("machine_name")] public string MachineName { get; set; }
            [JsonPropertyName("slot")] public string Slot { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; }
            [JsonPropertyName("input_values")] public List<uint> InputValues { get; set; }
            [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; set; }
        }

        // ----- auth -----------------------------------------------------------

        public static async Task SignInAsync(string email, string password)
        {
            var body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "email", email }, { "password", password }
            });
            string resp = await PostAsync(
                SupabaseUrl + "/auth/v1/token?grant_type=password", body, null);
            AdoptSession(resp, email);
            if (!IsSignedIn) throw new SyncException("Sign-in didn't return a session.");
        }

        // Returns true if a session was created immediately; false if the
        // account needs email confirmation first.
        public static async Task<bool> SignUpAsync(string email, string password)
        {
            var body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "email", email }, { "password", password }
            });
            string resp = await PostAsync(SupabaseUrl + "/auth/v1/signup", body, null);
            AdoptSession(resp, email);
            return IsSignedIn;
        }

        // Silent sign-in from the stored refresh token. Returns false if there
        // is no stored session; throws only on network trouble.
        public static async Task<bool> TryRestoreAsync()
        {
            string email, stored;
            if (!AuthStore.TryLoad(out email, out stored)) return false;
            refreshToken = stored;
            Email = email;
            try
            {
                await RefreshAsync();
                return true;
            }
            catch (SyncException)
            {
                // Refresh token rejected (revoked/expired) - forget it.
                SignOut();
                return false;
            }
        }

        static async Task RefreshAsync()
        {
            var body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "refresh_token", refreshToken }
            });
            string resp = await PostAsync(
                SupabaseUrl + "/auth/v1/token?grant_type=refresh_token", body, null);
            AdoptSession(resp, Email);
        }

        public static void SignOut()
        {
            accessToken = null;
            refreshToken = null;
            Email = null;
            accessExpiresUtc = DateTime.MinValue;
            AuthStore.Delete();
        }

        static void AdoptSession(string json, string fallbackEmail)
        {
            var session = JsonSerializer.Deserialize<SessionDto>(json);
            if (session == null || string.IsNullOrEmpty(session.AccessToken)) return;
            accessToken = session.AccessToken;
            refreshToken = session.RefreshToken;
            accessExpiresUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, session.ExpiresIn));
            Email = (session.User != null && !string.IsNullOrEmpty(session.User.Email))
                ? session.User.Email : fallbackEmail;
            AuthStore.Save(Email, refreshToken);
        }

        static async Task<string> EnsureAccessTokenAsync()
        {
            if (refreshToken == null)
                throw new SyncException("Not signed in.");
            if (accessToken == null || DateTime.UtcNow > accessExpiresUtc.AddSeconds(-60))
                await RefreshAsync();
            return accessToken;
        }

        // ----- data -----------------------------------------------------------

        public static async Task<List<ProfileRow>> FetchAsync(string machineName)
        {
            string token = await EnsureAccessTokenAsync();
            string url = SupabaseUrl + "/rest/v1/profiles"
                + "?select=slot,name,input_values,updated_at"
                + "&machine_name=eq." + Uri.EscapeDataString(machineName);

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                req.Headers.Add("Authorization", "Bearer " + token);
                string resp = await SendAsync(req);
                var rows = JsonSerializer.Deserialize<List<RowDto>>(resp) ?? new List<RowDto>();
                var result = new List<ProfileRow>();
                foreach (var r in rows)
                {
                    result.Add(new ProfileRow
                    {
                        Slot = r.Slot,
                        Name = r.Name,
                        Values = r.InputValues ?? new List<uint>(),
                        UpdatedAtUtc = r.UpdatedAt.UtcDateTime
                    });
                }
                return result;
            }
        }

        public static async Task UpsertAsync(string machineName, List<ProfileRow> rows)
        {
            if (rows.Count == 0) return;
            string token = await EnsureAccessTokenAsync();

            var dtos = new List<RowDto>();
            foreach (var r in rows)
            {
                dtos.Add(new RowDto
                {
                    MachineName = machineName,
                    Slot = r.Slot,
                    Name = r.Name,
                    InputValues = r.Values,
                    UpdatedAt = new DateTimeOffset(r.UpdatedAtUtc, TimeSpan.Zero)
                });
            }

            using (var req = new HttpRequestMessage(HttpMethod.Post, SupabaseUrl + "/rest/v1/profiles"))
            {
                req.Headers.Add("Authorization", "Bearer " + token);
                req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
                req.Content = new StringContent(
                    JsonSerializer.Serialize(dtos), Encoding.UTF8, "application/json");
                await SendAsync(req);
            }
        }

        // ----- plumbing -------------------------------------------------------

        static async Task<string> PostAsync(string url, string jsonBody, string bearer)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (bearer != null)
                    req.Headers.Add("Authorization", "Bearer " + bearer);
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                return await SendAsync(req);
            }
        }

        static async Task<string> SendAsync(HttpRequestMessage req)
        {
            HttpResponseMessage resp;
            try
            {
                resp = await http.SendAsync(req);
            }
            catch (TaskCanceledException)
            {
                throw new SyncException("The sync server didn't respond (timeout).");
            }
            catch (HttpRequestException)
            {
                throw new SyncException("No connection to the sync server.");
            }

            using (resp)
            {
                string body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode) return body;
                throw new SyncException(ExtractError(body, (int)resp.StatusCode));
            }
        }

        static string ExtractError(string body, int status)
        {
            try
            {
                var err = JsonSerializer.Deserialize<ErrorDto>(body);
                if (err != null)
                {
                    string m = err.Msg ?? err.Message ?? err.ErrorDescription;
                    if (!string.IsNullOrEmpty(m))
                    {
                        // Friendlier phrasing for the two most common cases.
                        if (err.ErrorCode == "invalid_credentials")
                            return "Wrong email or password.";
                        if (err.ErrorCode == "email_not_confirmed")
                            return "Email not confirmed yet - check your inbox for the confirmation link.";
                        return m;
                    }
                }
            }
            catch { }
            return "Sync server error (HTTP " + status + ").";
        }
    }
}
