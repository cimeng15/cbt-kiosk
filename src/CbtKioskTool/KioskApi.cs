using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace CbtKioskTool
{
    internal sealed class KioskPassword
    {
        public string Password = "";
        public DateTime? ExpiresAt;
        public bool IsExpired;
        public bool IsExpiredNow => IsExpired || (ExpiresAt.HasValue && ExpiresAt.Value < DateTime.Now);
    }

    /// <summary>
    /// Tiny client for the CBT kiosk endpoint(s), mirroring the contract documented in the README:
    ///   { "success": true, "data": { "exit_password": "...", "password_expires_at": "...", "is_expired": false } }
    /// Uses HttpWebRequest so the tool needs no extra assemblies.
    /// </summary>
    internal static class KioskApi
    {
        public static bool Fetch(string url, int timeoutMs, int attempts, int intervalMs, out KioskPassword password, out string error)
        {
            password = null;
            error = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "Alamat endpoint masih kosong.";
                return false;
            }

            if (attempts < 1) attempts = 1;
            if (timeoutMs < 500) timeoutMs = 500;

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.Timeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.UserAgent = "CbtKioskTool/1.0";
                    request.Accept = "application/json";
                    request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        var body = reader.ReadToEnd();
                        var status = (int)response.StatusCode;

                        if (status < 200 || status >= 300)
                        {
                            error = "HTTP " + status + " dari server.";
                        }
                        else if (TryParsePassword(body, out password, out var parseError))
                        {
                            return true;
                        }
                        else
                        {
                            error = parseError;
                        }
                    }
                }
                catch (WebException ex)
                {
                    var detail = ex.Message;
                    var httpResponse = ex.Response as HttpWebResponse;
                    if (httpResponse != null)
                        detail = "HTTP " + (int)httpResponse.StatusCode + " " + httpResponse.StatusDescription;
                    error = detail;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                if (attempt < attempts) Thread.Sleep(Math.Max(0, intervalMs));
            }

            return false;
        }

        /// <summary>Parses one endpoint response. Internal so tests can feed it canned payloads.</summary>
        internal static bool TryParsePassword(string body, out KioskPassword password, out string error)
        {
            password = null;
            error = null;

            try
            {
                var root = Json.AsObject(Json.Parse(body));
                if (root == null)
                {
                    error = "Respons bukan objek JSON.";
                    return false;
                }

                if (root.ContainsKey("success") && !Json.AsBool(root["success"], true))
                {
                    error = "Server menjawab success=false.";
                    return false;
                }

                object dataValue;
                var data = root.TryGetValue("data", out dataValue) ? Json.AsObject(dataValue) : null;
                if (data == null)
                {
                    error = "Respons tidak memuat objek 'data'.";
                    return false;
                }

                var result = new KioskPassword();

                object v;
                if (data.TryGetValue("exit_password", out v)) result.Password = Json.AsString(v) ?? "";

                if (data.TryGetValue("is_expired", out v)) result.IsExpired = Json.AsBool(v, false);

                if (data.TryGetValue("password_expires_at", out v))
                {
                    var raw = Json.AsString(v);
                    DateTime parsed;
                    if (!string.IsNullOrWhiteSpace(raw) &&
                        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                        result.ExpiresAt = parsed;
                }

                password = result;
                return true;
            }
            catch (Exception ex)
            {
                error = "Respons tidak dapat dibaca: " + ex.Message;
                return false;
            }
        }

        /// <summary>Full check used by the "Tes koneksi" button.</summary>
        public static bool TestConnection(AppSettings probe, out string message)
        {
            var endpoints = new List<string>();
            if (!string.IsNullOrWhiteSpace(probe.KioskUrl)) endpoints.Add(probe.KioskUrl);
            if (!string.IsNullOrWhiteSpace(probe.FallbackUrl) && !endpoints.Contains(probe.FallbackUrl)) endpoints.Add(probe.FallbackUrl);

            if (endpoints.Count == 0)
            {
                message = "Alamat endpoint masih kosong.";
                return false;
            }

            var problems = new List<string>();
            foreach (var endpoint in endpoints)
            {
                KioskPassword password;
                string error;
                if (Fetch(endpoint, probe.KioskTimeoutMs, probe.KioskAttempts, probe.KioskAttemptIntervalMs, out password, out error))
                {
                    if (password.IsExpiredNow)
                    {
                        message = "OK - endpoint terjangkau, TETAPI password online KEDALUWARSA (expires: " +
                                  Format(password.ExpiresAt) + "). Password darurat offline tetap bisa dipakai untuk keluar.";
                    }
                    else if (string.IsNullOrEmpty(password.Password))
                    {
                        message = "OK - endpoint terjangkau, tetapi password KOSONG (belum diatur di panel CBT).";
                    }
                    else
                    {
                        message = "OK - endpoint terjangkau. Password online tersedia (kedaluwarsa: " +
                                  (password.ExpiresAt.HasValue ? Format(password.ExpiresAt) : "tidak disebutkan") + ").";
                    }
                    return true;
                }

                problems.Add(endpoint + " -> " + (error ?? "gagal"));
            }

            message = "GAGAL - " + string.Join(" | ", problems.ToArray());
            return false;
        }

        /// <summary>Checks a candidate against the ONLINE password only (diagnostic for the supervisor).</summary>
        public static string CheckOnlinePassword(AppSettings probe, string candidate, out bool ok)
        {
            ok = false;

            KioskPassword password;
            string error;
            if (!Fetch(probe.KioskUrl, probe.KioskTimeoutMs, probe.KioskAttempts, probe.KioskAttemptIntervalMs, out password, out error))
                return "GAGAL menghubungi endpoint. " + (error ?? "");

            if (password.IsExpiredNow)
                return "Password online KEDALUWARSA (" + Format(password.ExpiresAt) + "). " +
                       "Perbarui di panel CBT, atau pakai password darurat offline.";

            if (string.IsNullOrEmpty(password.Password))
                return "Server menjawab tetapi password KOSONG (belum diatur di panel CBT).";

            if (Hash.FixedTimeEquals(candidate, password.Password))
            {
                ok = true;
                return "BENAR - password ini diterima oleh panel CBT.";
            }

            return "SALAH - password ini tidak sama dengan password online dari panel CBT.";
        }

        private static string Format(DateTime? value) =>
            value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : "(tidak disebutkan)";
    }
}
